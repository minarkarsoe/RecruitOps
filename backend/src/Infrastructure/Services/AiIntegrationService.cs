using Microsoft.Extensions.Logging;
using RecruitOps.Application.DTOs.Ai;
using RecruitOps.Application.Interfaces;

namespace RecruitOps.Infrastructure.Services;

public class AiIntegrationService : IAiIntegrationService
{
    private readonly IClaudeService _claudeService;
    private readonly IGeminiService _geminiService;
    private readonly ILogger<AiIntegrationService> _logger;

    public AiIntegrationService(
        IClaudeService claudeService,
        IGeminiService geminiService,
        ILogger<AiIntegrationService> logger)
    {
        _claudeService = claudeService;
        _geminiService = geminiService;
        _logger = logger;
    }

    public Task<ParsedResumeResultDto> ParseResumeAsync(ParseResumeRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("Routing ParseResume request to Claude API service.");
        return _claudeService.ParseResumeAsync(request, ct);
    }

    public Task<CandidateMatchAnalysisDto> MatchCandidateAsync(MatchCandidateRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("Routing MatchCandidate request for Candidate {CandidateId} to Claude API service.", request.CandidateId);
        return _claudeService.MatchCandidateAsync(request, null, null, ct);
    }

    public Task<ExecutiveSummaryDto> GenerateExecutiveSummaryAsync(GenerateExecutiveSummaryRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("Routing ExecutiveSummary request for Candidate {CandidateId} to Gemini API service.", request.CandidateId);
        return _geminiService.GenerateExecutiveSummaryAsync(request, null, null, ct);
    }

    public Task<DocumentPrepResultDto> PrepareDocumentAsync(PrepareDocumentRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("Routing PrepareDocument request ({DocumentType}) for Candidate {CandidateId} to Gemini API service.", request.DocumentType, request.CandidateId);
        return _geminiService.PrepareDocumentAsync(request, null, null, ct);
    }

    public Task<BurmeseLocalizationResultDto> TranslateBurmeseAsync(BurmeseLocalizationRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("Routing BurmeseLocalization request (Target: {TargetLanguage}) to Gemini API service.", request.TargetLanguage);
        return _geminiService.TranslateBurmeseAsync(request, ct);
    }
}
