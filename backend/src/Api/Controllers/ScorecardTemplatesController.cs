using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RecruitOps.Api.Auth;
using RecruitOps.Application.DTOs;
using RecruitOps.Application.Interfaces;

namespace RecruitOps.Api.Controllers;

/// <summary>Module 3.3 configuration — the criteria interviews are scored against.
/// <para>Reads are open to any internal user: an interviewer should be able to see what
/// they will be asked before the day. Writes are recruitment staff, because defining the
/// criteria for a department is setting the standard everyone in it is compared against —
/// the same reasoning that made approval chains Admin-only in Module 1.</para></summary>
[ApiController]
[Route("api/scorecardtemplates")]
[Authorize(Policy = Policies.InternalUser)]
public class ScorecardTemplatesController : ControllerBase
{
    private readonly IScorecardTemplateService _templates;

    public ScorecardTemplatesController(IScorecardTemplateService templates)
        => _templates = templates;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ScorecardTemplateDto>>> List(CancellationToken ct)
        => Ok(await _templates.ListAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ScorecardTemplateDto>> Get(Guid id, CancellationToken ct)
    {
        var result = await _templates.GetAsync(id, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = Policies.RecruitmentStaff)]
    public async Task<ActionResult<ScorecardTemplateDto>> Create(
        SaveScorecardTemplateRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await _templates.CreateAsync(request, ct));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails { Title = "Cannot save template", Detail = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.RecruitmentStaff)]
    public async Task<ActionResult<ScorecardTemplateDto>> Update(
        Guid id, SaveScorecardTemplateRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _templates.UpdateAsync(id, request, ct);
            return result is null ? NotFound() : Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails { Title = "Cannot save template", Detail = ex.Message });
        }
    }

    /// <summary>Which template a posting's interviews will actually be scored against.
    /// Exposed so a recruiter can see the resolution result rather than infer it.</summary>
    [HttpGet("resolve/{jobPostingId:guid}")]
    public async Task<ActionResult<ScorecardTemplateDto>> Resolve(
        Guid jobPostingId, CancellationToken ct)
    {
        var result = await _templates.ResolveForPostingAsync(jobPostingId, ct);
        return result is null ? NotFound() : Ok(result);
    }
}
