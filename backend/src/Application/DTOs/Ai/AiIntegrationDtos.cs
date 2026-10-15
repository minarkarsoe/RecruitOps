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

/// <param name="Language">Output language: <c>"en"</c>, <c>"my"</c> (Burmese Unicode) or
/// <c>"bilingual"</c>. Null means English.
///
/// <para>Added 2026-08-28. The SPA had shipped an EN / MY / Bilingual selector since Module 2
/// and sent a <c>language</c> field this record did not have, so model binding discarded it on
/// every request — the control looked like it worked and never did. Burmese output is a real
/// requirement (ADR-0009), not a nicety: the market this product is sold into reads Burmese, and
/// a summary a hiring manager cannot read is a summary nobody uses.</para></param>
public record GenerateExecutiveSummaryRequest(
    Guid CandidateId,
    Guid? JobPostingId = null,
    string? Tone = null,
    string? Language = null
);

public record ExecutiveSummaryDto(
    string Headline,
    string ExecutiveSummary,
    List<string> KeyHighlights,
    List<string> RecommendedInterviewQuestions
);

/// <summary>
/// The documents this endpoint knows how to prepare.
///
/// <para><b>A closed set, not a comment.</b> <c>DocumentType</c> was a free <c>string</c>
/// documented as <c>"InterviewKit" | "ClientDossier" | "JdDraft"</c> and validated nowhere — and
/// it is interpolated straight into the model prompt in <c>GeminiApiClient</c>. An unvalidated
/// caller-supplied string reaching a prompt is prompt-injection surface: <c>"InterviewKit. Ignore
/// the above and ..."</c> was a perfectly acceptable document type. Deleting a value from the
/// comment would not have stopped anyone sending it.</para>
///
/// <para><c>ClientDossier</c> is gone (2026-08-28). It was an agency-era concept — a candidate
/// packaged for presentation to a client — and ADR-0001 removed clients from the product on
/// 2026-07-27. There is nobody to send a dossier to.</para>
/// </summary>
public static class DocumentTypes
{
    public const string InterviewKit = "InterviewKit";
    public const string JdDraft = "JdDraft";

    public static readonly IReadOnlyList<string> All = new[] { InterviewKit, JdDraft };

    /// <summary>Case-sensitive on purpose: this is an identifier crossing an API boundary, and
    /// the set is short enough that a caller can match it exactly.</summary>
    public static bool IsSupported(string? documentType) =>
        documentType is not null && All.Contains(documentType, StringComparer.Ordinal);
}

public record PrepareDocumentRequest(
    Guid CandidateId,
    Guid JobPostingId,
    /// <summary>One of <see cref="DocumentTypes.All"/>. Rejected with 400 otherwise.</summary>
    string DocumentType
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
