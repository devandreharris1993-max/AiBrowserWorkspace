using System.Collections.Specialized;
using WebViewControl;

namespace AiBrowserWorkspace.Services;

// Google refuses interactive sign-in from CEF / Chromium Embedded Framework
// ("This browser or app may not be secure."). Their check keys off a Chromium
// User-Agent plus Client Hints on accounts.google.com. Presenting a non-Chromium
// UA for those requests is the workaround CEF apps use so ChatGPT / Claude
// Google OAuth can complete. Other hosts keep the real CEF Chrome UA.
internal static class GoogleSignInCompatibility
{
    private const string FirefoxUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:133.0) Gecko/20100101 Firefox/133.0";

    private static readonly string[] ClientHintHeaders =
    [
        "Sec-CH-UA",
        "Sec-CH-UA-Mobile",
        "Sec-CH-UA-Platform",
        "Sec-CH-UA-Platform-Version",
        "Sec-CH-UA-Arch",
        "Sec-CH-UA-Bitness",
        "Sec-CH-UA-Model",
        "Sec-CH-UA-Full-Version",
        "Sec-CH-UA-Full-Version-List",
        "Sec-CH-UA-Wow64",
        "Sec-CH-UA-Form-Factors"
    ];

    public static void Apply(Request request)
    {
        if (!IsGoogleAccountUrl(request.Url))
        {
            return;
        }

        var headers = request.GetHeaderMap() ?? new NameValueCollection();
        headers.Set("User-Agent", FirefoxUserAgent);
        foreach (var header in ClientHintHeaders)
        {
            headers.Remove(header);
        }

        request.SetHeaderMap(headers);
        request.SetHeaderByName("User-Agent", FirefoxUserAgent, overwrite: true);
    }

    private static bool IsGoogleAccountUrl(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var host = uri.Host;
        return host.Equals("accounts.google.com", StringComparison.OrdinalIgnoreCase)
               || host.Equals("accounts.youtube.com", StringComparison.OrdinalIgnoreCase)
               || host.EndsWith(".accounts.google.com", StringComparison.OrdinalIgnoreCase);
    }
}
