using WebViewControl;

namespace AiBrowserWorkspace.Services.SiteAdapters;

public interface IAiSiteAdapter
{
    Task SendPromptAsync(WebView webView, string prompt, CancellationToken cancellationToken = default);
}
