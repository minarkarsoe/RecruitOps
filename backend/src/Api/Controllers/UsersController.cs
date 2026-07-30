using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecruitOps.Api.Auth;
using RecruitOps.Api.Authorization;
using RecruitOps.Application.DTOs;
using RecruitOps.Application.Interfaces;
using RecruitOps.Infrastructure.Persistence;

namespace RecruitOps.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly AppDbContext _db;

    public UsersController(IUserService userService, AppDbContext db)
    {
        _userService = userService;
        _db = db;
    }

    /// <summary>Paged directory of users with search, role, and active status filters.</summary>
    [HttpGet]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<PagedResult<UserListItemDto>>> Get(
        [FromQuery] UserQueryParameters query, CancellationToken ct)
    {
        var result = await _userService.GetUsersAsync(query, ct);
        return Ok(result);
    }

    /// <summary>Active users selectable for interview panels (ADR-0019).</summary>
    [HttpGet("selectable")]
    [Authorize(Policy = Policies.RecruitmentStaff)]
    public async Task<ActionResult<IReadOnlyList<SelectableUserDto>>> Selectable(CancellationToken ct)
    {
        var rows = await _db.Users
            .AsNoTracking()
            .Where(u => u.IsActive)
            .OrderBy(u => u.DisplayName)
            .Select(u => new { u.Id, u.DisplayName, u.Role })
            .ToListAsync(ct);

        var users = rows
            .Select(u => new SelectableUserDto(u.Id, u.DisplayName, u.Role.ToString()))
            .ToList();

        return Ok(users);
    }

    /// <summary>Get detailed user profile by ID including custom role and permissions.</summary>
    [HttpGet("{id:guid}")]
    [HasPermission("permission:users:users:read")]
    public async Task<ActionResult<UserDetailDto>> GetById(Guid id, CancellationToken ct)
    {
        var user = await _userService.GetUserByIdAsync(id, ct);
        return user is null
            ? NotFound(new ProblemDetails { Title = "User not found", Detail = $"No user found with ID '{id}'." })
            : Ok(user);
    }

    /// <summary>Create a new user account.</summary>
    [HttpPost]
    [HasPermission("permission:users:users:create")]
    public async Task<ActionResult<UserDetailDto>> Create([FromBody] CreateUserRequest request, CancellationToken ct)
    {
        try
        {
            var created = await _userService.CreateUserAsync(request, ct);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails { Title = "Cannot create user", Detail = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid user payload", Detail = ex.Message });
        }
    }

    /// <summary>Update user metadata and role assignment.</summary>
    [HttpPut("{id:guid}")]
    [HasPermission("permission:users:users:update")]
    public async Task<ActionResult<UserDetailDto>> Update(Guid id, [FromBody] UpdateUserRequest request, CancellationToken ct)
    {
        try
        {
            var updated = await _userService.UpdateUserAsync(id, request, ct);
            return updated is null
                ? NotFound(new ProblemDetails { Title = "User not found", Detail = $"No user found with ID '{id}'." })
                : Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails { Title = "Cannot update user", Detail = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid update payload", Detail = ex.Message });
        }
    }

    /// <summary>Deactivate user account.</summary>
    [HttpPut("{id:guid}/deactivate")]
    [HasPermission("permission:users:users:delete")]
    public async Task<ActionResult<UserDetailDto>> Deactivate(Guid id, CancellationToken ct)
    {
        try
        {
            var result = await _userService.SetUserActiveAsync(id, isActive: false, ct);
            return result is null
                ? NotFound(new ProblemDetails { Title = "User not found", Detail = $"No user found with ID '{id}'." })
                : Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails { Title = "Cannot deactivate user", Detail = ex.Message });
        }
    }

    /// <summary>Reactivate user account.</summary>
    [HttpPut("{id:guid}/reactivate")]
    [HasPermission("permission:users:users:update")]
    public async Task<ActionResult<UserDetailDto>> Reactivate(Guid id, CancellationToken ct)
    {
        try
        {
            var result = await _userService.SetUserActiveAsync(id, isActive: true, ct);
            return result is null
                ? NotFound(new ProblemDetails { Title = "User not found", Detail = $"No user found with ID '{id}'." })
                : Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails { Title = "Cannot reactivate user", Detail = ex.Message });
        }
    }
}
