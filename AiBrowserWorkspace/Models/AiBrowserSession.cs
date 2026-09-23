namespace AiBrowserWorkspace.Models;

public sealed class AiBrowserSession
{
    public AiBrowserSession(Guid id, string displayName, AiPlatform platform, ProxyEndpoint? proxy)
    {
        Id = id;
        DisplayName = displayName;
        Platform = platform;
        Proxy = proxy;
    }

    public Guid Id { get; }
    public string DisplayName { get; }
    public AiPlatform Platform { get; }

    // The proxy in effect when this session was created (null if none was active yet).
    // Since the proxy is process-wide (see IWebProxyService), every session created
    // afterward shares this same value regardless of what it requests.
    public ProxyEndpoint? Proxy { get; }

    public DateTime CreatedAt { get; } = DateTime.Now;
}
