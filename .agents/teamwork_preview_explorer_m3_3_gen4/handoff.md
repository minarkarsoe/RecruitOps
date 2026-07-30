# Handoff Report — Architectural Specification for User Account Management APIs (Milestone 3)

## 1. Observation

Direct observations from codebase inspection across `src/Api/`, `src/Application/`, `src/Domain/`, `src/Infrastructure/`, and `tests/`:

1. **Existing `UsersController.cs` (`src/Api/Controllers/UsersController.cs`)**:
   - `UsersController` currently contains only two endpoints:
     - `GET /api/users` (lines 42-58): Decorated with `[Authorize(Policy = Policies.AdminOnly)]`. Returns `IReadOnlyList<UserListItemDto>` unpaged.
     - `GET /api/users/selectable` (lines 79-100): Decorated with `[Authorize(Policy = Policies.RecruitmentStaff)]`. Returns `IReadOnlyList<SelectableUserDto>` containing `Id`, `DisplayName`, `Role`.
   - Explicit comment on EF Core 10 translation at lines 84-87:
     ```csharp
     // Two-step (query in SQL, project in memory) — EF Core 10 will not translate
     // `enum.ToString()` into SQL, so the ToString happens here, after materialisation.
     // `Get` above projects the enum inside the query and has never been run against
     // Postgres; do not copy that shape.
     ```
   - Class-level attribute is `[Authorize]` without policy (lines 28-29), avoiding additive policy override bugs (ADR-0019).

2. **Domain Entities (`src/Domain/Entities/`)**:
   - `User.cs` (lines 7-22): Implements `BaseEntity`, `ITenantScoped`. Properties: `Guid TenantId`, `string Email`, `string DisplayName`, `string PasswordHash`, `UserRole Role` (system enum), `bool IsActive`, `Guid? RoleId` (FK to `Role`), `Role? CustomRole` (navigation property), `bool IsSuperAdmin`.
   - `Role.cs` (lines 6-18): Implements `BaseEntity`. Properties: `Guid? TenantId`, `string Name`, `string Code`, `string Description`, `bool IsSystemRole`, `bool IsSuperAdmin`, `bool IsActive`, collections `RolePermissions` and `Users`.
   - `Permission.cs` (lines 6-16): Implements `BaseEntity`. Properties: `string Module`, `string Feature`, `string Action`, `string Name`, `string Description`, `string Code` (unique index e.g., `permission:users:users:read`).
   - `RolePermission.cs` (lines 4-13): Join entity connecting `RoleId` and `PermissionId`.

3. **DbContext & Configuration (`src/Infrastructure/Persistence/AppDbContext.cs`)**:
   - Line 112: `e.Property(x => x.Role).HasConversion<string>().HasMaxLength(30);`
   - Line 117: `e.HasIndex(x => x.Email).IsUnique();` (Globally unique index).
   - Lines 119-122: `e.HasOne(x => x.CustomRole).WithMany(r => r.Users).HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);`
   - Line 429: `builder.Entity<User>().HasQueryFilter(e => e.TenantId == _tenant.TenantId);`
   - Lines 58-82: `StampTenantAndTimestamps()` automatically assigns `TenantId` from `_tenant.TenantId` on entity insertion when empty.

4. **Dynamic RBAC & Seed Data (`src/Infrastructure/Persistence/RbacSeedData.cs`)**:
   - Lines 51-54: Canonical permission codes for User management:
     - `"permission:users:users:read"` ("Read Users")
     - `"permission:users:users:create"` ("Create Users")
     - `"permission:users:users:update"` ("Update Users")
     - `"permission:users:users:delete"` ("Delete Users")
   - System Roles (`SuperAdmin`, `Admin`, `HrDirector`, `Recruiter`, `HiringManager`, `Approver`, `Interviewer`) seeded with default permission mapping.

5. **Existing DTOs (`src/Application/DTOs/UserListItemDto.cs`)**:
   - `UserListItemDto`: `record UserListItemDto(Guid Id, string Email, string DisplayName, string Role)`
   - `SelectableUserDto`: `record SelectableUserDto(Guid Id, string DisplayName, string Role)`

6. **Authentication & Service Infrastructure (`src/Infrastructure/Services/`)**:
   - `AuthService.cs`: Uses `IPasswordHasher<User>` for password hashing and verification.
   - `JwtTokenService.cs`: Issues JWTs containing `sub`, `tenant_id`, `ClaimTypes.Role`, `email`, `name`.
   - `DepartmentService.cs`: Demonstrates pattern for soft deactivation guard checks (checking active work in progress before deactivating, lines 125-135).

7. **Test Suite Baseline (`tests/RecruitOps.Api.Tests/UserDirectoryTests.cs`)**:
   - All 181 unit and integration tests currently pass (`dotnet test backend/RecruitOps.sln`).
   - `UserDirectoryTests.cs` explicitly verifies policy boundaries for `GET /api/users` (AdminOnly) and `GET /api/users/selectable` (RecruitmentStaff).

---

## 2. Logic Chain

1. **Endpoint Boundary & Compatibility**:
   - The user management module requires expanding `UsersController` from directory reading to full account lifecycle operations: paged listing (`GET /api/users`), user detail (`GET /api/users/{id}`), user creation (`POST /api/users`), metadata/role update (`PUT /api/users/{id}`), deactivation (`PUT /api/users/{id}/deactivate`), and reactivation (`PUT /api/users/{id}/reactivate`).
   - To avoid breaking ADR-0019 panel picker tests, `GET /api/users/selectable` must remain intact with `[Authorize(Policy = Policies.RecruitmentStaff)]`.
   - `GET /api/users` must be upgraded to support query parameter filtering while retaining default paged listing behavior under `[Authorize(Policy = Policies.AdminOnly)]`.

2. **EF Core 10 Translation Safeguards**:
   - Observation 1 notes that EF Core 10 fails SQL translation when `Enum.ToString()` is called directly inside an `IQueryable.Select(...)` block when targeting database providers like PostgreSQL.
   - Therefore, all LINQ queries in `UserService` must execute a two-step materialization strategy:
     - **Step 1 (SQL)**: Query database, apply filters (`.Where(...)`), count (`.CountAsync(...)`), page (`.Skip(...).Take(...)`), and project primitive/enum values into anonymous types or intermediate tuples.
     - **Step 2 (Memory)**: Call `.ToListAsync()`, then map intermediate objects into DTOs in memory where `.ToString()` and string formatting evaluate safely.

3. **System Role & Dynamic RBAC Mapping**:
   - `User` entity has both legacy `Role` enum (`UserRole`) and dynamic `RoleId` (`Role` entity FK).
   - When creating or updating a user:
     - If `RoleId` is specified: validate existence against `_db.Roles`. Map `User.RoleId = roleId`. If the referenced role code corresponds to a system role (`Admin`, `HrDirector`, `Recruiter`, `HiringManager`, `Approver`), set `User.Role` to that `UserRole` enum. For custom roles, default `User.Role` to `UserRole.Recruiter` as fallback.
     - If `RoleId` is null but legacy `Role` string is specified: parse `Role` string into `UserRole` enum, lookup matching system `Role` entity by `Code == Role` from `_db.Roles`, and set both `User.Role` and `User.RoleId`.
   - Permission derivation in `GET /api/users/{id}`:
     - If `CustomRole` navigation property is present: permissions are retrieved from `CustomRole.RolePermissions.Select(rp => rp.Permission.Code)`.
     - If `CustomRole` is null: permissions are derived from `RbacSeedData.GetSystemRoles()` matching `User.Role.ToString()`.

4. **Tenant Isolation & Security Safeguards**:
   - `AppDbContext` automatically applies `TenantId` query filter (`builder.Entity<User>().HasQueryFilter(e => e.TenantId == _tenant.TenantId)`).
   - `AppDbContext.SaveChangesAsync()` automatically stamps `TenantId` on new users.
   - Email uniqueness check must use `.IgnoreQueryFilters()` to enforce global uniqueness across login (Observation 3).
   - User deactivation must enforce safety guards:
     - Prevent self-deactivation (`id == _currentUser.UserId`).
     - Prevent deactivating the last active Admin account in the tenant (`activeAdminCount <= 1`).
     - Reject invalid state transitions (deactivating already inactive user, reactivating already active user).

---

## 3. Architectural Specification for User Account Management APIs

### 3.1 Interface & Service Contracts

#### `IUserService` (`src/Application/Interfaces/IUserService.cs`)
```csharp
namespace RecruitOps.Application.Interfaces;

using RecruitOps.Application.DTOs;

public interface IUserService
{
    Task<PagedResult<UserListItemDto>> GetUsersAsync(UserQueryParameters query, CancellationToken ct = default);
    Task<UserDetailDto?> GetUserByIdAsync(Guid id, CancellationToken ct = default);
    Task<UserDetailDto> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default);
    Task<UserDetailDto?> UpdateUserAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default);
    Task<UserDetailDto?> SetUserActiveAsync(Guid id, bool isActive, CancellationToken ct = default);
}
```

---

### 3.2 Data Transfer Objects (DTOs)

#### `UserQueryParameters.cs` (`src/Application/DTOs/UserQueryParameters.cs`)
```csharp
namespace RecruitOps.Application.DTOs;

public record UserQueryParameters(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    Guid? RoleId = null,
    bool? IsActive = null);
```

#### `PagedResult.cs` (`src/Application/DTOs/PagedResult.cs`)
```csharp
namespace RecruitOps.Application.DTOs;

public record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
```

#### `UserListItemDto.cs` (Updated) (`src/Application/DTOs/UserListItemDto.cs`)
```csharp
namespace RecruitOps.Application.DTOs;

public record UserListItemDto(
    Guid Id,
    string Email,
    string DisplayName,
    string Role,
    Guid? RoleId = null,
    string? RoleName = null,
    bool IsActive = true,
    DateTimeOffset CreatedAt = default);
```

#### `UserDetailDto.cs` (`src/Application/DTOs/UserDetailDto.cs`)
```csharp
namespace RecruitOps.Application.DTOs;

public record UserRoleInfoDto(
    Guid Id,
    string Name,
    string Code,
    string Description,
    bool IsSystemRole,
    bool IsSuperAdmin);

public record UserDetailDto(
    Guid Id,
    string Email,
    string DisplayName,
    string Role,
    Guid? RoleId,
    UserRoleInfoDto? RoleDetails,
    IReadOnlyList<string> Permissions,
    bool IsActive,
    bool IsSuperAdmin,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
```

#### `CreateUserRequest.cs` (`src/Application/DTOs/CreateUserRequest.cs`)
```csharp
namespace RecruitOps.Application.DTOs;

public record CreateUserRequest(
    string Email,
    string DisplayName,
    string Password,
    Guid? RoleId = null,
    string? Role = null);
```

#### `UpdateUserRequest.cs` (`src/Application/DTOs/UpdateUserRequest.cs`)
```csharp
namespace RecruitOps.Application.DTOs;

public record UpdateUserRequest(
    string DisplayName,
    Guid? RoleId = null,
    string? Role = null);
```

---

### 3.3 Endpoint Detailed Design & Controller Routing

#### Controller Class (`src/Api/Controllers/UsersController.cs`)
```csharp
namespace RecruitOps.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RecruitOps.Api.Auth;
using RecruitOps.Application.DTOs;
using RecruitOps.Application.Interfaces;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly AppDbContext _db; // Retained for Selectable endpoint

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
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<UserDetailDto>> GetById(Guid id, CancellationToken ct)
    {
        var user = await _userService.GetUserByIdAsync(id, ct);
        return user is null ? NotFound(new ProblemDetails { Title = "User not found", Detail = $"No user found with ID '{id}'." }) : Ok(user);
    }

    /// <summary>Create a new user account.</summary>
    [HttpPost]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<UserDetailDto>> Create(CreateUserRequest request, CancellationToken ct)
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
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<UserDetailDto>> Update(Guid id, UpdateUserRequest request, CancellationToken ct)
    {
        try
        {
            var updated = await _userService.UpdateUserAsync(id, request, ct);
            return updated is null ? NotFound(new ProblemDetails { Title = "User not found", Detail = $"No user found with ID '{id}'." }) : Ok(updated);
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
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<UserDetailDto>> Deactivate(Guid id, CancellationToken ct)
    {
        try
        {
            var result = await _userService.SetUserActiveAsync(id, isActive: false, ct);
            return result is null ? NotFound(new ProblemDetails { Title = "User not found", Detail = $"No user found with ID '{id}'." }) : Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails { Title = "Cannot deactivate user", Detail = ex.Message });
        }
    }

    /// <summary>Reactivate user account.</summary>
    [HttpPut("{id:guid}/reactivate")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<UserDetailDto>> Reactivate(Guid id, CancellationToken ct)
    {
        try
        {
            var result = await _userService.SetUserActiveAsync(id, isActive: true, ct);
            return result is null ? NotFound(new ProblemDetails { Title = "User not found", Detail = $"No user found with ID '{id}'." }) : Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails { Title = "Cannot reactivate user", Detail = ex.Message });
        }
    }
}
```

---

### 3.4 Request Validation Rules & Error Mapping

| Field / Action | Rule / Constraint | Failure HTTP Code | Error Response Detail |
|---|---|---|---|
| `CreateUserRequest.Email` | Required, non-empty, valid email format, max 256 chars. | 400 Bad Request | `"Email format is invalid."` |
| `CreateUserRequest.Email` | Globally unique across tenant & system (`IgnoreQueryFilters()`). | 409 Conflict | `"A user with email '{email}' already exists."` |
| `CreateUserRequest.DisplayName` | Required, non-empty, max 200 chars. | 400 Bad Request | `"DisplayName is required and cannot exceed 200 characters."` |
| `CreateUserRequest.Password` | Required, min 8 chars, complexity check (uppercase, lowercase, digit). | 400 Bad Request | `"Password must be at least 8 characters long and include uppercase, lowercase, and digit."` |
| `CreateUserRequest.RoleId` | If provided, must exist in `Roles` table and be active (`IsActive == true`). | 409 Conflict | `"Role with ID '{roleId}' was not found or is inactive."` |
| `CreateUserRequest.Role` | If `RoleId` is null, `Role` string must parse to valid `UserRole` enum or system role code. | 400 Bad Request | `"Role '{role}' is invalid."` |
| `UpdateUserRequest.DisplayName` | Required, non-empty, max 200 chars. | 400 Bad Request | `"DisplayName is required."` |
| `Deactivate (self)` | `id == currentUser.UserId` | 409 Conflict | `"You cannot deactivate your own account."` |
| `Deactivate (last admin)` | Active Admin count in tenant <= 1 when target user is Admin/SuperAdmin. | 409 Conflict | `"Cannot deactivate the last active Administrator account."` |
| `Deactivate (already inactive)` | Target user `IsActive == false` | 409 Conflict | `"User account is already inactive."` |
| `Reactivate (already active)` | Target user `IsActive == true` | 409 Conflict | `"User account is already active."` |

---

### 3.5 EF Core 10 Two-Step LINQ Query Safeguards

#### 1. Paged Query Implementation Example (`GetUsersAsync`):
```csharp
public async Task<PagedResult<UserListItemDto>> GetUsersAsync(
    UserQueryParameters queryParams, CancellationToken ct = default)
{
    int page = queryParams.Page < 1 ? 1 : queryParams.Page;
    int pageSize = queryParams.PageSize switch
    {
        < 1 => 20,
        > 100 => 100,
        _ => queryParams.PageSize
    };

    var query = _db.Users.AsNoTracking();

    if (!string.IsNullOrWhiteSpace(queryParams.Search))
    {
        var search = queryParams.Search.Trim().ToLower();
        query = query.Where(u => u.Email.ToLower().Contains(search) || u.DisplayName.ToLower().Contains(search));
    }

    if (queryParams.RoleId.HasValue)
    {
        query = query.Where(u => u.RoleId == queryParams.RoleId.Value);
    }

    if (queryParams.IsActive.HasValue)
    {
        query = query.Where(u => u.IsActive == queryParams.IsActive.Value);
    }

    int totalCount = await query.CountAsync(ct);

    // STEP 1: Query SQL with primitive projection
    var rows = await query
        .OrderBy(u => u.DisplayName)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .Select(u => new
        {
            u.Id,
            u.Email,
            u.DisplayName,
            u.Role,
            u.RoleId,
            RoleName = u.CustomRole != null ? u.CustomRole.Name : null,
            u.IsActive,
            u.CreatedAt
        })
        .ToListAsync(ct);

    // STEP 2: Memory transformation to DTO (ToString() happens safely in memory)
    var items = rows.Select(r => new UserListItemDto(
        r.Id,
        r.Email,
        r.DisplayName,
        r.Role.ToString(),
        r.RoleId,
        r.RoleName ?? r.Role.ToString(),
        r.IsActive,
        r.CreatedAt
    )).ToList();

    int totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
    return new PagedResult<UserListItemDto>(items, page, pageSize, totalCount, totalPages);
}
```

#### 2. User Detail & Permission Resolution Example (`GetUserByIdAsync`):
```csharp
public async Task<UserDetailDto?> GetUserByIdAsync(Guid id, CancellationToken ct = default)
{
    var user = await _db.Users
        .AsNoTracking()
        .Include(u => u.CustomRole)
            .ThenInclude(r => r!.RolePermissions)
                .ThenInclude(rp => rp.Permission)
        .FirstOrDefaultAsync(u => u.Id == id, ct);

    if (user is null) return null;

    UserRoleInfoDto? roleDetails = null;
    List<string> permissions = new();

    if (user.CustomRole is not null)
    {
        roleDetails = new UserRoleInfoDto(
            user.CustomRole.Id,
            user.CustomRole.Name,
            user.CustomRole.Code,
            user.CustomRole.Description,
            user.CustomRole.IsSystemRole,
            user.CustomRole.IsSuperAdmin
        );

        permissions = user.CustomRole.RolePermissions
            .Select(rp => rp.Permission.Code)
            .Distinct()
            .OrderBy(p => p)
            .ToList();
    }
    else
    {
        // System role fallback permission mapping
        var systemRoleCode = user.Role.ToString();
        var systemRoleSeed = RbacSeedData.GetSystemRoles()
            .FirstOrDefault(r => r.Code.Equals(systemRoleCode, StringComparison.OrdinalIgnoreCase));

        if (systemRoleSeed is not null)
        {
            permissions = systemRoleSeed.PermissionCodes.OrderBy(p => p).ToList();
        }
    }

    return new UserDetailDto(
        user.Id,
        user.Email,
        user.DisplayName,
        user.Role.ToString(),
        user.RoleId,
        roleDetails,
        permissions,
        user.IsActive,
        user.IsSuperAdmin,
        user.CreatedAt,
        user.UpdatedAt
    );
}
```

---

## 4. Caveats

1. **No Source Modifications Made**: This investigation was strictly read-only. No `.cs` implementation files outside `.agents/` were modified.
2. **Email Scope Limitation**: Email uniqueness is enforced globally per `User.Email` index (`ADR-0002` known single-tenant-per-email limitation). Multi-tenant accounts sharing an email address require tenant selector support in future auth iterations.
3. **Legacy `UserRole` Enum Synchronization**: Custom dynamic roles created by administrators without a matching legacy `UserRole` enum will map to `UserRole.Recruiter` as system fallback to preserve JWT claim compatibility (`ClaimTypes.Role`).

---

## 5. Conclusion

The specification provides a complete, robust, and backwards-compatible architectural blueprint for the User Account Management APIs in RecruitOps backend (.NET 10). It addresses all scope items:
- Paged user listing with search, RoleId filter, and IsActive filter.
- Detailed user retrieval with dynamic role details and permissions resolution.
- Secure user creation with password hashing and email uniqueness verification.
- User metadata and role assignment updates.
- Guard-protected soft deactivation and reactivation workflows.
- Clean EF Core 10 LINQ translation safeguards using two-step materialization to prevent runtime SQL translation failures.

---

## 6. Verification Method

1. **Build Verification**:
   Execute the following command in the workspace root:
   ```powershell
   dotnet build backend/RecruitOps.sln
   ```
   *Expected Output*: Build succeeded with 0 errors.

2. **Test Suite Verification**:
   Execute the test suite to verify existing regression baseline:
   ```powershell
   dotnet test backend/RecruitOps.sln
   ```
   *Expected Output*: Passed 181 tests (48 Domain + 133 API tests).

3. **Specification Implementation Inspection**:
   Inspect the proposed specification against target contract paths:
   - `backend/src/Api/Controllers/UsersController.cs`
   - `backend/src/Application/Interfaces/IUserService.cs`
   - `backend/src/Infrastructure/Services/UserService.cs`
   - `backend/src/Application/DTOs/`

4. **Invalidation Conditions**:
   - Any modification that removes or alters `GET /api/users/selectable` will fail `UserDirectoryTests.cs` (ADR-0019).
   - Any direct `Enum.ToString()` call within LINQ `.Select()` projected into SQL will fail when executed against PostgreSQL.
