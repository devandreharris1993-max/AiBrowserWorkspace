using AiBrowserWorkspace.Models;

namespace AiBrowserWorkspace.ViewModels;

public sealed class AddBrowserViewModel : ViewModelBase
{
    private AiPlatform _selectedPlatform = AiPlatform.ChatGpt;
    private string _sessionName = string.Empty;
    private string _proxyHost = string.Empty;
    private string _proxyPortText = string.Empty;
    private string _proxyUserName = string.Empty;
    private string _proxyPassword = string.Empty;
    private string? _validationError;

    public AiPlatform SelectedPlatform
    {
        get => _selectedPlatform;
        set => SetField(ref _selectedPlatform, value);
    }

    public string SessionName
    {
        get => _sessionName;
        set => SetField(ref _sessionName, value);
    }

    // Every new browser gets its own OS process with its own CEF engine (see
    // Services/BrowserSessionService.cs), so — unlike the old single-shared-engine
    // design — a proxy here never affects any other session, and there's nothing to
    // lock: this is always available.
    public string ProxyStatusText => "Optional. Only affects this browser.";

    public string ProxyHost
    {
        get => _proxyHost;
        set => SetField(ref _proxyHost, value);
    }

    public string ProxyPortText
    {
        get => _proxyPortText;
        set => SetField(ref _proxyPortText, value);
    }

    // Optional — only some proxies require a login (see WebView.ProxyAuthentication).
    public string ProxyUserName
    {
        get => _proxyUserName;
        set => SetField(ref _proxyUserName, value);
    }

    public string ProxyPassword
    {
        get => _proxyPassword;
        set => SetField(ref _proxyPassword, value);
    }

    public string? ValidationError
    {
        get => _validationError;
        private set => SetField(ref _validationError, value);
    }

    // The proxy to request, resolved by the last successful Validate() call.
    public ProxyEndpoint? ResolvedProxy { get; private set; }

    // Validates the form and resolves ResolvedProxy. Returns false (with
    // ValidationError set) if the dialog shouldn't close yet.
    public bool Validate()
    {
        ResolvedProxy = null;
        ValidationError = null;

        var host = ProxyHost.Trim();
        var portText = ProxyPortText.Trim();

        if (host.Length == 0 && portText.Length == 0)
        {
            return true;
        }

        if (host.Length == 0 || portText.Length == 0)
        {
            ValidationError = "Enter both a proxy host and a port, or leave both blank.";
            return false;
        }

        if (!int.TryParse(portText, out var port) || port is < 1 or > 65535)
        {
            ValidationError = "Port must be a number between 1 and 65535.";
            return false;
        }

        var userName = ProxyUserName.Trim();
        var password = ProxyPassword.Trim();

        if (userName.Length == 0 ^ password.Length == 0)
        {
            ValidationError = "Enter both a username and a password for the proxy, or leave both blank.";
            return false;
        }

        ResolvedProxy = new ProxyEndpoint(
            host,
            port,
            userName.Length == 0 ? null : userName,
            password.Length == 0 ? null : password);
        return true;
    }
}
