using Avalonia;
using Avalonia.Media;
using AiBrowserWorkspace.Commands;
using AiBrowserWorkspace.Models;
using AiBrowserWorkspace.Services;
using WebViewControl;

namespace AiBrowserWorkspace.ViewModels;

public sealed class BrowserSessionViewModel : ViewModelBase
{
    private static readonly IBrush ChatGptAccentBrush = Solid(0x10, 0xA3, 0x7F);
    private static readonly IBrush ClaudeAccentBrush = Solid(0xCC, 0x78, 0x5C);
    private static readonly IBrush ReadyBrush = Solid(0x4C, 0xAF, 0x8C);
    private static readonly IBrush BusyBrush = Solid(0xE8, 0xA3, 0x3D);
    private static readonly IBrush ErrorBrush = Solid(0xE0, 0x5A, 0x5A);
    private static readonly IBrush NeutralBorderBrush = Solid(0x2A, 0x2E, 0x37);

    private readonly IAiSiteAdapterFactory _adapterFactory;

    private string _promptText = string.Empty;
    private bool _isSelected;
    private bool _includeInBroadcast = true;
    private SessionStatus _status = SessionStatus.Initializing;
    private string? _errorMessage;
    private WebView? _browserControl;

    public BrowserSessionViewModel(AiBrowserSession session, IAiSiteAdapterFactory adapterFactory)
    {
        Session = session;
        _adapterFactory = adapterFactory;

        SendCommand = new AsyncRelayCommand(SendAsync, CanSend);
        ClearCommand = new RelayCommand(Clear, () => PromptText.Length > 0);
    }

    public AiBrowserSession Session { get; }

    public Guid Id => Session.Id;
    public string DisplayName => Session.DisplayName;
    public AiPlatform Platform => Session.Platform;
    public string PlatformDisplayName => Platform.GetDisplayName();
    public Uri HomeUri => Platform.GetHomeUri();
    public ProxyEndpoint? Proxy => Session.Proxy;
    public string ProxyDisplayText => Proxy is null ? "Direct (no proxy)" : $"via {Proxy}";

    public AsyncRelayCommand SendCommand { get; }
    public RelayCommand ClearCommand { get; }

    public string PromptText
    {
        get => _promptText;
        set
        {
            if (SetField(ref _promptText, value))
            {
                OnPropertyChanged(nameof(CharacterCount));
                OnPropertyChanged(nameof(PromptIsEmpty));
                SendCommand.RaiseCanExecuteChanged();
                ClearCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetField(ref _isSelected, value))
            {
                OnPropertyChanged(nameof(CardBorderBrush));
                OnPropertyChanged(nameof(CardBorderThickness));
            }
        }
    }

    // Whether this session receives the shared prompt when the owning MainViewModel
    // broadcasts one; defaults on so a freshly added session joins broadcasts immediately.
    public bool IncludeInBroadcast
    {
        get => _includeInBroadcast;
        set => SetField(ref _includeInBroadcast, value);
    }

    public SessionStatus Status
    {
        get => _status;
        set
        {
            if (SetField(ref _status, value))
            {
                OnPropertyChanged(nameof(StatusDisplayText));
                OnPropertyChanged(nameof(StatusBrush));
                OnPropertyChanged(nameof(IsLoadingOverlayVisible));
                OnPropertyChanged(nameof(HasError));
                OnPropertyChanged(nameof(SendButtonLabel));
                SendCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        set
        {
            if (SetField(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public WebView? BrowserControl
    {
        get => _browserControl;
        set
        {
            if (SetField(ref _browserControl, value))
            {
                SendCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasError => Status == SessionStatus.Error && !string.IsNullOrEmpty(ErrorMessage);
    public bool IsLoadingOverlayVisible => Status is SessionStatus.Initializing or SessionStatus.Loading;
    public bool PromptIsEmpty => string.IsNullOrEmpty(PromptText);
    public int CharacterCount => PromptText.Length;

    public string SendButtonLabel => Status == SessionStatus.Sending ? "Sending…" : "Send";

    public string StatusDisplayText => Status switch
    {
        SessionStatus.Initializing => "Initializing…",
        SessionStatus.Loading => "Loading…",
        SessionStatus.Ready => "Ready",
        SessionStatus.Sending => "Sending…",
        SessionStatus.Sent => "Sent",
        SessionStatus.Error => "Error",
        _ => Status.ToString()
    };

    public IBrush StatusBrush => Status switch
    {
        SessionStatus.Ready or SessionStatus.Sent => ReadyBrush,
        SessionStatus.Error => ErrorBrush,
        _ => BusyBrush
    };

    public IBrush AccentBrush => Platform == AiPlatform.ChatGpt ? ChatGptAccentBrush : ClaudeAccentBrush;

    public IBrush CardBorderBrush => IsSelected ? AccentBrush : NeutralBorderBrush;
    public Thickness CardBorderThickness => IsSelected ? new Thickness(2) : new Thickness(1);

    private bool CanSend(object? parameter) =>
        Status != SessionStatus.Sending &&
        Status != SessionStatus.Initializing &&
        !string.IsNullOrWhiteSpace(PromptText) &&
        _browserControl is not null;

    private Task SendAsync(object? parameter) => SendPromptCoreAsync(PromptText, clearPromptOnSuccess: true);

    // Used by MainViewModel's broadcast: sends an explicit prompt without touching this
    // session's own draft (PromptText), so a broadcast can never clobber what the user was
    // mid-typing for this session before the broadcast ran.
    public Task<bool> SendBroadcastPromptAsync(string prompt, CancellationToken cancellationToken = default) =>
        SendPromptCoreAsync(prompt, clearPromptOnSuccess: false, cancellationToken);

    private async Task<bool> SendPromptCoreAsync(string prompt, bool clearPromptOnSuccess, CancellationToken cancellationToken = default)
    {
        if (_browserControl is null)
        {
            Status = SessionStatus.Error;
            ErrorMessage = "The browser for this session is not ready yet.";
            return false;
        }

        if (Status == SessionStatus.Sending)
        {
            // Already mid-send (e.g. a manual Send racing a broadcast); skip rather than
            // interleave two prompts into the same composer.
            return false;
        }

        Status = SessionStatus.Sending;
        ErrorMessage = null;

        try
        {
            var adapter = _adapterFactory.GetAdapter(Platform);
            await adapter.SendPromptAsync(_browserControl, prompt, cancellationToken);
            Status = SessionStatus.Sent;
            if (clearPromptOnSuccess)
            {
                PromptText = string.Empty;
            }

            return true;
        }
        catch (Exception ex)
        {
            Status = SessionStatus.Error;
            ErrorMessage = ex.Message;
            return false;
        }
        finally
        {
            _ = RevertToReadyAfterDelayAsync();
        }
    }

    private async Task RevertToReadyAfterDelayAsync()
    {
        await Task.Delay(TimeSpan.FromSeconds(2));
        if (Status == SessionStatus.Sent)
        {
            Status = SessionStatus.Ready;
        }
    }

    private void Clear()
    {
        PromptText = string.Empty;
        if (Status == SessionStatus.Error)
        {
            Status = SessionStatus.Ready;
            ErrorMessage = null;
        }
    }

    private static IBrush Solid(byte r, byte g, byte b) => new SolidColorBrush(Color.FromRgb(r, g, b));
}
