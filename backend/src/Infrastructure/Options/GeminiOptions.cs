namespace RecruitOps.Infrastructure.Options;

public class GeminiOptions
{
    public const string SectionName = "AI:Gemini";
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gemini-1.5-pro";
    public int TimeoutSeconds { get; set; } = 30;
    public string ApiUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta/models";

    /// <summary>
    /// Returns canned sample documents instead of calling Gemini when no <see cref="ApiKey"/> is set.
    /// **Local development only** — see <see cref="ClaudeOptions.EnableFallback"/>; the same rule and
    /// the same reasoning apply. Defaults to <c>false</c>.
    /// </summary>
    public bool EnableFallback { get; set; } = false;
}
