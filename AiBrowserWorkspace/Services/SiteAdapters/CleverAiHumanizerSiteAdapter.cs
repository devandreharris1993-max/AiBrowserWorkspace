using System.Text.Json;
using WebViewControl;

namespace AiBrowserWorkspace.Services.SiteAdapters;

// Fills the humanizer textarea on https://cleverhumanizer.ai/ and clicks Humanize AI.
public sealed class CleverAiHumanizerSiteAdapter : IAiSiteAdapter
{
    private static readonly TimeSpan ComposerWaitTimeout = TimeSpan.FromSeconds(12);

    private const string ComposerProbeScript = @"
        (function () {
            var input = document.querySelector('textarea')
                || document.querySelector('[placeholder*=""Paste your text""]')
                || document.querySelector('[contenteditable=""true""]');
            return !!input;
        })();";

    private const string SendScriptTemplate = @"
        (function () {
            function findComposer() {
                return document.querySelector('textarea')
                    || document.querySelector('[placeholder*=""Paste your text""]')
                    || document.querySelector('[contenteditable=""true""]');
            }
            function findSubmitButton() {
                return Array.prototype.find.call(document.querySelectorAll('button'), function (b) {
                    var label = (b.getAttribute('aria-label') || '') + ' ' + (b.textContent || '');
                    return /humanize/i.test(label) && !b.disabled;
                });
            }

            var composer = findComposer();
            if (!composer) {
                return JSON.stringify({ ok: false, reason: 'composer-not-found' });
            }

            composer.focus();
            if (composer.tagName === 'TEXTAREA' || composer.tagName === 'INPUT') {
                var proto = composer.tagName === 'TEXTAREA'
                    ? window.HTMLTextAreaElement.prototype
                    : window.HTMLInputElement.prototype;
                var setter = Object.getOwnPropertyDescriptor(proto, 'value').set;
                setter.call(composer, __PROMPT_JSON__);
                composer.dispatchEvent(new Event('input', { bubbles: true }));
                composer.dispatchEvent(new Event('change', { bubbles: true }));
            } else {
                document.execCommand('selectAll', false, null);
                document.execCommand('insertText', false, __PROMPT_JSON__);
            }

            var submit = findSubmitButton();
            if (submit && !submit.disabled) {
                submit.click();
                return JSON.stringify({ ok: true, method: 'click' });
            }

            return JSON.stringify({ ok: false, reason: 'submit-not-found' });
        })();";

    public async Task SendPromptAsync(WebView webView, string prompt, CancellationToken cancellationToken = default)
    {
        if (!webView.IsBrowserInitialized)
        {
            throw new InvalidOperationException("The Clever AI Humanizer browser has not finished initializing yet.");
        }

        await PromptInjection.WaitForComposerAsync(
            webView, ComposerProbeScript, ComposerWaitTimeout, "Clever AI Humanizer", cancellationToken);

        var script = SendScriptTemplate.Replace("__PROMPT_JSON__", JsonSerializer.Serialize(prompt));

        string resultJson;
        try
        {
            resultJson = await webView.EvaluateScript<string>(script);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Could not send the prompt to Clever AI Humanizer.", ex);
        }

        PromptInjection.EnsureSucceeded(resultJson, "Clever AI Humanizer");
    }
}
