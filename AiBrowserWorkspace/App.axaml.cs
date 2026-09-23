using System;
using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using AiBrowserWorkspace.Services;
using AiBrowserWorkspace.ViewModels;
using AiBrowserWorkspace.Views;
using Microsoft.Extensions.DependencyInjection;
using WebViewControl;

namespace AiBrowserWorkspace;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    public static IServiceProvider Services { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            InitializeOrchestrator(desktop);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void InitializeOrchestrator(IClassicDesktopStyleApplicationLifetime desktop)
    {
        // This must happen before any session constructs its WebView — see
        // IWebProxyService (the proxy, if any, can only be set before the first
        // browser starts) and CachePath/PersistCache below.
        var cachePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AiBrowserWorkspace", "WebViewCache");

        WebView.Settings.CachePath = cachePath;
        WebView.Settings.PersistCache = true;

        // Boolean Chromium switches must be flag-only (--disable-gpu), not --disable-gpu=1:
        // the latter is ignored, the GPU process still launches, fails, and CEF aborts
        // with "GPU process isn't usable. Goodbye."
        //
        // CEF's GPU work has to stay in this process: the helper
        // (Xilium.CefGlue.BrowserProcess.exe) ships without a supportedOS
        // manifest, so a separate GPU process can't create its windows and
        // Chromium kills the whole app. --disable-gpu-compositing is intentionally
        // omitted — that flag produces a permanent white page.
        WebView.Settings.AddCommandLineSwitch("in-process-gpu", null);
        WebView.Settings.AddCommandLineSwitch("disable-gpu-sandbox", null);
        WebView.Settings.AddCommandLineSwitch("ignore-gpu-blocklist", null);
        WebView.Settings.AddCommandLineSwitch("use-angle", "swiftshader");
        WebView.Settings.AddCommandLineSwitch("lang", "en-US");
        // Stops Chromium advertising navigator.webdriver / automation bits that
        // Google's sign-in page treats as an insecure embedded browser.
        WebView.Settings.AddCommandLineSwitch("disable-blink-features", "AutomationControlled");

        if (OperatingSystem.IsLinux())
        {
            // Same VMware SVGA3D driver that forced software rendering for Avalonia
            // itself (see Program.cs) also breaks CEF's hardware GL init.
            WebView.Settings.AddCommandLineSwitch("disable-gpu", null);
        }

        WebView.Settings.AddCommandLineSwitch("no-sandbox", null);
        // Out-of-process network/utility helpers crash-loop on this host
        // ("Network service crashed, restarting service"), so pages never
        // leave "Loading…". Single-process keeps I/O in this manifested exe.
        WebView.Settings.AddCommandLineSwitch("single-process", null);

        var services = new ServiceCollection();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IWebProxyService, WebProxyService>();
        services.AddSingleton<IAiSiteAdapterFactory, AiSiteAdapterFactory>();
        services.AddSingleton<IBrowserSessionService, BrowserSessionService>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();

        _serviceProvider = services.BuildServiceProvider();
        Services = _serviceProvider;

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        desktop.MainWindow = mainWindow;

        desktop.ShutdownRequested += (_, _) => _serviceProvider?.Dispose();
    }
}
