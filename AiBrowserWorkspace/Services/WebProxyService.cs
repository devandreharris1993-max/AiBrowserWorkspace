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
    ProxyEndpoint? ActiveProxy { get; }

    bool CanConfigureProxy { get; }

    ProxyActivationResult TryActivate(ProxyEndpoint proxy);

    void NotifySessionStarting();
}

// Every session shares one CEF engine. It snapshots --proxy-server the moment
// the first browser object is constructed, so a proxy can only be set by the
// first session in a run. The value must be a URI (http://host:port); a bare
// host:port is ignored by this CEF build.
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
        WebView.Settings.AddCommandLineSwitch("proxy-server", proxy.ToProxyServerUri());
        WebView.Settings.AddCommandLineSwitch("proxy-bypass-list", "<-loopback>");

        // Chromium's WebRTC engine does its own STUN/ICE UDP probing straight to the
        // network, bypassing proxy-server entirely by default. This forces WebRTC
        // to respect the proxy too, so an IP check inside a session reflects it.
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

        var switches = string.Join(", ", WebView.Settings.CommandLineSwitches.Select(kv =>
            kv.Value is null ? $"--{kv.Key}" : $"--{kv.Key}={kv.Value}"));
        Console.Error.WriteLine($"[WebProxyService] First browser starting; command-line switches: {switches}");
    }
}
