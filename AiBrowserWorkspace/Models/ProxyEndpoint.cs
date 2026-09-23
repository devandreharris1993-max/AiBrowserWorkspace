namespace AiBrowserWorkspace.Models;

// An optional upstream proxy for the embedded browser engine. See IWebProxyService for
// why this can only be set once, by the first session added in an app run.
// UserName/Password are optional — only some proxies require them (see
// WebView.ProxyAuthentication, applied per-session in BrowserCard).
public sealed record ProxyEndpoint(string Host, int Port, string? UserName = null, string? Password = null)
{
    // Deliberately excludes UserName/Password — this is used in logs and on-screen
    // status text (MainWindow's top bar, the Add Browser dialog), which shouldn't leak
    // credentials.
    public override string ToString() => $"{Host}:{Port}";
}
