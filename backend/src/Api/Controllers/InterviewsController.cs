using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RecruitOps.Api.Auth;
using RecruitOps.Application.DTOs;
using RecruitOps.Application.Interfaces;

namespace RecruitOps.Api.Controllers;

/// <summary>Module 3 — interview rounds, panels and evaluations.
///
/// <para><b>Read endpoints are <see cref="Policies.InternalUser"/>, not
/// <see cref="Policies.RecruitmentStaff"/>.</b> A panel member is very often a Hiring
/// Manager from another department; locking these to recruitment staff would make
/// cross-department panels impossible, which is the case ADR-0017 §4 exists to allow. The
/// department predicate is applied in the service, as ADR-0003 requires — the policy here
/// is not the access control.</para>
///
/// <para><b>Write endpoints are <see cref="Policies.RecruitmentStaff"/></b>, and the service
/// additionally refuses a caller whose only reach is participation. Two guards, because the
/// policy alone would let a Hiring Manager reschedule any round in their own department,
/// which is a recruiter's job.</para>
/// </summary>
[ApiController]
[Route("api")]
[Authorize(Policy = Policies.InternalUser)]
public class InterviewsController : ControllerBase
{
    private readonly IInterviewService _interviews;
    private readonly IScorecardService _scorecards;

    public InterviewsController(IInterviewService interviews, IScorecardService scorecards)
    {
        _interviews = interviews;
        _scorecards = scorecards;
    }

    // ---------- scheduling ----------

    [HttpPost("applications/{applicationId:guid}/interviews")]
    [Authorize(Policy = Policies.RecruitmentStaff)]
    public async Task<ActionResult<InterviewDto>> Schedule(
        Guid applicationId, ScheduleInterviewRequest request, CancellationToken ct)
        => await Guarded(() => _interviews.ScheduleAsync(applicationId, request, ct));

    [HttpGet("applications/{applicationId:guid}/interviews")]
    public async Task<ActionResult<IReadOnlyList<InterviewDto>>> ListForApplication(
        Guid applicationId, CancellationToken ct)
    {
        var result = await _interviews.ListForApplicationAsync(applicationId, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("interviews/{id:guid}")]
    public async Task<ActionResult<InterviewDto>> Get(Guid id, CancellationToken ct)
    {
        var result = await _interviews.GetAsync(id, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPut("interviews/{id:guid}")]
    [Authorize(Policy = Policies.RecruitmentStaff)]
    public async Task<ActionResult<InterviewDto>> Reschedule(
        Guid id, RescheduleInterviewRequest request, CancellationToken ct)
        => await Guarded(() => _interviews.RescheduleAsync(id, request, ct));

    [HttpPut("interviews/{id:guid}/panel")]
    [Authorize(Policy = Policies.RecruitmentStaff)]
    public async Task<ActionResult<InterviewDto>> SetPanel(
        Guid id, SetPanelRequest request, CancellationToken ct)
        => await Guarded(() => _interviews.SetPanelAsync(id, request, ct));

    [HttpPost("interviews/{id:guid}/cancel")]
    [Authorize(Policy = Policies.RecruitmentStaff)]
    public async Task<ActionResult<InterviewDto>> Cancel(
        Guid id, CancelInterviewRequest request, CancellationToken ct)
        => await Guarded(() => _interviews.CancelAsync(id, request, ct));

    [HttpPost("interviews/{id:guid}/complete")]
    [Authorize(Policy = Policies.RecruitmentStaff)]
    public async Task<ActionResult<InterviewDto>> Complete(
        Guid id, CompleteInterviewRequest request, CancellationToken ct)
        => await Guarded(() => _interviews.CompleteAsync(id, request, ct));

    // ---------- scorecards (3.3) ----------

    /// <summary>The caller's own scorecard and the criteria to fill in.</summary>
    [HttpGet("interviews/{id:guid}/scorecard")]
    public async Task<ActionResult<MyScorecardDto>> MyScorecard(Guid id, CancellationToken ct)
    {
        var result = await _scorecards.GetMineAsync(id, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPut("interviews/{id:guid}/scorecard")]
    public async Task<ActionResult<MyScorecardDto>> SaveMyScorecard(
        Guid id, SaveScorecardRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _scorecards.SaveMineAsync(id, request, ct);
            return result is null ? NotFound() : Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails { Title = "Cannot save scorecard", Detail = ex.Message });
        }
    }

    [HttpPost("interviews/{id:guid}/scorecard/submit")]
    public async Task<ActionResult<MyScorecardDto>> SubmitMyScorecard(
        Guid id, SaveScorecardRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _scorecards.SubmitMineAsync(id, request, ct);
            return result is null ? NotFound() : Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails { Title = "Cannot submit scorecard", Detail = ex.Message });
        }
    }

    /// <summary>The panel's evaluations, filtered by the blind rule (ADR-0017 §3).
    /// <para>A separate endpoint from the one above on purpose: the visibility rule lives
    /// inside this method and cannot be reached past by a query parameter on the other.</para></summary>
    [HttpGet("interviews/{id:guid}/scorecards")]
    public async Task<ActionResult<InterviewScorecardsDto>> PanelScorecards(
        Guid id, CancellationToken ct)
    {
        var result = await _scorecards.GetForInterviewAsync(id, ct);
        return result is null ? NotFound() : Ok(result);
    }

    // ---------- shared ----------

    /// <summary>Null → 404 (no such round, or not yours — indistinguishable on purpose);
    /// <see cref="InvalidOperationException"/> → 409.</summary>
    private async Task<ActionResult<InterviewDto>> Guarded(Func<Task<InterviewDto?>> action)
    {
        try
        {
            var result = await action();
            return result is null ? NotFound() : Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails { Title = "Cannot update interview", Detail = ex.Message });
        }
    }
}
