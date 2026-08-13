using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RecruitOps.Api.Attributes;
using RecruitOps.Api.Authorization;
using RecruitOps.Application.Common.Exceptions;
using RecruitOps.Application.DTOs.Ai;
using RecruitOps.Application.Interfaces;

namespace RecruitOps.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[FeatureGate("EnableAiProfiling")]
public class AiController : ControllerBase
{
    private readonly IAiIntegrationService _aiService;
    private readonly IAiSimulationScope _simulation;

    public AiController(IAiIntegrationService aiService, IAiSimulationScope simulation)
    {
        _aiService = aiService;
        _simulation = simulation;
    }

    /// <summary>
    /// The one place an AI call's outcome is turned into a status code. Every action goes through
    /// it so the rules cannot be applied to some endpoints and forgotten on others:
    /// <list type="bullet">
    /// <item>no API key configured — 402, the feature is off</item>
    /// <item>provider called but unusable — 502; never a fabricated answer with a 200</item>
    /// <item>answer came from a development stub — 200 stamped <c>X-Ai-Simulated: true</c></item>
    /// </list>
    /// </summary>
    private async Task<ActionResult<T>> RunAsync<T>(Func<Task<T>> call)
    {
        try
        {
            var result = await call();

            if (_simulation.IsSimulated)
            {
                Response.Headers["X-Ai-Simulated"] = "true";
            }

            return Ok(result);
        }
        catch (AiApiKeyMissingException ex)
        {
            return StatusCode(StatusCodes.Status402PaymentRequired, new ProblemDetails
            {
                Status = StatusCodes.Status402PaymentRequired,
                Title = "AI Feature Disabled or API Key Unconfigured",
                Detail = ex.Message,
                Type = "https://recruitops.io/errors/ai-feature-disabled",
                Instance = HttpContext.Request.Path
            });
        }
        catch (AiProviderUnavailableException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new ProblemDetails
            {
                Status = StatusCodes.Status502BadGateway,
                Title = "AI Provider Unavailable",
                Detail = ex.Message,
                Type = "https://recruitops.io/errors/ai-provider-unavailable",
                Instance = HttpContext.Request.Path
            });
        }
    }

    /// <summary>
    /// Parses and structures resume text into candidate profile fields using Claude AI.
    /// Dual routed: POST /api/ai/parse-resume AND /api/ai/claude/parse-resume
    /// </summary>
    [HttpPost("parse-resume")]
    [HttpPost("claude/parse-resume")]
    [HasPermission("permission:ai:resume:parse")]
    public async Task<ActionResult<ParsedResumeResultDto>> ParseResume(
        [FromBody] ParseResumeRequest request, CancellationToken ct)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.ResumeText))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid Request Payload",
                Detail = "ResumeText cannot be empty."
            });
        }

        return await RunAsync(() => _aiService.ParseResumeAsync(request, ct));
    }

    /// <summary>
    /// Analyzes candidate fit against job requirements using Claude AI.
    /// Dual routed: POST /api/ai/match-candidate AND /api/ai/claude/match-candidate
    /// </summary>
    [HttpPost("match-candidate")]
    [HttpPost("claude/match-candidate")]
    [HasPermission("permission:ai:matching:analyze")]
    public async Task<ActionResult<CandidateMatchAnalysisDto>> MatchCandidate(
        [FromBody] MatchCandidateRequest request, CancellationToken ct)
    {
        if (request == null || request.CandidateId == Guid.Empty || request.JobPostingId == Guid.Empty)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid Request Payload",
                Detail = "CandidateId and JobPostingId must be valid non-empty GUIDs."
            });
        }

        return await RunAsync(() => _aiService.MatchCandidateAsync(request, ct));
    }

    /// <summary>
    /// Generates executive summary and suggested interview questions using Gemini AI.
    /// Dual routed: POST /api/ai/executive-summary AND /api/ai/gemini/executive-summary
    /// </summary>
    [HttpPost("executive-summary")]
    [HttpPost("gemini/executive-summary")]
    [HasPermission("permission:ai:summary:generate")]
    public async Task<ActionResult<ExecutiveSummaryDto>> GenerateExecutiveSummary(
        [FromBody] GenerateExecutiveSummaryRequest request, CancellationToken ct)
    {
        if (request == null || request.CandidateId == Guid.Empty)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid Request Payload",
                Detail = "CandidateId must be a valid non-empty GUID."
            });
        }

        return await RunAsync(() => _aiService.GenerateExecutiveSummaryAsync(request, ct));
    }

    /// <summary>
    /// Prepares interview kits and client dossiers in Markdown & HTML using Gemini AI.
    /// Dual routed: POST /api/ai/document-prep AND /api/ai/gemini/document-prep
    /// </summary>
    [HttpPost("document-prep")]
    [HttpPost("gemini/document-prep")]
    [HasPermission("permission:ai:document:prepare")]
    public async Task<ActionResult<DocumentPrepResultDto>> PrepareDocument(
        [FromBody] PrepareDocumentRequest request, CancellationToken ct)
    {
        if (request == null || request.CandidateId == Guid.Empty || request.JobPostingId == Guid.Empty || string.IsNullOrWhiteSpace(request.DocumentType))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid Request Payload",
                Detail = "CandidateId, JobPostingId, and DocumentType are required."
            });
        }

        return await RunAsync(() => _aiService.PrepareDocumentAsync(request, ct));
    }

    /// <summary>
    /// Translates text between English and Burmese using Gemini AI.
    /// Dual routed: POST /api/ai/translate AND /api/ai/gemini/burmese-localization (and /api/ai/gemini/translate)
    /// </summary>
    [HttpPost("translate")]
    [HttpPost("gemini/burmese-localization")]
    [HttpPost("gemini/translate")]
    [HasPermission("permission:ai:localization:translate")]
    public async Task<ActionResult<BurmeseLocalizationResultDto>> BurmeseLocalization(
        [FromBody] BurmeseLocalizationRequest request, CancellationToken ct)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.SourceText) || string.IsNullOrWhiteSpace(request.TargetLanguage))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid Request Payload",
                Detail = "SourceText and TargetLanguage are required."
            });
        }

        return await RunAsync(() => _aiService.TranslateBurmeseAsync(request, ct));
    }
}
