using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RecruitOps.Api.Auth;
using RecruitOps.Application.DTOs;
using RecruitOps.Application.Interfaces;

namespace RecruitOps.Api.Controllers;

/// <summary>Module 1 — Job Requisition &amp; Approval.
/// <para>Uses <see cref="Policies.InternalUser"/> (not RecruitmentStaff) because Hiring
/// Managers and Approvers must reach these endpoints. Row-level visibility is enforced
/// by the service via department scoping (ADR-0003).</para></summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = Policies.InternalUser)]
public class RequisitionsController : ControllerBase
{
    private readonly IRequisitionService _requisitions;

    public RequisitionsController(IRequisitionService requisitions) => _requisitions = requisitions;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RequisitionListItemDto>>> Get(CancellationToken ct)
        => Ok(await _requisitions.GetRequisitionsAsync(ct));

    /// <summary>Requisitions where the current user is the next-in-line approver.
    /// Named route "inbox" so it is resolved before the {id:guid} route.</summary>
    [HttpGet("inbox")]
    public async Task<ActionResult<IReadOnlyList<RequisitionListItemDto>>> GetInbox(CancellationToken ct)
        => Ok(await _requisitions.GetInboxAsync(ct));

    /// <summary>404 covers both "does not exist" and "not yours" so existence isn't leaked.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RequisitionDetailDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await _requisitions.GetByIdAsync(id, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<RequisitionDetailDto>> Create(CreateRequisitionRequest request, CancellationToken ct)
    {
        var created = await _requisitions.CreateAsync(request, ct);
        return created is null
            ? NotFound() // unknown department, or not one of the caller's
            : CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Edits a Draft. 409 once it has been submitted.</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<RequisitionDetailDto>> Update(
        Guid id, UpdateRequisitionRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _requisitions.UpdateAsync(id, request, ct);
            return result is null ? NotFound() : Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails { Title = "Cannot edit", Detail = ex.Message });
        }
    }

    [HttpPost("{id:guid}/submit")]
    public async Task<ActionResult<RequisitionDetailDto>> Submit(Guid id, CancellationToken ct)
    {
        try
        {
            var result = await _requisitions.SubmitAsync(id, ct);
            return result is null ? NotFound() : Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails { Title = "Cannot submit", Detail = ex.Message });
        }
    }

    [HttpPost("{id:guid}/decision")]
    public async Task<ActionResult<RequisitionDetailDto>> Decide(Guid id, ApprovalDecisionRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _requisitions.DecideAsync(id, request, ct);
            return result is null ? NotFound() : Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails { Title = "Cannot record decision", Detail = ex.Message });
        }
    }

    /// <summary>Withdraws a requisition before it is decided. The requester or a
    /// company-wide role may do this; anyone else gets 404 (ADR-0003).</summary>
    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<RequisitionDetailDto>> Cancel(Guid id, CancellationToken ct)
    {
        try
        {
            var result = await _requisitions.CancelAsync(id, ct);
            return result is null ? NotFound() : Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails { Title = "Cannot cancel", Detail = ex.Message });
        }
    }
}
