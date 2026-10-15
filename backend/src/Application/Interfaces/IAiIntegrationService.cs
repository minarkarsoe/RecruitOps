using RecruitOps.Application.DTOs.Ai;

namespace RecruitOps.Application.Interfaces;

public interface IAiIntegrationService
{
    Task<ParsedResumeResultDto> ParseResumeAsync(ParseResumeRequest request, CancellationToken ct = default);
    Task<CandidateMatchAnalysisDto> MatchCandidateAsync(MatchCandidateRequest request, CancellationToken ct = default);
    Task<ExecutiveSummaryDto> GenerateExecutiveSummaryAsync(GenerateExecutiveSummaryRequest request, CancellationToken ct = default);
    Task<DocumentPrepResultDto> PrepareDocumentAsync(PrepareDocumentRequest request, CancellationToken ct = default);
    Task<BurmeseLocalizationResultDto> TranslateBurmeseAsync(BurmeseLocalizationRequest request, CancellationToken ct = default);
}
