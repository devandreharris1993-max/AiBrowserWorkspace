using AiBrowserWorkspace.Models;
using AiBrowserWorkspace.Services.SiteAdapters;

namespace AiBrowserWorkspace.Services;

public interface IAiSiteAdapterFactory
{
    IAiSiteAdapter GetAdapter(AiPlatform platform);
}

public sealed class AiSiteAdapterFactory : IAiSiteAdapterFactory
{
    private readonly Dictionary<AiPlatform, IAiSiteAdapter> _adapters = new()
    {
        [AiPlatform.ChatGpt] = new ChatGptSiteAdapter(),
        [AiPlatform.Claude] = new ClaudeSiteAdapter(),
        [AiPlatform.CleverAiHumanizer] = new CleverAiHumanizerSiteAdapter()
    };

    public IAiSiteAdapter GetAdapter(AiPlatform platform)
    {
        if (_adapters.TryGetValue(platform, out var adapter))
        {
            return adapter;
        }

        throw new NotSupportedException($"No site adapter is registered for platform '{platform}'.");
    }
}
