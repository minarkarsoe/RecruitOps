namespace RecruitOps.Application.DTOs;

/// <summary>
/// Structured contact and profile information parsed via heuristics/regex from extracted CV text.
/// </summary>
public record ParsedContactInfoDto(
    string? CandidateName,
    string? Email,
    string? Phone,
    int? YearsOfExperience,
    List<string> Skills
);

/// <summary>
/// Response DTO returned after processing a candidate resume upload.
/// </summary>
public record ResumeExtractionResultDto(
    Guid ApplicationId,
    string FileKey,
    string FileName,
    long FileSizeBytes,
    string ExtractedText,
    string OriginalText,
    string DetectedLanguage,
    bool IsZawgyiNormalized,
    ParsedContactInfoDto ParsedContactInfo,
    DateTimeOffset ProcessedAt
);
