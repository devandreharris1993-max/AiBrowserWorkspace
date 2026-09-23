using System.Linq;
using AiBrowserWorkspace.Models;
using WebViewControl;

namespace AiBrowserWorkspace.Services;

public sealed record ProxyActivationResult(bool Succeeded, string? ErrorMessage)
{
    public static ProxyActivationResult Success { get; } = new(true, null);
    public static ProxyActivationResult Failure(string message) => new(false, message);
}

public interface IWebProxyService
{
    // The proxy in effect for every session's browser, or null if none has been set.
    ProxyEndpoint? ActiveProxy { get; }

    // Whether a proxy can still be configured. Only true before the first session's
    // browser has started — see TryActivate.
    bool CanConfigureProxy { get; }

    // Attempts to set the process-wide proxy. Fails once a browser has already
    // started, or if a different proxy is already active.
    ProxyActivationResult TryActivate(ProxyEndpoint proxy);

    // Marks that a session's browser is about to start, permanently closing the
    // window for configuring (or changing) the proxy for the rest of this app run.
    void NotifySessionStarting();
}

// The embedded browser engine (CEF, behind every session's WebView) is a single
// process shared by every session — see App.axaml.cs's CachePath comment — and it
// reads its proxy setting once, from the command line, the moment its first browser
// instance starts. There is no per-browser proxy in the WebViewControl/CefGlue API
// this app uses (only per-session proxy *authentication* credentials, which is a
// different thing — see WebView.ProxyAuthentication). So in practice a proxy can only
// ever be set by the very first session added in an app run, and from that point on
// every session — including ones added afterward with no proxy requested — goes
// through it, or through none, for the rest of the run.
public sealed class WebProxyService : IWebProxyService
{
    private bool _hasSessionStarted;

    public ProxyEndpoint? ActiveProxy { get; private set; }

    public bool CanConfigureProxy => !_hasSessionStarted;

    public ProxyActivationResult TryActivate(ProxyEndpoint proxy)
    {
        if (_hasSessionStarted)
        {
            return ProxyActivationResult.Failure(
                ActiveProxy is null
                    ? "A browser has already started without a proxy, so none can be added now without restarting the app."
                    : $"A browser has already started using {ActiveProxy}, so the proxy can't be changed without restarting the app.");
        }

        ActiveProxy = proxy;
        WebView.Settings.AddCommandLineSwitch("proxy-server", proxy.ToString());

        // Chromium's WebRTC engine does its own STUN/ICE UDP probing straight to the
        // network, bypassing proxy-server entirely by default — a well-known "WebRTC
        // leak" that most IP-check sites specifically use to reveal the real address
        // even behind a proxy/VPN. This forces WebRTC to respect the proxy too, so an
        // IP check made *inside* a session reflects the proxy instead of leaking the
        // real one. It doesn't affect whether ordinary page traffic is proxied — that
        // was already handled by proxy-server above.
        WebView.Settings.AddCommandLineSwitch("force-webrtc-ip-handling-policy", "disable_non_proxied_udp");

        return ProxyActivationResult.Success;
    }

    public void NotifySessionStarting()
    {
        if (_hasSessionStarted)
        {
            return;
        }

        _hasSessionStarted = true;

        // Diagnostic: the embedded browser engine snapshots these switches into its
        // native startup command line the instant its first browser object is
        // constructed, which happens right after this call returns (see
        // BrowserSessionService.CreateSession -> MainViewModel.Sessions.Add ->
        // BrowserCard's WebView). If "proxy-server" isn't in this list, the proxy was
        // requested too late (or never) and every session will go direct — run the
        // app from a terminal to see this line and check.
        var switches = string.Join(", ", WebView.Settings.CommandLineSwitches.Select(kv =>
            kv.Value is null ? $"--{kv.Key}" : $"--{kv.Key}={kv.Value}"));
        Console.Error.WriteLine($"[WebProxyService] First browser starting; command-line switches: {switches}");
    }
}
