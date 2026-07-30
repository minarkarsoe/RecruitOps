using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RecruitOps.Api.Authorization;
using RecruitOps.Application.DTOs;
using RecruitOps.Application.Interfaces;

namespace RecruitOps.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RolesController : ControllerBase
{
    private readonly IRoleService _roleService;

    public RolesController(IRoleService roleService) => _roleService = roleService;

    /// <summary>List all system and custom tenant roles.</summary>
    [HttpGet]
    [HasPermission("permission:roles:roles:read")]
    public async Task<ActionResult<IReadOnlyList<RoleListItemDto>>> Get(CancellationToken ct)
    {
        var roles = await _roleService.GetRolesAsync(ct);
        return Ok(roles);
    }

    /// <summary>Get role details with assigned permission codes.</summary>
    [HttpGet("{id:guid}")]
    [HasPermission("permission:roles:roles:read")]
    public async Task<ActionResult<RoleDetailDto>> GetById(Guid id, CancellationToken ct)
    {
        var role = await _roleService.GetRoleByIdAsync(id, ct);
        return role is null ? NotFound(new ProblemDetails { Title = "Role not found", Detail = $"No role found with ID '{id}'." }) : Ok(role);
    }

    /// <summary>Create a custom tenant role with assigned permissions.</summary>
    [HttpPost]
    [HasPermission("permission:roles:roles:create")]
    public async Task<ActionResult<RoleDetailDto>> Create([FromBody] CreateRoleRequest request, CancellationToken ct)
    {
        try
        {
            var created = await _roleService.CreateRoleAsync(request, ct);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails { Title = "Cannot create role", Detail = ex.Message });
        }
    }

    /// <summary>Update custom tenant role metadata and permissions.</summary>
    [HttpPut("{id:guid}")]
    [HasPermission("permission:roles:roles:update")]
    public async Task<ActionResult<RoleDetailDto>> Update(Guid id, [FromBody] UpdateRoleRequest request, CancellationToken ct)
    {
        try
        {
            var updated = await _roleService.UpdateRoleAsync(id, request, ct);
            return updated is null ? NotFound(new ProblemDetails { Title = "Role not found", Detail = $"No role found with ID '{id}'." }) : Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Cannot update role", Detail = ex.Message });
        }
    }

    /// <summary>Delete custom tenant role (enforcing system role protection).</summary>
    [HttpDelete("{id:guid}")]
    [HasPermission("permission:roles:roles:delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try
        {
            var deleted = await _roleService.DeleteRoleAsync(id, ct);
            return deleted ? NoContent() : NotFound(new ProblemDetails { Title = "Role not found", Detail = $"No role found with ID '{id}'." });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails { Title = "Cannot delete role", Detail = ex.Message });
        }
    }
}
