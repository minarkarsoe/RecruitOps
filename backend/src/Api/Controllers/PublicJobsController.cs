using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RecruitOps.Api.Auth;
using RecruitOps.Application.DTOs;
using RecruitOps.Application.Interfaces;

namespace RecruitOps.Api.Controllers;

/// <summary>Module 2.1/2.2 — the public job page and application form.
///
/// <para>⚠️ This is the <b>only</b> anonymous surface besides login, and unlike login it
/// <b>writes</b>. Two consequences it does not get to opt out of:</para>
/// <list type="bullet">
/// <item>It is rate limited. An unauthenticated endpoint that creates rows is an invitation
/// to fill the customer's candidate database with junk.</item>
/// <item>Every response is deliberately uninformative. An unknown, revoked, expired or
/// unpublished token all return the same 404, so nobody can probe for near-miss tokens.</item>
/// </list>
/// </summary>
[ApiController]
[Route("api/public/jobs")]
[AllowAnonymous]
[EnableRateLimiting(RateLimitPolicies.PublicApply)]
public class PublicJobsController : ControllerBase
{
    private readonly IPublicJobService _jobs;

    public PublicJobsController(IPublicJobService jobs) => _jobs = jobs;

    /// <summary>The job as an applicant sees it. Consumed server-side by the Next.js public
    /// app, which needs it at render time to emit Open Graph tags.</summary>
    [HttpGet("{token}")]
    public async Task<ActionResult<PublicJobDto>> GetByToken(string token, CancellationToken ct)
    {
        var result = await _jobs.GetByTokenAsync(token, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("{token}/apply")]
    public async Task<ActionResult<SubmitApplicationResponse>> Apply(
        string token, SubmitApplicationRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _jobs.SubmitAsync(token, request, ct);
            return result is null ? NotFound() : Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            // The applicant is a member of the public, so this message is shown to them —
            // it says what to do, not what the server's state is.
            return Conflict(new ProblemDetails { Title = "Cannot apply", Detail = ex.Message });
        }
    }
}
