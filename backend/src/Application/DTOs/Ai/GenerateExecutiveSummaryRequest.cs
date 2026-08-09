namespace RecruitOps.Application.DTOs.Ai;

public record GenerateExecutiveSummaryRequest(
    Guid CandidateId,
    Guid? JobPostingId,
    string? Tone // "Brief" | "Detailed" | "Executive"
);

public record ExecutiveSummaryDto(
    string Headline,
    string ExecutiveSummary,
    List<string> KeyHighlights,
    List<string> RecommendedInterviewQuestions
);
