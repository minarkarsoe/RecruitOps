using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RecruitOps.Api.Auth;
using RecruitOps.Api.Authorization;
using RecruitOps.Application.DTOs;
using RecruitOps.Application.Interfaces;

namespace RecruitOps.Api.Controllers;

/// <summary>Module 1.3 — approval chain configuration.
/// <para>Permission-driven (ADR-0022): reading a chain requires
/// <c>permission:settings:settings:read</c>, and creating one requires
/// <c>permission:settings:settings:update</c> — a chain decides who can approve headcount
/// and spend, so being able to edit one is equivalent to being able to approve. This is
/// company configuration (Module 7.1), not day-to-day recruiting, and both codes are
/// grantable per role through the Role Builder rather than welded to a role literal.</para>
/// <para>Both also need <see cref="Policies.CompanyWide"/>: whoever is named on a chain can open
/// the requisitions it governs, so a department-scoped role editing chains could name itself and
/// reach every department (security review, 2026-10-17, M2).</para></summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = Policies.CompanyWide)]
public class ApprovalChainsController : ControllerBase
{
    private readonly IApprovalChainService _chains;

    public ApprovalChainsController(IApprovalChainService chains) => _chains = chains;

    [HttpGet]
    [HasPermission("permission:settings:settings:read")]
    public async Task<ActionResult<IReadOnlyList<ApprovalChainDto>>> Get(CancellationToken ct)
        => Ok(await _chains.GetChainsAsync(ct));

    [HttpGet("{id:guid}")]
    [HasPermission("permission:settings:settings:read")]
    public async Task<ActionResult<ApprovalChainDto>> GetById(Guid id, CancellationToken ct)
    {
        var chain = await _chains.GetByIdAsync(id, ct);
        return chain is null ? NotFound() : Ok(chain);
    }

    [HttpPost]
    [HasPermission("permission:settings:settings:update")]
    public async Task<ActionResult<ApprovalChainDto>> Create(CreateApprovalChainRequest request, CancellationToken ct)
    {
        var created = await _chains.CreateAsync(request, ct);
        return created is null
            ? NotFound() // unknown department, or an approver who isn't a user here
            : CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }
}
