using System.Text.Json;
using WebViewControl;

namespace AiBrowserWorkspace.Services.SiteAdapters;

// Shared plumbing for site adapters that drive a page's DOM: polling for the
// composer to exist, and interpreting the JSON result a "fill and submit" script reports back.
internal static class PromptInjection
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(300);

    private static readonly JsonSerializerOptions ResultOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task WaitForComposerAsync(
        WebView webView,
        string probeScript,
        TimeSpan timeout,
        string platformLabel,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            bool found;
            try
            {
                found = await webView.EvaluateScript<bool>(probeScript);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Could not reach the {platformLabel} page. Make sure it has finished loading.", ex);
            }

            if (found)
            {
                return;
            }

            if (DateTime.UtcNow >= deadline)
            {
                throw new InvalidOperationException(
                    $"Could not find the {platformLabel} prompt box. Make sure you're signed in and on the chat page, then try again.");
            }

            await Task.Delay(PollInterval, cancellationToken);
        }
    }

    public static void EnsureSucceeded(string? resultJson, string platformLabel)
    {
        if (string.IsNullOrWhiteSpace(resultJson))
        {
            throw new InvalidOperationException($"Sending the prompt to {platformLabel} did not return a result.");
        }

        SendResult? result;
        try
        {
            result = JsonSerializer.Deserialize<SendResult>(resultJson, ResultOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Sending the prompt to {platformLabel} returned an unexpected result.", ex);
        }

        if (result is { Ok: true })
        {
            return;
        }

        var reason = result?.Reason switch
        {
            "composer-not-found" => "the prompt box could not be found on the page",
            "send-button-not-found" => "the send button could not be found",
            _ => result?.Reason ?? "an unknown error occurred"
        };

        throw new InvalidOperationException($"Could not send the prompt to {platformLabel} because {reason}.");
    }

    private sealed class SendResult
    {
        public bool Ok { get; set; }
        public string? Reason { get; set; }
        public string? Method { get; set; }
    }
}
