namespace RecruitOps.Application.DTOs.Ai;

public record ParseResumeRequest(
    string ResumeText,
    string? FileName = null
);

public record ParsedResumeResultDto(
    string FullName,
    string? Email,
    string? Phone,
    string Summary,
    List<WorkExperienceDto> WorkExperiences,
    List<EducationDto> Educations,
    List<string> Skills,
    List<string> Languages,
    int EstimatedYearsOfExperience
);

public record WorkExperienceDto(
    string Company,
    string Position,
    string StartDate,
    string EndDate,
    string Description,
    List<string> Highlights
);

public record EducationDto(
    string Institution,
    string Degree,
    string FieldOfStudy,
    string StartDate,
    string EndDate
);

public record MatchCandidateRequest(
    Guid CandidateId,
    Guid JobPostingId
);

public record CandidateMatchAnalysisDto(
    int MatchScore, // 0 to 100
    string OverallVerdict, // e.g. "Strong Fit", "Moderate Fit", "Gap Identified"
    List<string> MatchedSkills,
    List<string> MissingSkills,
    List<string> Strengths,
    List<string> Concerns,
    string Recommendation
);

public record GenerateExecutiveSummaryRequest(
    Guid CandidateId,
    Guid? JobPostingId = null,
    string? Tone = null
);

public record ExecutiveSummaryDto(
    string Headline,
    string ExecutiveSummary,
    List<string> KeyHighlights,
    List<string> RecommendedInterviewQuestions
);

public record PrepareDocumentRequest(
    Guid CandidateId,
    Guid JobPostingId,
    string DocumentType // "InterviewKit" | "ClientDossier" | "JdDraft"
);

public record DocumentPrepResultDto(
    string DocumentTitle,
    string ContentMarkdown,
    string ContentHtml
);

public record BurmeseLocalizationRequest(
    string SourceText,
    string TargetLanguage, // "my" (Burmese) | "en" (English)
    string? Context = null
);

public record BurmeseLocalizationResultDto(
    string OriginalText,
    string TranslatedText,
    string SourceLanguage,
    string TargetLanguage
);
