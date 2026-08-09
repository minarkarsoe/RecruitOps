namespace RecruitOps.Application.DTOs.Ai;

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
