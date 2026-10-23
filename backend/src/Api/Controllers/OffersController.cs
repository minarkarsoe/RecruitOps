using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using RecruitOps.Api.Auth;
using RecruitOps.Api.Authorization;
using RecruitOps.Application.DTOs;
using RecruitOps.Application.Interfaces;

namespace RecruitOps.Api.Controllers;

/// <summary>Module 4 offers, slice A (ADR-0030): the dashboard, drafting, the budget check and
/// internal approval.
///
/// <para>The class policy is <see cref="Policies.InternalUser"/> because reach is per offer, decided
/// in <c>OfferService</c>: a Hiring Manager reads their departments' offers, and anyone named on an
/// offer's approval step — whatever their role — reads and decides that one. Interviewers are not
/// internal users here and reach nothing. Every write a recruiter makes is
/// <see cref="Policies.RecruitmentStaff"/> on top. Out of reach is a 404, never a 403 (ADR-0003).</para></summary>
[ApiController]
[Route("api")]
[Authorize(Policy = Policies.InternalUser)]
public class OffersController : ControllerBase
{
    private readonly IOfferService _offers;

    public OffersController(IOfferService offers) => _offers = offers;

    // ---------- dashboard ----------

    [HttpGet("offers")]
    public async Task<ActionResult<IReadOnlyList<OfferListItemDto>>> List(
        [FromQuery] string? group, [FromQuery] Guid? departmentId,
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
        => Ok(await _offers.ListAsync(group, departmentId, from, to, ct));

    /// <summary>Applications at the Offer stage with no open offer — what "Create offer" starts from.</summary>
    [HttpGet("offers/eligible-applications")]
    [Authorize(Policy = Policies.RecruitmentStaff)]
    public async Task<ActionResult<IReadOnlyList<OfferEligibleApplicationDto>>> Eligible(CancellationToken ct)
        => Ok(await _offers.EligibleApplicationsAsync(ct));

    [HttpGet("offers/{id:guid}")]
    public async Task<ActionResult<OfferDetailDto>> Get(Guid id, CancellationToken ct)
        => await _offers.GetAsync(id, ct) is { } offer ? Ok(offer) : NotFound();

    /// <summary>The letter with its merge fields filled from the current terms. It quotes the salary,
    /// so it follows the salary rule: a viewer who may not see salaries gets a 404.</summary>
    [HttpGet("offers/{id:guid}/letter")]
    public async Task<ActionResult<OfferLetterDto>> Letter(Guid id, CancellationToken ct)
        => await _offers.RenderLetterAsync(id, ct) is { } letter ? Ok(letter) : NotFound();

    // ---------- drafting and approval ----------

    [HttpPost("offers")]
    [Authorize(Policy = Policies.RecruitmentStaff)]
    public Task<ActionResult<OfferDetailDto>> Create(CreateOfferRequest request, CancellationToken ct)
        => Guarded(() => _offers.CreateAsync(request, ct));

    [HttpPut("offers/{id:guid}")]
    [Authorize(Policy = Policies.RecruitmentStaff)]
    public Task<ActionResult<OfferDetailDto>> Update(Guid id, UpdateOfferRequest request, CancellationToken ct)
        => Guarded(() => _offers.UpdateAsync(id, request, ct));

    [HttpPost("offers/{id:guid}/submit")]
    [Authorize(Policy = Policies.RecruitmentStaff)]
    public Task<ActionResult<OfferDetailDto>> Submit(Guid id, CancellationToken ct)
        => Guarded(() => _offers.SubmitAsync(id, ct));

    /// <summary>Only someone holding a waiting step of the live round reaches this — whatever their
    /// role; anyone else gets a 404 before anything about the offer is said.</summary>
    [HttpPost("offers/{id:guid}/decide")]
    public Task<ActionResult<OfferDetailDto>> Decide(Guid id, ApprovalDecisionRequest request, CancellationToken ct)
        => Guarded(() => _offers.DecideAsync(id, request, ct));

    [HttpPost("offers/{id:guid}/revise")]
    [Authorize(Policy = Policies.RecruitmentStaff)]
    public Task<ActionResult<OfferDetailDto>> Revise(Guid id, CancellationToken ct)
        => Guarded(() => _offers.ReviseAsync(id, ct));

    /// <summary>Emails the candidate their link (ADR-0030 §5a).</summary>
    [HttpPost("offers/{id:guid}/send")]
    [Authorize(Policy = Policies.RecruitmentStaff)]
    public Task<ActionResult<OfferDetailDto>> Send(
        Guid id, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] SendOfferRequest? request, CancellationToken ct)
        => Guarded(() => _offers.SendAsync(id, request?.ExpectedEmail, ct));

    /// <summary>"Remind candidate": the same link again, at most once a day.</summary>
    [HttpPost("offers/{id:guid}/remind")]
    [Authorize(Policy = Policies.RecruitmentStaff)]
    public Task<ActionResult<OfferDetailDto>> Remind(Guid id, CancellationToken ct)
        => Guarded(() => _offers.RemindAsync(id, ct));

    [HttpDelete("offers/{id:guid}")]
    [Authorize(Policy = Policies.RecruitmentStaff)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try
        {
            return await _offers.DeleteAsync(id, ct) ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails { Title = "Cannot delete offer", Detail = ex.Message });
        }
    }

    // ---------- templates and settings ----------

    /// <summary>Templates to start a letter from — the built-in default first, with no id.</summary>
    [HttpGet("offer-templates")]
    [Authorize(Policy = Policies.RecruitmentStaff)]
    public async Task<ActionResult<IReadOnlyList<OfferTemplateDto>>> Templates(CancellationToken ct)
        => Ok(await _offers.TemplatesAsync(ct));

    /// <summary>Templates live in System Settings (spec 4.2): <c>settings:update</c> and company-wide reach,
    /// as for departments.</summary>
    [HttpPost("offer-templates")]
    [HasPermission("permission:settings:settings:update")]
    [Authorize(Policy = Policies.CompanyWide)]
    public async Task<ActionResult<OfferTemplateDto>> CreateTemplate(SaveOfferTemplateRequest request, CancellationToken ct)
        => Ok(await _offers.CreateTemplateAsync(request, ct));

    [HttpPut("offer-templates/{id:guid}")]
    [HasPermission("permission:settings:settings:update")]
    [Authorize(Policy = Policies.CompanyWide)]
    public async Task<ActionResult<OfferTemplateDto>> UpdateTemplate(Guid id, SaveOfferTemplateRequest request, CancellationToken ct)
        => await _offers.UpdateTemplateAsync(id, request, ct) is { } template ? Ok(template) : NotFound();

    [HttpGet("settings/offers")]
    [HasPermission("permission:settings:settings:read")]
    [Authorize(Policy = Policies.CompanyWide)]
    public async Task<ActionResult<OfferSettingsDto>> Settings(CancellationToken ct)
        => Ok(await _offers.SettingsAsync(ct));

    [HttpPut("settings/offers")]
    [HasPermission("permission:settings:settings:update")]
    [Authorize(Policy = Policies.CompanyWide)]
    public async Task<ActionResult<OfferSettingsDto>> UpdateSettings(UpdateOfferSettingsRequest request, CancellationToken ct)
        => Ok(await _offers.UpdateSettingsAsync(request, ct));

    private async Task<ActionResult<OfferDetailDto>> Guarded(Func<Task<OfferDetailDto?>> action)
    {
        try
        {
            var result = await action();
            return result is null ? NotFound() : Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails { Title = "Cannot update offer", Detail = ex.Message });
        }
    }
}
