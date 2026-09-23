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

        // Same VMware SVGA3D driver that forced software rendering for Avalonia itself
        // (see Program.cs) also breaks CEF's own internal GPU/compositor process, which
        // otherwise renders blank/black tiles instead of page content. Disabling GPU use
        // inside CEF too mirrors that fix and lets it fall back to software compositing.
        WebView.Settings.AddCommandLineSwitch("disable-gpu", "1");
        WebView.Settings.AddCommandLineSwitch("disable-gpu-compositing", "1");
        WebView.Settings.AddCommandLineSwitch("no-sandbox", "1");

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
