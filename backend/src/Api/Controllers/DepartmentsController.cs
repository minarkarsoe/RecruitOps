using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RecruitOps.Api.Auth;
using RecruitOps.Api.Authorization;
using RecruitOps.Application.DTOs;
using RecruitOps.Application.Interfaces;

namespace RecruitOps.Api.Controllers;

/// <summary>Departments — the unit that owns requisitions and the axis department scoping
/// is applied along (ADR-0003).
///
/// <para>The class policy is <see cref="Policies.InternalUser"/> so a Hiring Manager can
/// load the department picker on the new-requisition form; the service returns only their
/// own. Everything that <em>changes</em> a department needs <c>settings:update</c>, because
/// creating a department or editing its membership is granting access to requisitions — the same
/// authority as editing an approval chain, and now the same permission (ADR-0022 amendment,
/// 2026-10-17). The administration view needs <c>settings:read</c>, which the HR Director holds.
/// Both sit <em>on top of</em> the class's <c>InternalUser</c> — authorization attributes are
/// additive (ADR-0019 amendment, 2026-07-28) — and so does <see cref="Policies.CompanyWide"/>:
/// a department-scoped role administers no department, whatever it holds, because membership is
/// its own scope.</para></summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = Policies.InternalUser)]
public class DepartmentsController : ControllerBase
{
    private readonly IDepartmentService _departments;

    public DepartmentsController(IDepartmentService departments) => _departments = departments;

    /// <summary>Active departments the caller can raise work in.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DepartmentListItemDto>>> Get(CancellationToken ct)
        => Ok(await _departments.GetDepartmentsAsync(ct));

    /// <summary>All departments, including inactive, with member and open-requisition counts.</summary>
    // Reads on settings:read, every change on settings:update — the approval-chain split (ADR-0022
    // amendment, 2026-10-17). The HR Director reads departments; only Admin changes them by default.
    [HttpGet("admin")]
    [HasPermission("permission:settings:settings:read")]
    [Authorize(Policy = Policies.CompanyWide)]
    public async Task<ActionResult<IReadOnlyList<DepartmentDetailDto>>> GetForAdmin(CancellationToken ct)
        => Ok(await _departments.GetForAdminAsync(ct));

    [HttpPost]
    [HasPermission("permission:settings:settings:update")]
    [Authorize(Policy = Policies.CompanyWide)]
    public async Task<ActionResult<DepartmentDetailDto>> Create(
        CreateDepartmentRequest request, CancellationToken ct)
    {
        try
        {
            var created = await _departments.CreateAsync(request, ct);
            return CreatedAtAction(nameof(GetForAdmin), new { }, created);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails { Title = "Cannot create department", Detail = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    [HasPermission("permission:settings:settings:update")]
    [Authorize(Policy = Policies.CompanyWide)]
    public async Task<ActionResult<DepartmentDetailDto>> Update(
        Guid id, UpdateDepartmentRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _departments.UpdateAsync(id, request, ct);
            return result is null ? NotFound() : Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails { Title = "Cannot rename department", Detail = ex.Message });
        }
    }

    /// <summary>Stops new work being raised here. There is no delete — requisitions,
    /// postings and the audit trail point at departments.</summary>
    [HttpPost("{id:guid}/deactivate")]
    [HasPermission("permission:settings:settings:update")]
    [Authorize(Policy = Policies.CompanyWide)]
    public Task<ActionResult<DepartmentDetailDto>> Deactivate(Guid id, CancellationToken ct)
        => SetActive(id, false, "Cannot deactivate", ct);

    [HttpPost("{id:guid}/activate")]
    [HasPermission("permission:settings:settings:update")]
    [Authorize(Policy = Policies.CompanyWide)]
    public Task<ActionResult<DepartmentDetailDto>> Activate(Guid id, CancellationToken ct)
        => SetActive(id, true, "Cannot activate", ct);

    private async Task<ActionResult<DepartmentDetailDto>> SetActive(
        Guid id, bool isActive, string failureTitle, CancellationToken ct)
    {
        try
        {
            var result = await _departments.SetActiveAsync(id, isActive, ct);
            return result is null ? NotFound() : Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails { Title = failureTitle, Detail = ex.Message });
        }
    }

    /// <summary>The whole user roster, flagged with who belongs to this department.</summary>
    [HttpGet("{id:guid}/members")]
    [HasPermission("permission:settings:settings:read")]
    [Authorize(Policy = Policies.CompanyWide)]
    public async Task<ActionResult<IReadOnlyList<DepartmentMemberDto>>> GetMembers(Guid id, CancellationToken ct)
    {
        var result = await _departments.GetMembersAsync(id, ct);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Replaces the member list. PUT, not PATCH: this grants and revokes access to
    /// requisitions, and an admin should be committing to a complete list rather than
    /// issuing deltas against a state they cannot see.</summary>
    [HttpPut("{id:guid}/members")]
    [HasPermission("permission:settings:settings:update")]
    [Authorize(Policy = Policies.CompanyWide)]
    public async Task<ActionResult<IReadOnlyList<DepartmentMemberDto>>> SetMembers(
        Guid id, SetDepartmentMembersRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _departments.SetMembersAsync(id, request, ct);
            return result is null ? NotFound() : Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails { Title = "Cannot set members", Detail = ex.Message });
        }
    }
}
