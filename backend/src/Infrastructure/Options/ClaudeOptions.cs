namespace RecruitOps.Infrastructure.Options;

public class ClaudeOptions
{
    public const string SectionName = "AI:Claude";
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "claude-3-5-sonnet-20241022";
    public int MaxTokens { get; set; } = 4096;
    public int TimeoutSeconds { get; set; } = 30;
    public string ApiUrl { get; set; } = "https://api.anthropic.com/v1/messages";
}
