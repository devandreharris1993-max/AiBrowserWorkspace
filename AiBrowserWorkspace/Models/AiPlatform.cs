namespace AiBrowserWorkspace.Models;

public enum AiPlatform
{
    ChatGpt,
    Claude,
    CleverAiHumanizer
}

public static class AiPlatformExtensions
{
    public static string GetDisplayName(this AiPlatform platform) => platform switch
    {
        AiPlatform.ChatGpt => "ChatGPT",
        AiPlatform.Claude => "Claude",
        AiPlatform.CleverAiHumanizer => "Clever AI Humanizer",
        _ => platform.ToString()
    };

    public static Uri GetHomeUri(this AiPlatform platform) => platform switch
    {
        AiPlatform.ChatGpt => new Uri("https://chatgpt.com/"),
        AiPlatform.Claude => new Uri("https://claude.ai/"),
        AiPlatform.CleverAiHumanizer => new Uri("https://cleverhumanizer.ai/"),
        _ => throw new ArgumentOutOfRangeException(nameof(platform), platform, null)
    };
}
