using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RecruitOps.Api.Authorization;
using RecruitOps.Application.DTOs;
using RecruitOps.Application.Interfaces;

namespace RecruitOps.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PermissionsController : ControllerBase
{
    private readonly IRoleService _roleService;

    public PermissionsController(IRoleService roleService) => _roleService = roleService;

    /// <summary>List all available system permissions grouped by module and feature.</summary>
    [HttpGet]
    [HasPermission("permission:roles:roles:read")]
    public async Task<ActionResult<IReadOnlyList<PermissionModuleDto>>> Get(CancellationToken ct)
    {
        var permissions = await _roleService.GetPermissionsGroupedAsync(ct);
        return Ok(permissions);
    }
}
