using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using AiBrowserWorkspace.Models;
using AiBrowserWorkspace.ViewModels;

namespace AiBrowserWorkspace.Views;

public partial class BrowserCard : UserControl
{
    private bool _initialized;
    private BrowserSessionViewModel? _viewModel;

    public BrowserCard()
    {
        InitializeComponent();
    }

    private void CloseButton_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Stop the click from also bubbling into ListBoxItem's click-to-select handling.
        e.Handled = true;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (_initialized || DataContext is not BrowserSessionViewModel vm)
        {
            return;
        }

        _initialized = true;
        _viewModel = vm;

        WebViewControl.BeforeNavigate += OnBeforeNavigate;
        WebViewControl.Navigated += OnNavigated;
        WebViewControl.LoadFailed += OnLoadFailed;
        WebViewControl.WebViewInitialized += OnWebViewInitialized;

        // This app's one CEF engine has at most one proxy active for the whole run
        // (see IWebProxyService), but its auth challenge is still per-browser, so
        // every session's own WebView needs its own copy of the credentials.
        if (vm.Proxy is { UserName: not null } proxy)
        {
            WebViewControl.ProxyAuthentication = new WebViewControl.ProxyAuthentication
            {
                UserName = proxy.UserName,
                Password = proxy.Password
            };
        }

        vm.BrowserControl = WebViewControl;

        if (WebViewControl.IsBrowserInitialized)
        {
            NavigateHome();
        }
    }

    private void OnWebViewInitialized() => Dispatcher.UIThread.Post(NavigateHome);

    private void NavigateHome()
    {
        if (_viewModel is null)
        {
            return;
        }

        Navigate(_viewModel.HomeUri.ToString());
    }

    private void Navigate(string url)
    {
        if (_viewModel is null)
        {
            return;
        }

        try
        {
            AddressBar.Text = url;
            WebViewControl.LoadUrl(url);
        }
        catch (Exception ex)
        {
            _viewModel.Status = SessionStatus.Error;
            _viewModel.ErrorMessage = $"Failed to start the embedded browser: {ex.Message}";
        }
    }

    private void AddressBar_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        var text = AddressBar.Text?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        Navigate(text.Contains("://") ? text : $"https://{text}");
        e.Handled = true;
    }

    private void BackButton_Click(object? sender, RoutedEventArgs e) => WebViewControl.GoBack();

    private void ForwardButton_Click(object? sender, RoutedEventArgs e) => WebViewControl.GoForward();

    private void ReloadButton_Click(object? sender, RoutedEventArgs e) => WebViewControl.Reload();

    // WebViewControl (CEF) raises these on its own engine thread, not the UI thread,
    // so every handler below marshals back via Dispatcher.UIThread before touching the VM.
    private void OnBeforeNavigate(WebViewControl.Request request) => Dispatcher.UIThread.Post(() =>
    {
        if (_viewModel is not null)
        {
            _viewModel.Status = SessionStatus.Loading;
        }
    });

    private void OnNavigated(string url, string frameName) => Dispatcher.UIThread.Post(() =>
    {
        if (_viewModel is null)
        {
            return;
        }

        _viewModel.Status = SessionStatus.Ready;
        _viewModel.ErrorMessage = null;

        // frameName is null for the main frame; ignore iframe/sub-resource navigations
        // so the address bar always reflects the page the user is actually looking at.
        if (frameName is null)
        {
            AddressBar.Text = url;
        }
    });

    private void OnLoadFailed(string url, int errorCode, string frameName) => Dispatcher.UIThread.Post(() =>
    {
        if (_viewModel is null)
        {
            return;
        }

        _viewModel.Status = SessionStatus.Error;
        _viewModel.ErrorMessage = $"The page failed to load (error {errorCode}).";
    });
}
