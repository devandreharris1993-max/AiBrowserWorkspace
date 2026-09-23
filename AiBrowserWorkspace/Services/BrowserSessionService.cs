using AiBrowserWorkspace.Models;
using AiBrowserWorkspace.ViewModels;

namespace AiBrowserWorkspace.Services;

public interface IBrowserSessionService
{
    BrowserSessionViewModel CreateSession(AiPlatform platform, string? requestedName, ProxyEndpoint? proxy);
}

// Every session's browser lives in this same process, sharing one CEF engine — see
// IWebProxyService for why that means only the very first session added can set a
// proxy for the whole app run.
public sealed class BrowserSessionService : IBrowserSessionService
{
    private readonly IWebProxyService _proxyService;
    private readonly IAiSiteAdapterFactory _adapterFactory;
    private readonly Dictionary<AiPlatform, int> _sessionCounters = new();

    public BrowserSessionService(IWebProxyService proxyService, IAiSiteAdapterFactory adapterFactory)
    {
        _proxyService = proxyService;
        _adapterFactory = adapterFactory;
    }

    public BrowserSessionViewModel CreateSession(AiPlatform platform, string? requestedName, ProxyEndpoint? proxy)
    {
        var displayName = string.IsNullOrWhiteSpace(requestedName)
            ? GenerateDefaultName(platform)
            : requestedName.Trim();

        ProxyActivationResult? proxyResult = null;
        if (proxy is not null)
        {
            proxyResult = _proxyService.TryActivate(proxy);
        }

        _proxyService.NotifySessionStarting();

        var session = new AiBrowserSession(Guid.NewGuid(), displayName, platform, proxy ?? _proxyService.ActiveProxy);
        var viewModel = new BrowserSessionViewModel(session, _adapterFactory);

        if (proxyResult is { Succeeded: false })
        {
            viewModel.Status = SessionStatus.Error;
            viewModel.ErrorMessage = proxyResult.ErrorMessage;
        }

        return viewModel;
    }

    private string GenerateDefaultName(AiPlatform platform)
    {
        _sessionCounters.TryGetValue(platform, out var count);
        count++;
        _sessionCounters[platform] = count;
        return $"{platform.GetDisplayName()} Session {count}";
    }
}
