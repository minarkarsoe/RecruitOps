using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RecruitOps.Api.Auth;
using RecruitOps.Application.DTOs;
using RecruitOps.Application.Interfaces;

namespace RecruitOps.Api.Controllers;

/// <summary>Candidate records across postings — the Talent pool. Recruitment staff only: these
/// roles are company-wide, which is what lets them see two records of one person from different
/// departments' postings side by side.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = Policies.RecruitmentStaff)]
public class CandidatesController : ControllerBase
{
    private readonly ICandidateDuplicateService _duplicates;

    public CandidatesController(ICandidateDuplicateService duplicates) => _duplicates = duplicates;

    // TODO: the Talent pool list itself (search, filters) — design/internal/talent-pool.html.
    [HttpGet]
    public IActionResult Get() => Ok(Array.Empty<object>());

    /// <summary>Possible duplicates (Module 2.7): records sharing a phone or email, not yet decided.</summary>
    [HttpGet("duplicates")]
    public async Task<ActionResult<IReadOnlyList<DuplicatePairDto>>> Duplicates(CancellationToken ct)
        => Ok(await _duplicates.ListPairsAsync(ct));

    /// <summary>Folds one record into the other. Not reversible.</summary>
    [HttpPost("merge")]
    public async Task<ActionResult<MergeCandidatesResultDto>> Merge(MergeCandidatesRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _duplicates.MergeAsync(request, ct);
            return result is null ? NotFound() : Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails { Title = "Cannot merge", Detail = ex.Message });
        }
    }

    /// <summary>"Not the same person" — remembered, so the pair stops being flagged.</summary>
    [HttpPost("keep-separate")]
    public async Task<IActionResult> KeepSeparate(KeepCandidatesSeparateRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _duplicates.KeepSeparateAsync(request, ct);
            return result is null ? NotFound() : NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails { Title = "Cannot keep separate", Detail = ex.Message });
        }
    }
}
