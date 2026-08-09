using RecruitOps.Application.DTOs.Ai;

namespace RecruitOps.Application.Interfaces;

public interface IClaudeService
{
    Task<ParsedResumeResultDto> ParseResumeAsync(ParseResumeRequest request, CancellationToken ct = default);
    Task<CandidateMatchAnalysisDto> MatchCandidateAsync(MatchCandidateRequest request, string? candidateProfileData = null, string? jobPostingData = null, CancellationToken ct = default);
}
