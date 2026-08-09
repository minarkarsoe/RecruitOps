namespace RecruitOps.Infrastructure.Options;

public class GeminiOptions
{
    public const string SectionName = "AI:Gemini";
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gemini-1.5-pro";
    public int TimeoutSeconds { get; set; } = 30;
    public string ApiUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta/models";
}
