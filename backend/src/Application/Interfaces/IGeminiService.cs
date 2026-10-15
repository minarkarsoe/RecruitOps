using RecruitOps.Application.DTOs.Ai;

namespace RecruitOps.Application.Interfaces;

public interface IGeminiService
{
    Task<ExecutiveSummaryDto> GenerateExecutiveSummaryAsync(GenerateExecutiveSummaryRequest request, string? candidateProfileData = null, string? jobPostingData = null, CancellationToken ct = default);
    Task<DocumentPrepResultDto> PrepareDocumentAsync(PrepareDocumentRequest request, string? candidateProfileData = null, string? jobPostingData = null, CancellationToken ct = default);
    Task<BurmeseLocalizationResultDto> TranslateBurmeseAsync(BurmeseLocalizationRequest request, CancellationToken ct = default);
}
