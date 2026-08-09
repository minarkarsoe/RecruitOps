using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RecruitOps.Api.Authorization;
using RecruitOps.Application.DTOs.Ai;
using RecruitOps.Application.Interfaces;

namespace RecruitOps.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AiController : ControllerBase
{
    private readonly IAiIntegrationService _aiService;

    public AiController(IAiIntegrationService aiService)
    {
        _aiService = aiService;
    }

    /// <summary>
    /// Parses and structures resume text into candidate profile fields using Claude AI.
    /// </summary>
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

        var result = await _aiService.ParseResumeAsync(request, ct);
        return Ok(result);
    }

    /// <summary>
    /// Analyzes candidate fit against job requirements using Claude AI.
    /// </summary>
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

        var result = await _aiService.MatchCandidateAsync(request, ct);
        return Ok(result);
    }

    /// <summary>
    /// Generates executive summary and suggested interview questions using Gemini AI.
    /// </summary>
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

        var result = await _aiService.GenerateExecutiveSummaryAsync(request, ct);
        return Ok(result);
    }

    /// <summary>
    /// Prepares interview kits and client dossiers in Markdown & HTML using Gemini AI.
    /// </summary>
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

        var result = await _aiService.PrepareDocumentAsync(request, ct);
        return Ok(result);
    }

    /// <summary>
    /// Translates text between English and Burmese using Gemini AI.
    /// </summary>
    [HttpPost("gemini/burmese-localization")]
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

        var result = await _aiService.TranslateBurmeseAsync(request, ct);
        return Ok(result);
    }
}
