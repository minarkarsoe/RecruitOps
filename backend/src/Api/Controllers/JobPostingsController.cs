using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RecruitOps.Api.Auth;
using RecruitOps.Application.Common;
using RecruitOps.Application.DTOs;
using RecruitOps.Application.Interfaces;

namespace RecruitOps.Api.Controllers;

/// <summary>Module 2.1 — job postings.
/// <para><see cref="Policies.InternalUser"/> rather than RecruitmentStaff so a Hiring
/// Manager can see the postings for their own department; row-level visibility is enforced
/// by the service via department scoping (ADR-0003).</para></summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = Policies.InternalUser)]
public class JobPostingsController : ControllerBase
{
    private readonly IJobPostingService _postings;
    private readonly IPipelineService _pipeline;
    private readonly IBulkResumeService _bulkResumeService;
    private readonly ICurrentUser _currentUser;

    public JobPostingsController(
        IJobPostingService postings,
        IPipelineService pipeline,
        IBulkResumeService bulkResumeService,
        ICurrentUser currentUser)
    {
        _postings = postings;
        _pipeline = pipeline;
        _bulkResumeService = bulkResumeService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<JobPostingListItemDto>>> Get(CancellationToken ct)
        => Ok(await _postings.GetPostingsAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<JobPostingDetailDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await _postings.GetByIdAsync(id, ct);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Creates a Draft posting from an approved requisition.</summary>
    [HttpPost]
    [Authorize(Policy = Policies.RecruitmentStaff)] // publishing is a recruiter's job, not a hiring manager's
    public async Task<ActionResult<JobPostingDetailDto>> Create(
        CreateJobPostingRequest request, CancellationToken ct)
    {
        try
        {
            var created = await _postings.CreateFromRequisitionAsync(request, ct);
            return created is null
                ? NotFound()
                : CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails { Title = "Cannot create posting", Detail = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.RecruitmentStaff)]
    public async Task<ActionResult<JobPostingDetailDto>> Update(
        Guid id, UpdateJobPostingRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _postings.UpdateAsync(id, request, ct);
            return result is null ? NotFound() : Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails { Title = "Cannot edit posting", Detail = ex.Message });
        }
    }

    [HttpPost("{id:guid}/publish")]
    [Authorize(Policy = Policies.RecruitmentStaff)]
    public async Task<ActionResult<JobPostingDetailDto>> Publish(Guid id, CancellationToken ct)
    {
        try
        {
            var result = await _postings.PublishAsync(id, ct);
            return result is null ? NotFound() : Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails { Title = "Cannot publish", Detail = ex.Message });
        }
    }

    [HttpPost("{id:guid}/close")]
    [Authorize(Policy = Policies.RecruitmentStaff)]
    public async Task<ActionResult<JobPostingDetailDto>> Close(Guid id, CancellationToken ct)
    {
        try
        {
            var result = await _postings.CloseAsync(id, ct);
            return result is null ? NotFound() : Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails { Title = "Cannot close", Detail = ex.Message });
        }
    }

    /// <summary>The talent pipeline for this posting (Module 2.5).</summary>
    [HttpGet("{id:guid}/pipeline")]
    public async Task<ActionResult<IReadOnlyList<PipelineItemDto>>> Pipeline(Guid id, CancellationToken ct)
    {
        var result = await _pipeline.GetForPostingAsync(id, ct);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// Accepts up to 50 CV files for asynchronous bulk processing against a job posting.
    /// </summary>
    [HttpPost("{jobPostingId:guid}/resumes/bulk")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<BulkUploadBatchResponseDto>> BulkUploadResumes(
        Guid jobPostingId,
        [FromForm] IFormFileCollection files,
        CancellationToken ct)
    {
        if (files is null || files.Count == 0)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid Request",
                Detail = "No files provided for bulk upload."
            });
        }

        if (files.Count > 50)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Batch Limit Exceeded",
                Detail = "Batch size exceeds maximum limit of 50 files."
            });
        }

        var fileInputs = new List<BulkFileItemInput>();
        foreach (var file in files)
        {
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);
            fileInputs.Add(new BulkFileItemInput(
                FileName: file.FileName,
                Content: ms.ToArray(),
                ContentType: file.ContentType
            ));
        }

        var result = await _bulkResumeService.EnqueueBatchAsync(jobPostingId, fileInputs, _currentUser.UserId, ct);

        if (result is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Not Found or Unauthorized",
                Detail = "Job posting not found or department access denied."
            });
        }

        return Ok(result);
    }

    /// <summary>
    /// Retrieves the current status and per-file progress summary of a bulk CV upload batch.
    /// </summary>
    [HttpGet("{jobPostingId:guid}/resumes/bulk/{batchId:guid}")]
    public async Task<ActionResult<BulkBatchStatusDto>> GetBulkUploadStatus(
        Guid jobPostingId,
        Guid batchId,
        CancellationToken ct)
    {
        var result = await _bulkResumeService.GetBatchStatusAsync(jobPostingId, batchId, ct);
        if (result is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Batch Not Found",
                Detail = "Bulk upload batch not found or department access denied."
            });
        }

        return Ok(result);
    }
}

