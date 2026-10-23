using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RecruitOps.Api.Auth;
using RecruitOps.Api.Authorization;
using RecruitOps.Application.DTOs;
using RecruitOps.Application.Interfaces;

namespace RecruitOps.Api.Controllers;

/// <summary>Module 4.3 pre-boarding, the recruiter's side (ADR-0030 §5b), drawn in
/// <c>design/internal/preboarding-review.html</c>.
///
/// <para><b>Recruitment staff only</b>, at the class: a Hiring Manager, an approver or an interviewer never
/// reaches a candidate's NRC or bank details, whatever offer they can see (403). The service checks again.
/// <b>And a permission per action</b> — read, review, reveal — so the Role Builder can withhold them from a
/// custom role whose base is Recruiter (security review, 2026-10-23, M1). An offer that is not accepted is
/// a 404.</para>
///
/// <para>Opening a file and revealing an account number are written to the audit log before anything is
/// returned. A file goes out as an attachment with the type detected when it was uploaded; the security
/// headers middleware adds <c>nosniff</c> and <c>no-store</c> to every API response.</para></summary>
[ApiController]
[Route("api")]
[Authorize(Policy = Policies.RecruitmentStaff)]
public class PreboardingController : ControllerBase
{
    private const string Read = "permission:offers:preboarding:read";
    private const string Review = "permission:offers:preboarding:review";

    private readonly IPreboardingService _preboarding;

    public PreboardingController(IPreboardingService preboarding) => _preboarding = preboarding;

    [HttpGet("offers/{id:guid}/preboarding")]
    [HasPermission(Read)]
    public async Task<ActionResult<PreboardingDto>> Get(Guid id, CancellationToken ct)
        => await _preboarding.GetAsync(id, ct) is { } dto ? Ok(dto) : NotFound();

    [HttpGet("offers/{id:guid}/preboarding/files/{fileId:guid}")]
    [HasPermission(Read)]
    public async Task<IActionResult> OpenFile(Guid id, Guid fileId, CancellationToken ct)
        => await _preboarding.OpenFileAsync(id, fileId, ct) is { } file
            ? File(file.Content, file.ContentType, file.DownloadName)
            : NotFound();

    [HttpPost("offers/{id:guid}/preboarding/bank/reveal")]
    [HasPermission("permission:offers:preboarding:reveal")]
    public async Task<ActionResult<RevealedBankNumberDto>> RevealBankNumber(Guid id, CancellationToken ct)
        => await _preboarding.RevealBankNumberAsync(id, ct) is { } number ? Ok(number) : NotFound();

    [HttpPost("offers/{id:guid}/preboarding/{kind}/approve")]
    [HasPermission(Review)]
    public Task<ActionResult<PreboardingDto>> Approve(Guid id, string kind, ReviewPreboardingDocumentRequest request, CancellationToken ct)
        => Guarded(() => _preboarding.ApproveAsync(id, kind, request.Revision!.Value, ct)); // [Required]

    /// <summary>Sends a document back to the candidate with the recruiter's message, and emails it.</summary>
    [HttpPost("offers/{id:guid}/preboarding/{kind}/return")]
    [HasPermission(Review)]
    public Task<ActionResult<PreboardingDto>> Return(Guid id, string kind, ReturnPreboardingDocumentRequest request, CancellationToken ct)
        => Guarded(() => _preboarding.ReturnAsync(id, kind, request.Revision!.Value, request.Message, ct)); // [Required]

    /// <summary>Emails the candidate what is still missing, at most once a day.</summary>
    [HttpPost("offers/{id:guid}/preboarding/remind")]
    [HasPermission(Review)]
    public Task<ActionResult<PreboardingDto>> Remind(Guid id, CancellationToken ct)
        => Guarded(() => _preboarding.RemindAsync(id, ct));

    // ---------- settings ----------

    [HttpGet("settings/preboarding")]
    [HasPermission("permission:settings:settings:read")]
    [Authorize(Policy = Policies.CompanyWide)]
    public async Task<ActionResult<PreboardingSettingsDto>> Settings(CancellationToken ct)
        => Ok(await _preboarding.SettingsAsync(ct));

    [HttpPut("settings/preboarding")]
    [HasPermission("permission:settings:settings:update")]
    [Authorize(Policy = Policies.CompanyWide)]
    public async Task<ActionResult<PreboardingSettingsDto>> UpdateSettings(UpdatePreboardingSettingsRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await _preboarding.UpdateSettingsAsync(request, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Cannot save the pre-boarding settings", Detail = ex.Message });
        }
    }

    private async Task<ActionResult<PreboardingDto>> Guarded(Func<Task<PreboardingDto?>> action)
    {
        try
        {
            return await action() is { } dto ? Ok(dto) : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails { Title = "Cannot update pre-boarding", Detail = ex.Message });
        }
    }
}
