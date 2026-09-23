using System.Text.Json;
using WebViewControl;

namespace AiBrowserWorkspace.Services.SiteAdapters;

// Milestone 2: types the prompt into ChatGPT's composer and submits it.
// Selectors are best-effort with fallbacks since chatgpt.com's DOM can change without notice.
public sealed class ChatGptSiteAdapter : IAiSiteAdapter
{
    private static readonly TimeSpan ComposerWaitTimeout = TimeSpan.FromSeconds(12);

    private const string ComposerProbeScript = @"
        (function () {
            var composer = document.querySelector('#prompt-textarea')
                || document.querySelector('div[contenteditable=""true""][data-testid=""prompt-textarea""]')
                || document.querySelector('form div[contenteditable=""true""]');
            return !!composer;
        })();";

    private const string SendScriptTemplate = @"
        (function () {
            function findComposer() {
                return document.querySelector('#prompt-textarea')
                    || document.querySelector('div[contenteditable=""true""][data-testid=""prompt-textarea""]')
                    || document.querySelector('form div[contenteditable=""true""]');
            }
            function findSendButton() {
                return document.querySelector('button[data-testid=""send-button""]')
                    || document.querySelector('#composer-submit-button')
                    || Array.prototype.find.call(document.querySelectorAll('button'), function (b) {
                        var label = (b.getAttribute('aria-label') || '') + ' ' + (b.textContent || '');
                        return /send/i.test(label) && !b.disabled;
                    });
            }

            var composer = findComposer();
            if (!composer) {
                return JSON.stringify({ ok: false, reason: 'composer-not-found' });
            }

            composer.focus();
            document.execCommand('selectAll', false, null);
            document.execCommand('insertText', false, __PROMPT_JSON__);

            var sendButton = findSendButton();
            if (sendButton && !sendButton.disabled) {
                sendButton.click();
                return JSON.stringify({ ok: true, method: 'click' });
            }

            var enterOptions = { key: 'Enter', code: 'Enter', keyCode: 13, which: 13, bubbles: true, cancelable: true };
            composer.dispatchEvent(new KeyboardEvent('keydown', enterOptions));
            composer.dispatchEvent(new KeyboardEvent('keypress', enterOptions));
            composer.dispatchEvent(new KeyboardEvent('keyup', enterOptions));
            return JSON.stringify({ ok: true, method: 'enter-key' });
        })();";

    public async Task SendPromptAsync(WebView webView, string prompt, CancellationToken cancellationToken = default)
    {
        if (!webView.IsBrowserInitialized)
        {
            throw new InvalidOperationException("The ChatGPT browser has not finished initializing yet.");
        }

        await PromptInjection.WaitForComposerAsync(webView, ComposerProbeScript, ComposerWaitTimeout, "ChatGPT", cancellationToken);

        var script = SendScriptTemplate.Replace("__PROMPT_JSON__", JsonSerializer.Serialize(prompt));

        string resultJson;
        try
        {
            resultJson = await webView.EvaluateScript<string>(script);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Could not send the prompt to ChatGPT.", ex);
        }

        PromptInjection.EnsureSucceeded(resultJson, "ChatGPT");
    }
}
