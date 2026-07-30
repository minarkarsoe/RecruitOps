# Roles & Permissions Management API Architecture & Design Report (Milestone 3 — Requirement R3)

## Executive Summary
This report provides a comprehensive architectural investigation and step-by-step implementation design for Requirement R3 (Roles & Permissions Management APIs) of RecruitOps backend (.NET 10 Clean Architecture).

The investigation analyzed existing Domain entities (`Role`, `Permission`, `RolePermission`, `User`), Infrastructure persistence configurations (`AppDbContext`, `RbacSeedData`), existing API controllers (`DepartmentsController`, `UsersController`), and authentication/authorization structures (`Program.cs`, `Policies.cs`). Based on these findings, we design 6 RESTful API endpoints for Roles & Permissions management with DTO contracts, validation rules, authorization requirements, system safeguards (e.g., system role immutability and delete protection), error handling, and a full verification plan.

---

## 1. Observation

### 1.1 Source Code Locations & Existing Implementations

1. **Domain Entities**:
   - `backend/src/Domain/Entities/Role.cs` (lines 6–18):
     ```csharp
     public class Role : BaseEntity
     {
         public Guid? TenantId { get; set; }
         public string Name { get; set; } = string.Empty;
         public string Code { get; set; } = string.Empty;
         public string Description { get; set; } = string.Empty;
         public bool IsSystemRole { get; set; }
         public bool IsSuperAdmin { get; set; }
         public bool IsActive { get; set; } = true;

         public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
         public ICollection<User> Users { get; set; } = new List<User>();
     }
     ```
   - `backend/src/Domain/Entities/Permission.cs` (lines 6–16):
     ```csharp
     public class Permission : BaseEntity
     {
         public string Module { get; set; } = string.Empty;
         public string Feature { get; set; } = string.Empty;
         public string Action { get; set; } = string.Empty;
         public string Name { get; set; } = string.Empty;
         public string Description { get; set; } = string.Empty;
         public string Code { get; set; } = string.Empty; // Format: permission:module:feature:action

         public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
     }
     ```
   - `backend/src/Domain/Entities/RolePermission.cs` (lines 4–13):
     ```csharp
     public class RolePermission
     {
         public Guid RoleId { get; set; }
         public Role Role { get; set; } = null!;
         public Guid PermissionId { get; set; }
         public Permission Permission { get; set; } = null!;
         public DateTimeOffset AssignedAt { get; set; } = DateTimeOffset.UtcNow;
     }
     ```
   - `backend/src/Domain/Entities/User.cs` (lines 7–22):
     ```csharp
     public class User : BaseEntity, ITenantScoped
     {
         public Guid TenantId { get; set; }
         public string Email { get; set; } = string.Empty;
         public string DisplayName { get; set; } = string.Empty;
         public string PasswordHash { get; set; } = string.Empty;
         public UserRole Role { get; set; } = UserRole.Recruiter; // Legacy enum for backwards compatibility
         public bool IsActive { get; set; } = true;
         public Guid? RoleId { get; set; }
         public Role? CustomRole { get; set; }
         public bool IsSuperAdmin { get; set; }
     }
     ```

2. **Database Context & Query Filters**:
   - `backend/src/Infrastructure/Persistence/AppDbContext.cs`:
     - Line 25–27: `DbSet<Role> Roles`, `DbSet<Permission> Permissions`, `DbSet<RolePermission> RolePermissions`.
     - Lines 126–162:
       ```csharp
       builder.Entity<Role>(e => {
           e.Property(x => x.Name).IsRequired().HasMaxLength(200);
           e.Property(x => x.Code).IsRequired().HasMaxLength(100);
           e.Property(x => x.Description).HasMaxLength(1000);
           e.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
       });
       builder.Entity<Permission>(e => {
           e.Property(x => x.Module).IsRequired().HasMaxLength(100);
           e.Property(x => x.Feature).IsRequired().HasMaxLength(100);
           e.Property(x => x.Action).IsRequired().HasMaxLength(100);
           e.Property(x => x.Name).IsRequired().HasMaxLength(200);
           e.Property(x => x.Code).IsRequired().HasMaxLength(200);
           e.HasIndex(x => x.Code).IsUnique();
       });
       ```
     - Line 454: Tenant Query Filter for `Role`:
       ```csharp
       builder.Entity<Role>().HasQueryFilter(e => e.TenantId == null || e.TenantId == _tenant.TenantId);
       ```
       *Observation*: System roles have `TenantId == null` while custom tenant roles have `TenantId == _tenant.TenantId`. Thus, querying `_db.Roles` automatically returns both system roles and the current tenant's custom roles without exposing other tenants' custom roles.

3. **Seeded Canonical Permissions & System Roles**:
   - `backend/src/Infrastructure/Persistence/RbacSeedData.cs`:
     - Defines 34 canonical permissions across 9 modules (`requisitions`, `postings`, `applications`, `interviews`, `scorecards`, `users`, `roles`, `settings`, `system`).
     - Defines 7 default system roles: `SuperAdmin`, `Admin`, `HrDirector`, `Recruiter`, `HiringManager`, `Approver`, `Interviewer`.
     - Canonical Role permission codes (lines 57–60):
       - `permission:roles:roles:read` -> Read Roles
       - `permission:roles:roles:create` -> Create Roles
       - `permission:roles:roles:update` -> Update Roles
       - `permission:roles:roles:delete` -> Delete Roles

4. **Service Architecture Pattern in RecruitOps**:
   - Clean Architecture flow in RecruitOps:
     - Interfaces in `backend/src/Application/Interfaces/`
     - DTOs in `backend/src/Application/DTOs/`
     - Service implementations in `backend/src/Infrastructure/Services/`
     - Service registration in `backend/src/Infrastructure/DependencyInjection.cs`
     - Web API Controllers in `backend/src/Api/Controllers/`

---

## 2. Logic Chain

From the observations above:
1. **Tenant Scoping**: `Role` entity has `TenantId` nullable. `AppDbContext` applies query filter `e.TenantId == null || e.TenantId == _tenant.TenantId`. This guarantees system roles (`TenantId == null`) are visible to all tenants, while custom roles are strictly scoped to the tenant that created them.
2. **System Role Immutability**: System roles (`IsSystemRole == true`) are shared across tenants and defined in `RbacSeedData.cs`. Modifying or deleting a system role would corrupt global tenant baseline permissions. Therefore, `PUT /api/roles/{id}` and `DELETE /api/roles/{id}` must enforce `IsSystemRole == false` check and return `400 Bad Request` or `403 Forbidden` if targeted.
3. **Data Integrity & Cascade Deletes**: `RolePermission` has composite key `{ RoleId, PermissionId }` with cascade delete on `RoleId`. Deleting a custom role will clean up its join records automatically in EF Core. However, deleting a role that is assigned to active `User` records (`User.RoleId == id`) would cause foreign key violation or leave orphaned users. Hence, `DELETE` must explicitly check `role.Users.Any(u => u.IsActive)` and return `409 Conflict` if active users exist.
4. **EF Core 10 Translation Safeguard**: In `UsersController.cs`, materializing SQL projections before calling in-memory string formatting was required. When mapping `Role` and `RolePermission` entities to DTOs in `RoleService`, projection into `RoleListItemDto` or `RoleDetailDto` should select primitive counts (`r.RolePermissions.Count`, `r.Users.Count`) directly or perform in-memory mapping after `ToListAsync()`.

---

## 3. Step-by-Step Implementation Design for Requirement R3

### 3.1 Designed Endpoints Specification

| Method | Route | Description | Authorization Requirement |
|---|---|---|---|
| `GET` | `/api/permissions` | List all available permissions grouped by module/feature | `permission:roles:roles:read` |
| `GET` | `/api/roles` | List system roles + tenant custom roles | `permission:roles:roles:read` |
| `GET` | `/api/roles/{id}` | Get detailed role info with assigned permission codes | `permission:roles:roles:read` |
| `POST` | `/api/roles` | Create a custom tenant role with permission assignments | `permission:roles:roles:create` |
| `PUT` | `/api/roles/{id}` | Update custom tenant role permissions/metadata | `permission:roles:roles:update` |
| `DELETE` | `/api/roles/{id}` | Delete custom tenant role (system role protection) | `permission:roles:roles:delete` |

---

### 3.2 DTO Contracts Design (`backend/src/Application/DTOs/RoleDtos.cs`)

```csharp
namespace RecruitOps.Application.DTOs;

public record PermissionDto(
    Guid Id,
    string Code,
    string Name,
    string Description,
    string Module,
    string Feature,
    string Action
);

public record PermissionFeatureDto(
    string Feature,
    IReadOnlyList<PermissionDto> Permissions
);

public record PermissionModuleDto(
    string Module,
    IReadOnlyList<PermissionFeatureDto> Features
);

public record RoleListItemDto(
    Guid Id,
    string Name,
    string Code,
    string Description,
    bool IsSystemRole,
    bool IsSuperAdmin,
    bool IsActive,
    int UserCount,
    int PermissionCount
);

public record RoleDetailDto(
    Guid Id,
    string Name,
    string Code,
    string Description,
    bool IsSystemRole,
    bool IsSuperAdmin,
    bool IsActive,
    IReadOnlyList<PermissionDto> AssignedPermissions,
    IReadOnlyList<string> AssignedPermissionCodes,
    int UserCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt
);

public record CreateRoleRequest(
    string Name,
    string? Code,
    string? Description,
    IReadOnlyList<string> PermissionCodes
);

public record UpdateRoleRequest(
    string Name,
    string? Description,
    bool IsActive,
    IReadOnlyList<string> PermissionCodes
);
```

---

### 3.3 Application Service Interface (`backend/src/Application/Interfaces/IRoleService.cs`)

```csharp
using RecruitOps.Application.DTOs;

namespace RecruitOps.Application.Interfaces;

public interface IRoleService
{
    Task<IReadOnlyList<PermissionModuleDto>> GetPermissionsGroupedAsync(CancellationToken ct = default);
    Task<IReadOnlyList<RoleListItemDto>> GetRolesAsync(CancellationToken ct = default);
    Task<RoleDetailDto?> GetRoleByIdAsync(Guid id, CancellationToken ct = default);
    Task<RoleDetailDto> CreateRoleAsync(CreateRoleRequest request, CancellationToken ct = default);
    Task<RoleDetailDto?> UpdateRoleAsync(Guid id, UpdateRoleRequest request, CancellationToken ct = default);
    Task<bool> DeleteRoleAsync(Guid id, CancellationToken ct = default);
}
```

---

### 3.4 Infrastructure Service Implementation (`backend/src/Infrastructure/Services/RoleService.cs`)

```csharp
using Microsoft.EntityFrameworkCore;
using RecruitOps.Application.Common;
using RecruitOps.Application.DTOs;
using RecruitOps.Application.Interfaces;
using RecruitOps.Domain.Entities;
using RecruitOps.Infrastructure.Persistence;

namespace RecruitOps.Infrastructure.Services;

public class RoleService : IRoleService
{
    private readonly AppDbContext _db;
    private readonly ICurrentTenant _tenant;

    public RoleService(AppDbContext db, ICurrentTenant tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<PermissionModuleDto>> GetPermissionsGroupedAsync(CancellationToken ct = default)
    {
        var permissions = await _db.Permissions
            .AsNoTracking()
            .OrderBy(p => p.Module)
            .ThenBy(p => p.Feature)
            .ThenBy(p => p.Action)
            .ToListAsync(ct);

        var grouped = permissions
            .GroupBy(p => p.Module)
            .Select(gModule => new PermissionModuleDto(
                gModule.Key,
                gModule.GroupBy(p => p.Feature)
                    .Select(gFeature => new PermissionFeatureDto(
                        gFeature.Key,
                        gFeature.Select(p => new PermissionDto(
                            p.Id, p.Code, p.Name, p.Description, p.Module, p.Feature, p.Action
                        )).ToList()
                    )).ToList()
            )).ToList();

        return grouped;
    }

    public async Task<IReadOnlyList<RoleListItemDto>> GetRolesAsync(CancellationToken ct = default)
    {
        var roles = await _db.Roles
            .AsNoTracking()
            .Include(r => r.RolePermissions)
            .Include(r => r.Users)
            .OrderBy(r => r.IsSystemRole ? 0 : 1)
            .ThenBy(r => r.Name)
            .ToListAsync(ct);

        return roles.Select(r => new RoleListItemDto(
            r.Id,
            r.Name,
            r.Code,
            r.Description,
            r.IsSystemRole,
            r.IsSuperAdmin,
            r.IsActive,
            r.Users.Count(u => u.IsActive),
            r.RolePermissions.Count
        )).ToList();
    }

    public async Task<RoleDetailDto?> GetRoleByIdAsync(Guid id, CancellationToken ct = default)
    {
        var role = await _db.Roles
            .AsNoTracking()
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .Include(r => r.Users)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        if (role is null) return null;

        var assignedPermissions = role.RolePermissions
            .Select(rp => new PermissionDto(
                rp.Permission.Id,
                rp.Permission.Code,
                rp.Permission.Name,
                rp.Permission.Description,
                rp.Permission.Module,
                rp.Permission.Feature,
                rp.Permission.Action
            )).ToList();

        var assignedCodes = assignedPermissions.Select(p => p.Code).ToList();

        return new RoleDetailDto(
            role.Id,
            role.Name,
            role.Code,
            role.Description,
            role.IsSystemRole,
            role.IsSuperAdmin,
            role.IsActive,
            assignedPermissions,
            assignedCodes,
            role.Users.Count(u => u.IsActive),
            role.CreatedAt,
            role.UpdatedAt
        );
    }

    public async Task<RoleDetailDto> CreateRoleAsync(CreateRoleRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new InvalidOperationException("Role name is required.");

        var trimmedName = request.Name.Trim();
        var nameExists = await _db.Roles.AnyAsync(r => r.Name.ToLower() == trimmedName.ToLower(), ct);
        if (nameExists)
            throw new InvalidOperationException($"A role named '{trimmedName}' already exists in this tenant.");

        var code = string.IsNullOrWhiteSpace(request.Code)
            ? trimmedName.ToUpperInvariant().Replace(" ", "_")
            : request.Code.Trim().ToUpperInvariant();

        var codeExists = await _db.Roles.AnyAsync(r => r.Code.ToLower() == code.ToLower(), ct);
        if (codeExists)
            throw new InvalidOperationException($"A role with code '{code}' already exists.");

        var requestedCodes = request.PermissionCodes?.Distinct().ToList() ?? new List<string>();
        var validPermissions = await _db.Permissions
            .Where(p => requestedCodes.Contains(p.Code))
            .ToListAsync(ct);

        if (validPermissions.Count != requestedCodes.Count)
        {
            var foundCodes = validPermissions.Select(p => p.Code).ToHashSet();
            var invalidCodes = requestedCodes.Where(c => !foundCodes.Contains(c)).ToList();
            throw new InvalidOperationException($"Invalid permission code(s): {string.Join(", ", invalidCodes)}");
        }

        var role = new Role
        {
            Id = Guid.NewGuid(),
            TenantId = _tenant.TenantId,
            Name = trimmedName,
            Code = code,
            Description = request.Description?.Trim() ?? string.Empty,
            IsSystemRole = false,
            IsSuperAdmin = false,
            IsActive = true
        };

        foreach (var perm in validPermissions)
        {
            role.RolePermissions.Add(new RolePermission
            {
                RoleId = role.Id,
                PermissionId = perm.Id,
                AssignedAt = DateTimeOffset.UtcNow
            });
        }

        _db.Roles.Add(role);
        await _db.SaveChangesAsync(ct);

        return (await GetRoleByIdAsync(role.Id, ct))!;
    }

    public async Task<RoleDetailDto?> UpdateRoleAsync(Guid id, UpdateRoleRequest request, CancellationToken ct = default)
    {
        var role = await _db.Roles
            .Include(r => r.RolePermissions)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        if (role is null) return null;

        if (role.IsSystemRole)
            throw new InvalidOperationException("System roles are pre-configured and immutable.");

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new InvalidOperationException("Role name is required.");

        var trimmedName = request.Name.Trim();
        var nameExists = await _db.Roles.AnyAsync(r => r.Id != id && r.Name.ToLower() == trimmedName.ToLower(), ct);
        if (nameExists)
            throw new InvalidOperationException($"A role named '{trimmedName}' already exists.");

        var requestedCodes = request.PermissionCodes?.Distinct().ToList() ?? new List<string>();
        var validPermissions = await _db.Permissions
            .Where(p => requestedCodes.Contains(p.Code))
            .ToListAsync(ct);

        if (validPermissions.Count != requestedCodes.Count)
        {
            var foundCodes = validPermissions.Select(p => p.Code).ToHashSet();
            var invalidCodes = requestedCodes.Where(c => !foundCodes.Contains(c)).ToList();
            throw new InvalidOperationException($"Invalid permission code(s): {string.Join(", ", invalidCodes)}");
        }

        role.Name = trimmedName;
        role.Description = request.Description?.Trim() ?? string.Empty;
        role.IsActive = request.IsActive;

        // Sync role permissions
        var newPermIds = validPermissions.Select(p => p.Id).ToHashSet();
        var toRemove = role.RolePermissions.Where(rp => !newPermIds.Contains(rp.PermissionId)).ToList();
        foreach (var rp in toRemove)
        {
            _db.RolePermissions.Remove(rp);
        }

        var existingPermIds = role.RolePermissions.Select(rp => rp.PermissionId).ToHashSet();
        foreach (var perm in validPermissions.Where(p => !existingPermIds.Contains(p.Id)))
        {
            role.RolePermissions.Add(new RolePermission
            {
                RoleId = role.Id,
                PermissionId = perm.Id,
                AssignedAt = DateTimeOffset.UtcNow
            });
        }

        await _db.SaveChangesAsync(ct);
        return await GetRoleByIdAsync(role.Id, ct);
    }

    public async Task<bool> DeleteRoleAsync(Guid id, CancellationToken ct = default)
    {
        var role = await _db.Roles
            .Include(r => r.Users)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        if (role is null) return false;

        if (role.IsSystemRole)
            throw new InvalidOperationException("Pre-configured system roles cannot be deleted.");

        var activeUsersCount = role.Users.Count(u => u.IsActive);
        if (activeUsersCount > 0)
            throw new InvalidOperationException($"Cannot delete role '{role.Name}' because it is assigned to {activeUsersCount} active user(s).");

        _db.Roles.Remove(role);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
```

---

### 3.5 API Controllers Design

#### 1. `backend/src/Api/Controllers/PermissionsController.cs`:
```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RecruitOps.Application.DTOs;
using RecruitOps.Application.Interfaces;

namespace RecruitOps.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "permission:roles:roles:read")]
public class PermissionsController : ControllerBase
{
    private readonly IRoleService _roleService;

    public PermissionsController(IRoleService roleService) => _roleService = roleService;

    /// <summary>List all available system permissions grouped by module and feature.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PermissionModuleDto>>> Get(CancellationToken ct)
    {
        var permissions = await _roleService.GetPermissionsGroupedAsync(ct);
        return Ok(permissions);
    }
}
```

#### 2. `backend/src/Api/Controllers/RolesController.cs`:
```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RecruitOps.Application.DTOs;
using RecruitOps.Application.Interfaces;

namespace RecruitOps.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RolesController : ControllerBase
{
    private readonly IRoleService _roleService;

    public RolesController(IRoleService roleService) => _roleService = roleService;

    /// <summary>List all system and custom tenant roles.</summary>
    [HttpGet]
    [Authorize(Policy = "permission:roles:roles:read")]
    public async Task<ActionResult<IReadOnlyList<RoleListItemDto>>> Get(CancellationToken ct)
    {
        var roles = await _roleService.GetRolesAsync(ct);
        return Ok(roles);
    }

    /// <summary>Get role details with assigned permission codes.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = "permission:roles:roles:read")]
    public async Task<ActionResult<RoleDetailDto>> GetById(Guid id, CancellationToken ct)
    {
        var role = await _roleService.GetRoleByIdAsync(id, ct);
        return role is null ? NotFound() : Ok(role);
    }

    /// <summary>Create a custom tenant role with assigned permissions.</summary>
    [HttpPost]
    [Authorize(Policy = "permission:roles:roles:create")]
    public async Task<ActionResult<RoleDetailDto>> Create(CreateRoleRequest request, CancellationToken ct)
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
    [Authorize(Policy = "permission:roles:roles:update")]
    public async Task<ActionResult<RoleDetailDto>> Update(Guid id, UpdateRoleRequest request, CancellationToken ct)
    {
        try
        {
            var updated = await _roleService.UpdateRoleAsync(id, request, ct);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Cannot update role", Detail = ex.Message });
        }
    }

    /// <summary>Delete custom tenant role (enforcing system role protection).</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "permission:roles:roles:delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try
        {
            var deleted = await _roleService.DeleteRoleAsync(id, ct);
            return deleted ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails { Title = "Cannot delete role", Detail = ex.Message });
        }
    }
}
```

---

### 3.6 MediatR CQRS Alternative Design

If the project mandates MediatR handlers over direct service interfaces in `Application`, the CQRS feature folder structure under `backend/src/Application/Features/Roles` is as follows:

```
backend/src/Application/Features/Roles/
├── Queries/
│   ├── GetPermissionsGroupedQuery.cs
│   ├── GetRolesQuery.cs
│   └── GetRoleByIdQuery.cs
└── Commands/
    ├── CreateRoleCommand.cs
    ├── UpdateRoleCommand.cs
    └── DeleteRoleCommand.cs
```

Each query/command maps directly to the logic specified in `RoleService.cs`.

---

## 4. Caveats

1. **Permission Authorization Handler**: Explorer 1 is designing the dynamic `PermissionAuthorizationHandler` and `PermissionRequirement` policy evaluator. The controllers designed above use `[Authorize(Policy = "permission:roles:roles:read")]`. Until the dynamic handler is registered, policies can map to `Policies.AdminOnly` or custom policy definitions in `Program.cs`.
2. **User Role Assignment**: User assignment to custom roles (`POST /api/users` and `PUT /api/users/{id}/role`) is being designed by Explorer 3 (M3.3). `RoleService.DeleteRoleAsync` checks `role.Users.Count(u => u.IsActive)` to safeguard against deleting active assigned roles.
3. **Assumptions**:
   - SuperAdmin system role is pre-seeded with all permissions and cannot be modified by any tenant endpoint.
   - All permission codes follow the canonical standard `permission:<module>:<feature>:<action>`.

---

## 5. Conclusion

The designed API surface satisfies Requirement R3 cleanly:
- `GET /api/permissions`: Returns all 34 canonical permissions grouped hierarchically by module and feature.
- `GET /api/roles`: Returns all system roles + tenant custom roles.
- `GET /api/roles/{id}`: Returns complete role details and permission code lists.
- `POST /api/roles`: Creates custom tenant roles with permission validation and tenant isolation.
- `PUT /api/roles/{id}`: Updates custom role metadata & permission set with immutability protection for system roles.
- `DELETE /api/roles/{id}`: Deletes custom role with active user assignment checks and system role protection.

All code locations, DTOs, validation rules, and error handling behaviors are specified and ready for implementation by Worker subagents.

---

## 6. Verification Method

### 6.1 Build Verification Command
```powershell
dotnet build backend/RecruitOps.sln
```
*Expected*: 0 Errors, 0 Warnings.

### 6.2 Unit & Integration Test Verification Command
```powershell
dotnet test backend/tests/RecruitOps.Domain.Tests
dotnet test backend/tests/RecruitOps.Api.Tests
```

### 6.3 Verification Matrix for Custom Integration Tests (`RolesAndPermissionsApiTests.cs`)

| Endpoint | Test Case | Inputs / Scenario | Expected Status Code | Expected Body Assertion |
|---|---|---|---|---|
| `GET /api/permissions` | Unauthenticated | No Auth Header | `401 Unauthorized` | Empty / Standard 401 |
| `GET /api/permissions` | Authorized Admin | Valid Bearer Token | `200 OK` | List of 9 modules, total 34 permissions |
| `GET /api/roles` | Authorized User | Valid Bearer Token | `200 OK` | Includes 7 System roles + tenant custom roles |
| `GET /api/roles/{id}` | Non-existent ID | Random Guid | `404 Not Found` | - |
| `GET /api/roles/{id}` | Existing System Role ID | SuperAdmin Role Guid | `200 OK` | `isSystemRole: true`, 34 permission codes |
| `POST /api/roles` | Duplicate Role Name | Existing name in tenant | `409 Conflict` | ProblemDetails detail contains name collision |
| `POST /api/roles` | Invalid Permission Code | `permission:invalid:code` | `400 Bad Request` or `409` | ProblemDetails lists invalid permission codes |
| `POST /api/roles` | Valid Request | "Lead Recruiter", 3 valid codes | `201 Created` | `Location` header, `isSystemRole: false` |
| `PUT /api/roles/{id}` | System Role ID | SuperAdmin Role Guid | `400 Bad Request` | ProblemDetails: "System roles are immutable..." |
| `PUT /api/roles/{id}` | Valid Custom Role ID | Updated name & permissions | `200 OK` | Updated permission codes list |
| `DELETE /api/roles/{id}`| System Role ID | Admin Role Guid | `409 Conflict` or `400` | ProblemDetails: "Pre-configured system roles..." |
| `DELETE /api/roles/{id}`| Custom Role with Users | Role assigned to active user | `409 Conflict` | ProblemDetails: "Cannot delete role ... assigned to user(s)" |
| `DELETE /api/roles/{id}`| Valid Custom Role | Unassigned custom role | `204 No Content` | Role removed from DB |
