namespace RecruitOps.Infrastructure.Options;

public class ClaudeOptions
{
    public const string SectionName = "AI:Claude";
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "claude-3-5-sonnet-20241022";
    public int MaxTokens { get; set; } = 4096;
    public int TimeoutSeconds { get; set; } = 30;
    public string ApiUrl { get; set; } = "https://api.anthropic.com/v1/messages";

    /// <summary>
    /// Returns canned sample analyses instead of calling Claude when no <see cref="ApiKey"/> is set.
    /// **Local development only** — the stubs are fabricated candidate profiles and match scores,
    /// and a recruiter cannot tell them apart from a real answer. Defaults to <c>false</c> so an
    /// install with no key returns 402 rather than inventing a candidate. Where it is switched on,
    /// every response carries <c>X-Ai-Simulated: true</c>.
    /// It never applies once a key is present: a provider that errors returns 502, never a stub.
    /// </summary>
    public bool EnableFallback { get; set; } = false;
}
