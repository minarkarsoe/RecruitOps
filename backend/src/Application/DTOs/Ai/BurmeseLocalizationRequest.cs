namespace RecruitOps.Application.DTOs.Ai;

public record BurmeseLocalizationRequest(
    string SourceText,
    string TargetLanguage, // "my" (Burmese) | "en" (English)
    string? Context // "ResumeNote" | "ScorecardComment" | "JobDescription"
);

public record BurmeseLocalizationResultDto(
    string OriginalText,
    string TranslatedText,
    string SourceLanguage,
    string TargetLanguage
);
