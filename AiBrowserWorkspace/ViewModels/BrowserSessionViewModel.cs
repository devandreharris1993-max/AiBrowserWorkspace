using Avalonia;
using Avalonia.Media;
using AiBrowserWorkspace.Models;
using AiBrowserWorkspace.Services;
using WebViewControl;

namespace AiBrowserWorkspace.ViewModels;

public sealed class BrowserSessionViewModel : ViewModelBase
{
    private static readonly IBrush ChatGptAccentBrush = Solid(0x10, 0xA3, 0x7F);
    private static readonly IBrush ClaudeAccentBrush = Solid(0xCC, 0x78, 0x5C);
    private static readonly IBrush CleverAiHumanizerAccentBrush = Solid(0x3D, 0x8B, 0xF0);
    private static readonly IBrush ReadyBrush = Solid(0x4C, 0xAF, 0x8C);
    private static readonly IBrush BusyBrush = Solid(0xE8, 0xA3, 0x3D);
    private static readonly IBrush ErrorBrush = Solid(0xE0, 0x5A, 0x5A);
    private static readonly IBrush NeutralBorderBrush = Solid(0x2A, 0x2E, 0x37);

    private readonly IAiSiteAdapterFactory _adapterFactory;

    private bool _isSelected;
    private bool _includeInBroadcast = true;
    private SessionStatus _status = SessionStatus.Initializing;
    private string? _errorMessage;
    private WebView? _browserControl;

    public BrowserSessionViewModel(AiBrowserSession session, IAiSiteAdapterFactory adapterFactory)
    {
        Session = session;
        _adapterFactory = adapterFactory;
    }

    public AiBrowserSession Session { get; }

    public Guid Id => Session.Id;
    public string DisplayName => Session.DisplayName;
    public AiPlatform Platform => Session.Platform;
    public string PlatformDisplayName => Platform.GetDisplayName();
    public Uri HomeUri => Platform.GetHomeUri();
    public ProxyEndpoint? Proxy => Session.Proxy;
    public string ProxyDisplayText => Proxy is null ? "Direct (no proxy)" : $"via {Proxy}";

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
        set => SetField(ref _browserControl, value);
    }

    public bool HasError => Status == SessionStatus.Error && !string.IsNullOrEmpty(ErrorMessage);
    public bool IsLoadingOverlayVisible => Status is SessionStatus.Initializing or SessionStatus.Loading;

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

    public IBrush AccentBrush => Platform switch
    {
        AiPlatform.ChatGpt => ChatGptAccentBrush,
        AiPlatform.Claude => ClaudeAccentBrush,
        AiPlatform.CleverAiHumanizer => CleverAiHumanizerAccentBrush,
        _ => NeutralBorderBrush
    };

    public IBrush CardBorderBrush => IsSelected ? AccentBrush : NeutralBorderBrush;
    public Thickness CardBorderThickness => IsSelected ? new Thickness(2) : new Thickness(1);

    public Task<bool> SendBroadcastPromptAsync(string prompt, CancellationToken cancellationToken = default)
    {
        if (_browserControl is null)
        {
            Status = SessionStatus.Error;
            ErrorMessage = "The browser for this session is not ready yet.";
            return Task.FromResult(false);
        }

        if (Status == SessionStatus.Sending)
        {
            return Task.FromResult(false);
        }

        return SendBroadcastCoreAsync(prompt, cancellationToken);
    }

    private async Task<bool> SendBroadcastCoreAsync(string prompt, CancellationToken cancellationToken)
    {
        Status = SessionStatus.Sending;
        ErrorMessage = null;

        try
        {
            var adapter = _adapterFactory.GetAdapter(Platform);
            await adapter.SendPromptAsync(_browserControl!, prompt, cancellationToken);
            Status = SessionStatus.Sent;
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

    private static IBrush Solid(byte r, byte g, byte b) => new SolidColorBrush(Color.FromRgb(r, g, b));
}
