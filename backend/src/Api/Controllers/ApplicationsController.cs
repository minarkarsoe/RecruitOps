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

    public ApplicationsController(IPipelineService pipeline) => _pipeline = pipeline;

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
}
