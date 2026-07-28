using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RecruitOps.Api.Auth;
using RecruitOps.Application.DTOs;
using RecruitOps.Application.Interfaces;

namespace RecruitOps.Api.Controllers;

/// <summary>Module 1.3 — approval chain configuration.
/// <para>Admin-only: a chain decides who can approve headcount and spend, so being able
/// to edit one is equivalent to being able to approve. This is company configuration
/// (Module 7.1), not day-to-day recruiting.</para></summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = Policies.AdminOnly)]
public class ApprovalChainsController : ControllerBase
{
    private readonly IApprovalChainService _chains;

    public ApprovalChainsController(IApprovalChainService chains) => _chains = chains;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ApprovalChainDto>>> Get(CancellationToken ct)
        => Ok(await _chains.GetChainsAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApprovalChainDto>> GetById(Guid id, CancellationToken ct)
    {
        var chain = await _chains.GetByIdAsync(id, ct);
        return chain is null ? NotFound() : Ok(chain);
    }

    [HttpPost]
    public async Task<ActionResult<ApprovalChainDto>> Create(CreateApprovalChainRequest request, CancellationToken ct)
    {
        var created = await _chains.CreateAsync(request, ct);
        return created is null
            ? NotFound() // unknown department, or an approver who isn't a user here
            : CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }
}
