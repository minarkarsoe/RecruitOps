using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RecruitOps.Api.Auth;
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

    public JobPostingsController(IJobPostingService postings, IPipelineService pipeline)
    {
        _postings = postings;
        _pipeline = pipeline;
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
}
