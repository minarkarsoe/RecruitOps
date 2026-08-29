using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RecruitOps.Api.Auth;
using RecruitOps.Application.DTOs;
using RecruitOps.Application.Interfaces;

namespace RecruitOps.Api.Controllers;

/// <summary>Module 2.5 — moving candidates through the pipeline.
/// <para>Route is "applications" (not "jobapplications") because the entity's name only
/// avoids a namespace collision; the URL should read the way the business speaks.</para></summary>
[ApiController]
[Route("api/applications")]
[Authorize(Policy = Policies.InternalUser)]
public class ApplicationsController : ControllerBase
{
    private readonly IPipelineService _pipeline;
    private readonly IResumeService _resumeService;

    public ApplicationsController(
        IPipelineService pipeline,
        IResumeService resumeService)
    {
        _pipeline = pipeline;
        _resumeService = resumeService;
    }

    [HttpPost("{id:guid}/stage")]
    [Authorize(Policy = Policies.RecruitmentStaff)] // hiring managers comment; recruiters move
    public async Task<ActionResult<PipelineItemDto>> MoveStage(
        Guid id, MoveStageRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _pipeline.MoveStageAsync(id, request, ct);
            return result is null ? NotFound() : Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails { Title = "Cannot move stage", Detail = ex.Message });
        }
    }

    /// <summary>Append-only stage history — the raw material for Module 5's analytics.</summary>
    [HttpGet("{id:guid}/history")]
    public async Task<ActionResult<IReadOnlyList<StageHistoryItemDto>>> History(Guid id, CancellationToken ct)
    {
        var result = await _pipeline.GetHistoryAsync(id, ct);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Upload and extract a candidate resume (PDF or DOCX).
    /// <para>Images are rejected while there is no OCR — see the guard below.</para></summary>
    [HttpPost("{id:guid}/resume")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ResumeExtractionResultDto>> UploadResume(
        Guid id, IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid File", Detail = "File is empty or missing." });
        }

        if (file.Length > 10 * 1024 * 1024) // 10MB
        {
            return BadRequest(new ProblemDetails { Title = "File Too Large", Detail = "File size exceeds maximum limit of 10MB." });
        }

        // Images were removed on 2026-08-29, matching BulkResumeService.AllowedExtensions. There is
        // no OCR in this build — accepting a photo produced a blank candidate from a fabricated
        // "Image Document: …" string and still reported success. The message names the reason
        // rather than only the rule, because "PNG is unsupported" invites the recruiter to convert
        // the photo to PDF, which lands in exactly the same empty-text path.
        var allowedExtensions = new[] { ".pdf", ".docx" };
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(ext))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Unsupported Format",
                Detail = "Allowed formats are PDF and DOCX. Photographed or scanned CVs cannot be "
                       + "read yet — text recognition is not enabled on this installation, so a "
                       + "picture of a CV would be stored with no readable content and would not "
                       + "appear in search. Please upload a text PDF or a Word document."
            });
        }

        var result = await _resumeService.UploadAndExtractResumeAsync(id, file, ct);
        if (result is null)
        {
            return NotFound(new ProblemDetails { Title = "Not Found", Detail = "Application not found or unauthorized." });
        }

        return Ok(result);
    }

    /// <summary>Download stored candidate resume document stream.</summary>
    [HttpGet("{id:guid}/resume")]
    public async Task<IActionResult> GetResume(Guid id, CancellationToken ct)
    {
        var fileResult = await _resumeService.GetResumeFileAsync(id, ct);
        if (fileResult is null)
        {
            return NotFound(new ProblemDetails { Title = "Not Found", Detail = "Resume not found or unauthorized." });
        }

        return File(fileResult.Value.Stream, fileResult.Value.ContentType, fileResult.Value.FileName);
    }
}
