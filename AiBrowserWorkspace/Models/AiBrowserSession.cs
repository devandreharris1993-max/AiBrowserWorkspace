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

    // Proxy requested for this session. The CEF engine is process-wide, so the last
    // applied proxy is what page traffic actually uses (see CefProxyApplicator).
    public ProxyEndpoint? Proxy { get; }

    public DateTime CreatedAt { get; } = DateTime.Now;
}
