using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using AiBrowserWorkspace.Commands;
using AiBrowserWorkspace.Models;
using AiBrowserWorkspace.Services;

namespace AiBrowserWorkspace.ViewModels;

// The dashboard: session cards (browser + address bar) and an optional broadcast
// bar that fans one shared prompt out to every checked card.
public sealed class MainViewModel : ViewModelBase
{
    private readonly IBrowserSessionService _sessionService;
    private readonly IDialogService _dialogService;
    private string _broadcastPromptText = string.Empty;
    private string _broadcastStatusMessage = string.Empty;
    private bool _isBroadcastPanelOpen;
    private bool _isBroadcasting;
    private BrowserSessionViewModel? _selectedSession;

    public MainViewModel(IBrowserSessionService sessionService, IDialogService dialogService)
    {
        _sessionService = sessionService;
        _dialogService = dialogService;

        Sessions.CollectionChanged += OnSessionsCollectionChanged;

        AddBrowserCommand = new AsyncRelayCommand(AddBrowserFromDialogAsync);
        RemoveBrowserCommand = new AsyncRelayCommand(RemoveBrowserAsync);
        ToggleBroadcastPanelCommand = new RelayCommand(() => IsBroadcastPanelOpen = !IsBroadcastPanelOpen);
        BroadcastCommand = new AsyncRelayCommand(BroadcastAsync, CanBroadcast);
    }

    public ObservableCollection<BrowserSessionViewModel> Sessions { get; } = new();

    public bool ShowEmptyState => Sessions.Count == 0;

    // Drives each card's highlighted border (BrowserSessionViewModel.CardBorderBrush).
    public BrowserSessionViewModel? SelectedSession
    {
        get => _selectedSession;
        set
        {
            var previous = _selectedSession;
            if (!SetField(ref _selectedSession, value))
            {
                return;
            }

            if (previous is not null)
            {
                previous.IsSelected = false;
            }

            if (value is not null)
            {
                value.IsSelected = true;
            }
        }
    }

    public bool IsBroadcastPanelOpen
    {
        get => _isBroadcastPanelOpen;
        set
        {
            if (SetField(ref _isBroadcastPanelOpen, value))
            {
                OnPropertyChanged(nameof(BroadcastToggleButtonLabel));
            }
        }
    }

    public string BroadcastToggleButtonLabel => IsBroadcastPanelOpen ? "Hide Broadcast" : "Broadcast";

    public string BroadcastPromptText
    {
        get => _broadcastPromptText;
        set
        {
            if (SetField(ref _broadcastPromptText, value))
            {
                OnPropertyChanged(nameof(BroadcastCharacterCount));
                OnPropertyChanged(nameof(BroadcastPromptIsEmpty));
                BroadcastCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public int BroadcastCharacterCount => BroadcastPromptText.Length;
    public bool BroadcastPromptIsEmpty => string.IsNullOrEmpty(BroadcastPromptText);

    // Sessions opted into the broadcast via their card checkbox, regardless of which
    // sessions actually exist yet to receive it.
    public int BroadcastTargetCount => Sessions.Count(s => s.IncludeInBroadcast);

    public bool IsBroadcasting
    {
        get => _isBroadcasting;
        private set
        {
            if (SetField(ref _isBroadcasting, value))
            {
                OnPropertyChanged(nameof(BroadcastButtonLabel));
            }
        }
    }

    public string BroadcastButtonLabel => IsBroadcasting ? "Sending…" : "Send to Selected";

    public string BroadcastStatusMessage
    {
        get => _broadcastStatusMessage;
        private set
        {
            if (SetField(ref _broadcastStatusMessage, value))
            {
                OnPropertyChanged(nameof(HasBroadcastStatusMessage));
            }
        }
    }

    public bool HasBroadcastStatusMessage => !string.IsNullOrEmpty(BroadcastStatusMessage);

    public AsyncRelayCommand AddBrowserCommand { get; }
    public AsyncRelayCommand RemoveBrowserCommand { get; }
    public RelayCommand ToggleBroadcastPanelCommand { get; }
    public AsyncRelayCommand BroadcastCommand { get; }

    public BrowserSessionViewModel AddSession(AiPlatform platform, string? requestedName = null, ProxyEndpoint? proxy = null)
    {
        var session = _sessionService.CreateSession(platform, requestedName, proxy);
        Sessions.Add(session);
        SelectedSession = session;
        return session;
    }

    private async Task AddBrowserFromDialogAsync()
    {
        var result = await _dialogService.ShowAddBrowserDialogAsync();
        if (result is null)
        {
            return;
        }

        AddSession(result.Platform, result.SessionName, result.Proxy);
    }

    private Task RemoveBrowserAsync(object? parameter)
    {
        if (parameter is BrowserSessionViewModel session)
        {
            Sessions.Remove(session);
            if (ReferenceEquals(SelectedSession, session))
            {
                SelectedSession = Sessions.FirstOrDefault();
            }
        }

        return Task.CompletedTask;
    }

    private bool CanBroadcast(object? parameter) =>
        !string.IsNullOrWhiteSpace(BroadcastPromptText) && Sessions.Any(s => s.IncludeInBroadcast);

    private async Task BroadcastAsync(object? parameter)
    {
        var targets = Sessions.Where(s => s.IncludeInBroadcast).ToList();
        if (targets.Count == 0)
        {
            return;
        }

        var prompt = BroadcastPromptText;
        IsBroadcasting = true;
        try
        {
            // Fire all sends together rather than one at a time, so this doesn't
            // serialize behind whichever session's page is slowest to respond.
            var results = await Task.WhenAll(targets.Select(session => session.SendBroadcastPromptAsync(prompt)));
            var succeeded = results.Count(ok => ok);

            BroadcastStatusMessage = succeeded == targets.Count
                ? $"Sent to all {targets.Count} selected session{(targets.Count == 1 ? string.Empty : "s")}."
                : $"Sent to {succeeded} of {targets.Count} selected sessions — check the ones showing an error.";

            if (succeeded == targets.Count)
            {
                BroadcastPromptText = string.Empty;
            }
        }
        finally
        {
            IsBroadcasting = false;
        }
    }

    private void OnSessionsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (BrowserSessionViewModel session in e.OldItems)
            {
                session.PropertyChanged -= OnSessionPropertyChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (BrowserSessionViewModel session in e.NewItems)
            {
                session.PropertyChanged += OnSessionPropertyChanged;
            }
        }

        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(BroadcastTargetCount));
        BroadcastCommand.RaiseCanExecuteChanged();
    }

    private void OnSessionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(BrowserSessionViewModel.IncludeInBroadcast))
        {
            return;
        }

        OnPropertyChanged(nameof(BroadcastTargetCount));
        BroadcastCommand.RaiseCanExecuteChanged();
    }
}
