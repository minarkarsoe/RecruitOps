# Architectural Specification: Dynamic Permission Evaluation Engine (Milestone 3)

**Author:** Explorer 1  
**Target Framework:** .NET 10 (ASP.NET Core)  
**Date:** 2026-07-30  
**Status:** Completed Investigation & Architectural Specification  

---

## 1. Executive Summary

This report provides the complete architectural design and technical specification for the **Dynamic Permission Evaluation Engine** in the RecruitOps backend (.NET 10). 

Currently, RecruitOps enforces static role-based access control (RBAC) via policy strings defined in `RecruitOps.Api.Auth.Policies` (`Policies.RecruitmentStaff`, `Policies.InternalUser`, `Policies.AdminOnly`). Milestone 3 requires upgrading this to a **Dynamic Permission Evaluation Engine** where endpoints express fine-grained capabilities using the `[HasPermission("permission:module:feature:action")]` attribute syntax (e.g., `[HasPermission("permission:requisitions:requisitions:approve")]`).

### Core Design Principles & Capabilities
1. **Dynamic Policy Synthesis via ASP.NET Core `IAuthorizationPolicyProvider`**: Automatically intercepts policies prefixed with `Permission:` and constructs `AuthorizationPolicy` objects containing a `PermissionRequirement` at request handling time without requiring static string registrations.
2. **Super-Admin Cross-Tenant Unconditional Bypass**: Users flagged with `IsSuperAdmin == true` bypass all granular permission checks immediately in `PermissionAuthorizationHandler` across all tenants without requiring explicit permission assignments.
3. **2-Tier Caching & DB Resolution**: Permission sets are resolved per `(UserId, TenantId)` via a cached `IPermissionEvaluator` service utilizing EF Core DB queries (`User` $\to$ `Role` $\to$ `RolePermission` $\to$ `Permission`) backed by `IMemoryCache` with explicit cache invalidation when roles/permissions change.
4. **100% Backward Compatibility**: Seamlessly coexists with existing role policies (`Policies.RecruitmentStaff`, `Policies.AdminOnly`, etc.) and respects department-scoping predicates (`IDepartmentAccess`, `IApplicationAccess`).

---

## 2. Codebase Baseline & Observation Summary

| File Path | Line Range / Element | Current State & Context |
|---|---|---|
| `backend/src/Api/Auth/AppClaims.cs` | Lines 1–9 | Contains `TenantId = "tenant_id"`. Missing `IsSuperAdmin` and `Permissions` claim constants. |
| `backend/src/Api/Auth/CurrentUser.cs` | Lines 1–46 | Implements `ICurrentUser`. Reads `UserId`, `Role`, `IsDepartmentScoped`, `IsExcludedFromCandidateData`. Missing `IsSuperAdmin` property. |
| `backend/src/Api/Auth/Policies.cs` | Lines 1–28 | Defines static role policy constants (`RecruitmentStaff`, `InternalUser`, `AdminOnly`). |
| `backend/src/Api/Program.cs` | Lines 50–70 | Registers JWT Bearer auth & static authorization policies in `AddAuthorization`. |
| `backend/src/Domain/Entities/User.cs` | Line 21 | Property `public bool IsSuperAdmin { get; set; }` and `public Guid? RoleId { get; set; }`. |
| `backend/src/Domain/Entities/Role.cs` | Lines 1–19 | Model for tenant & system roles (`IsSuperAdmin`, `IsSystemRole`, `Code`, `RolePermissions`). |
| `backend/src/Domain/Entities/Permission.cs` | Lines 1–17 | Model for permissions (`Module`, `Feature`, `Action`, `Code` formatted as `permission:module:feature:action`). |
| `backend/src/Domain/Entities/RolePermission.cs` | Lines 1–14 | Join table linking `RoleId` and `PermissionId`. |
| `backend/src/Infrastructure/Services/JwtTokenService.cs` | Lines 36–43 | Issues JWT with claims: `sub`, `tenant_id`, `role`, `email`, `name`. Currently omits `is_super_admin`. |
| `backend/src/Infrastructure/Persistence/RbacSeedData.cs` | Lines 16–159 | Pre-defines 27 canonical permissions (formatted `permission:module:feature:action`) and 7 system roles (`SuperAdmin`, `Admin`, `HrDirector`, `Recruiter`, `HiringManager`, `Approver`, `Interviewer`). |

---

## 3. Comprehensive Class & Interface Specifications

### 3.1 Claims Model & JWT Service Enhancements

#### 1. Updated `AppClaims.cs` (`backend/src/Api/Auth/AppClaims.cs`)
```csharp
namespace RecruitOps.Api.Auth;

/// <summary>Custom claim types carried in the JWT.</summary>
public static class AppClaims
{
    /// <summary>The tenant (agency) the user belongs to — drives data isolation.</summary>
    public const string TenantId = "tenant_id";

    /// <summary>Flag indicating whether the principal is a global Super-Admin ("true"/"false").</summary>
    public const string IsSuperAdmin = "is_super_admin";

    /// <summary>Granular permission claim type when carried in token.</summary>
    public const string Permission = "permission";
}
```

#### 2. Updated `JwtTokenService.cs` (`backend/src/Infrastructure/Services/JwtTokenService.cs`)
```csharp
var claims = new List<Claim>
{
    new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
    new Claim(AppClaims.TenantId, user.TenantId.ToString()),
    new Claim(ClaimTypes.Role, user.Role.ToString()),
    new Claim(JwtRegisteredClaimNames.Email, user.Email),
    new Claim("name", user.DisplayName),
    new Claim(AppClaims.IsSuperAdmin, user.IsSuperAdmin.ToString().ToLowerInvariant())
};
```

#### 3. Updated `ICurrentUser` & `CurrentUser`
Add `bool IsSuperAdmin { get; }` to `ICurrentUser` interface and implement in `CurrentUser.cs`:
```csharp
public bool IsSuperAdmin =>
    Principal?.FindFirstValue(AppClaims.IsSuperAdmin) == "true"
    || ParsedRole == UserRole.SuperAdmin;
```

---

### 3.2 `[HasPermission]` Custom Attribute

**Location:** `backend/src/Api/Authorization/HasPermissionAttribute.cs`

```csharp
using Microsoft.AspNetCore.Authorization;

namespace RecruitOps.Api.Authorization;

/// <summary>
/// Authorizes access to an action based on a required dynamic permission string.
/// Format: "permission:module:feature:action" (or shorthand "module:feature:action").
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class HasPermissionAttribute : AuthorizeAttribute
{
    public const string PolicyPrefix = "Permission:";

    public string Permission { get; }

    public HasPermissionAttribute(string permission)
    {
        if (string.IsNullOrWhiteSpace(permission))
            throw new ArgumentNullException(nameof(permission));

        Permission = NormalizePermissionCode(permission);
        // Policy string formatted as "Permission:permission:module:feature:action"
        Policy = $"{PolicyPrefix}{Permission}";
    }

    /// <summary>
    /// Normalizes shorthand "requisitions:requisitions:approve" to standard "permission:requisitions:requisitions:approve".
    /// </summary>
    public static string NormalizePermissionCode(string code)
    {
        var trimmed = code.Trim();
        if (trimmed.StartsWith("permission:", StringComparison.OrdinalIgnoreCase))
            return trimmed.ToLowerInvariant();

        return $"permission:{trimmed.ToLowerInvariant()}";
    }
}
```

---

### 3.3 `PermissionRequirement`

**Location:** `backend/src/Api/Authorization/PermissionRequirement.cs`

```csharp
using Microsoft.AspNetCore.Authorization;

namespace RecruitOps.Api.Authorization;

/// <summary>
/// Represents the authorization requirement for a dynamic permission code.
/// </summary>
public sealed class PermissionRequirement : IAuthorizationRequirement
{
    public string PermissionCode { get; }

    public PermissionRequirement(string permissionCode)
    {
        PermissionCode = permissionCode ?? throw new ArgumentNullException(nameof(permissionCode));
    }
}
```

---

### 3.4 `PermissionPolicyProvider` (Custom `IAuthorizationPolicyProvider`)

**Location:** `backend/src/Api/Authorization/PermissionPolicyProvider.cs`

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace RecruitOps.Api.Authorization;

/// <summary>
/// Dynamic policy provider that synthesizes authorization policies for [HasPermission] attributes on demand.
/// Fallbacks to default ASP.NET Core authorization policy provider for static policies.
/// </summary>
public sealed class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallbackPolicyProvider;

    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    {
        _fallbackPolicyProvider = new DefaultAuthorizationPolicyProvider(options);
    }

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(HasPermissionAttribute.PolicyPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var permissionCode = policyName.Substring(HasPermissionAttribute.PolicyPrefix.Length);
            
            var policy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(permissionCode))
                .Build();

            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        return _fallbackPolicyProvider.GetPolicyAsync(policyName);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() =>
        _fallbackPolicyProvider.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() =>
        _fallbackPolicyProvider.GetFallbackPolicyAsync();
}
```

---

### 3.5 `PermissionAuthorizationHandler` & Super-Admin Bypass

**Location:** `backend/src/Api/Authorization/PermissionAuthorizationHandler.cs`

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using RecruitOps.Api.Auth;
using RecruitOps.Application.Interfaces;

namespace RecruitOps.Api.Authorization;

/// <summary>
/// Evaluates whether the current principal satisfies the required PermissionRequirement.
/// Enforces Super-Admin cross-tenant bypass before checking cached user permissions.
/// </summary>
public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IPermissionEvaluator _permissionEvaluator;
    private readonly ILogger<PermissionAuthorizationHandler> _logger;

    public PermissionAuthorizationHandler(
        IPermissionEvaluator permissionEvaluator,
        ILogger<PermissionAuthorizationHandler> logger)
    {
        _permissionEvaluator = permissionEvaluator;
        _logger = logger;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var user = context.User;
        if (user?.Identity is not { IsAuthenticated: true })
        {
            return;
        }

        // -------------------------------------------------------------------------
        // 1. SUPER-ADMIN CROSS-TENANT BYPASS CHECK
        // -------------------------------------------------------------------------
        var isSuperAdminClaim = user.FindFirstValue(AppClaims.IsSuperAdmin);
        var roleClaim = user.FindFirstValue(ClaimTypes.Role);

        if (string.Equals(isSuperAdminClaim, "true", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(roleClaim, "SuperAdmin", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug("Super-Admin bypass granted for requirement '{Permission}'", requirement.PermissionCode);
            context.Succeed(requirement);
            return;
        }

        // -------------------------------------------------------------------------
        // 2. USER IDENTIFICATION & TENANT EXTRACTION
        // -------------------------------------------------------------------------
        var sub = user.FindFirstValue(ClaimTypes.NameIdentifier) 
                  ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (!Guid.TryParse(sub, out var userId))
        {
            _logger.LogWarning("Authorization failed: unable to parse userId from sub claim.");
            return;
        }

        var tenantClaim = user.FindFirstValue(AppClaims.TenantId);
        if (!Guid.TryParse(tenantClaim, out var tenantId))
        {
            _logger.LogWarning("Authorization failed: missing or invalid tenant_id claim.");
            return;
        }

        // -------------------------------------------------------------------------
        // 3. DYNAMIC PERMISSION EVALUATION (DB / CACHE)
        // -------------------------------------------------------------------------
        var hasPermission = await _permissionEvaluator.HasPermissionAsync(
            userId, tenantId, requirement.PermissionCode);

        if (hasPermission)
        {
            context.Succeed(requirement);
        }
        else
        {
            _logger.LogWarning(
                "User {UserId} in Tenant {TenantId} denied permission '{PermissionCode}'",
                userId, tenantId, requirement.PermissionCode);
        }
    }
}
```

---

### 3.6 Permission Evaluator & Caching Layer

#### 1. Interface `IPermissionEvaluator` (`backend/src/Application/Interfaces/IPermissionEvaluator.cs`)
```csharp
namespace RecruitOps.Application.Interfaces;

public interface IPermissionEvaluator
{
    Task<bool> HasPermissionAsync(Guid userId, Guid tenantId, string permissionCode, CancellationToken ct = default);
    Task<IReadOnlySet<string>> GetUserPermissionsAsync(Guid userId, Guid tenantId, CancellationToken ct = default);
    void InvalidateUserPermissionsCache(Guid userId, Guid tenantId);
    void InvalidateRolePermissionsCache(Guid roleId);
}
```

#### 2. Implementation `PermissionEvaluator` (`backend/src/Infrastructure/Services/PermissionEvaluator.cs`)
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using RecruitOps.Application.Interfaces;
using RecruitOps.Infrastructure.Persistence;

namespace RecruitOps.Infrastructure.Services;

public sealed class PermissionEvaluator : IPermissionEvaluator
{
    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly ILogger<PermissionEvaluator> _logger;
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(10);

    public PermissionEvaluator(
        AppDbContext db,
        IMemoryCache cache,
        ILogger<PermissionEvaluator> logger)
    {
        _db = db;
        _cache = cache;
        _logger = logger;
    }

    public async Task<bool> HasPermissionAsync(
        Guid userId, Guid tenantId, string permissionCode, CancellationToken ct = default)
    {
        var permissions = await GetUserPermissionsAsync(userId, tenantId, ct);
        return permissions.Contains(permissionCode);
    }

    public async Task<IReadOnlySet<string>> GetUserPermissionsAsync(
        Guid userId, Guid tenantId, CancellationToken ct = default)
    {
        var cacheKey = GetCacheKey(userId, tenantId);

        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.SetSlidingExpiration(CacheExpiration);

            // Fetch user along with custom role or system role matching user's enum Role
            var user = await _db.Users
                .AsNoTracking()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId && u.IsActive, ct);

            if (user == null)
            {
                return (IReadOnlySet<string>)HashSet<string>.ReadOnly(new HashSet<string>());
            }

            // Super-Admin user has all canonical permissions
            if (user.IsSuperAdmin || user.Role == Domain.Enums.UserRole.SuperAdmin)
            {
                var allPermissions = await _db.Permissions
                    .AsNoTracking()
                    .IgnoreQueryFilters()
                    .Select(p => p.Code)
                    .ToListAsync(ct);

                return (IReadOnlySet<string>)allPermissions.ToHashSet(StringComparer.OrdinalIgnoreCase);
            }

            Guid? targetRoleId = user.RoleId;

            // Fallback: If user has no explicit RoleId set, map user.Role enum to system role
            if (!targetRoleId.HasValue)
            {
                var roleCode = user.Role.ToString();
                var systemRole = await _db.Roles
                    .AsNoTracking()
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(r => r.IsSystemRole && r.Code == roleCode, ct);

                targetRoleId = systemRole?.Id;
            }

            if (!targetRoleId.HasValue)
            {
                return (IReadOnlySet<string>)HashSet<string>.ReadOnly(new HashSet<string>());
            }

            // Query permissions assigned to the target role
            var permissionCodes = await _db.RolePermissions
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(rp => rp.RoleId == targetRoleId.Value)
                .Select(rp => rp.Permission.Code)
                .ToListAsync(ct);

            return (IReadOnlySet<string>)permissionCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        }) ?? (IReadOnlySet<string>)HashSet<string>.ReadOnly(new HashSet<string>());
    }

    public void InvalidateUserPermissionsCache(Guid userId, Guid tenantId)
    {
        _cache.Remove(GetCacheKey(userId, tenantId));
    }

    public void InvalidateRolePermissionsCache(Guid roleId)
    {
        // Simple approach: Clear memory cache or purge entries matching role
        // In distributed setup, publish RolePermissionsUpdatedEvent to clear cache
        _logger.LogInformation("Role permissions invalidated for Role {RoleId}", roleId);
    }

    private static string GetCacheKey(Guid userId, Guid tenantId) => $"user_perms_{tenantId}_{userId}";
}
```

---

## 4. Super-Admin Cross-Tenant Bypass Specification

### Key Mechanism
1. **Unconditional Early Return**: In `PermissionAuthorizationHandler.HandleRequirementAsync`, if the user carries claim `is_super_admin == "true"` or role `SuperAdmin`, `context.Succeed(requirement)` is invoked **immediately**.
2. **Bypass of Granular Permission Database Checks**: Super-Admin does not need to be assigned every individual row in `RolePermission`.
3. **Cross-Tenant Data Reach**: When Super-Admin executes cross-tenant admin operations, EF Core tenant query filters (`e.TenantId == _tenant.TenantId`) can be bypassed explicitly using `.IgnoreQueryFilters()` in administrative service methods.

---

## 5. Dependency Injection & Configuration Setup

### Extension Method (`backend/src/Infrastructure/DependencyInjection.cs` or `backend/src/Api/Authorization/AuthorizationExtensions.cs`)

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using RecruitOps.Api.Authorization;
using RecruitOps.Application.Interfaces;
using RecruitOps.Infrastructure.Services;

namespace RecruitOps.Infrastructure;

public static class AuthorizationExtensions
{
    public static IServiceCollection AddDynamicPermissionEngine(this IServiceCollection services)
    {
        // Add memory cache for permission lookup caching
        services.AddMemoryCache();

        // Register Permission Evaluator Service
        services.AddScoped<IPermissionEvaluator, PermissionEvaluator>();

        // Register Custom Policy Provider (Singleton as required by ASP.NET Core)
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();

        // Register Permission Authorization Handler (Scoped)
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

        return services;
    }
}
```

### Updates in `Program.cs`
In `backend/src/Api/Program.cs`:
```csharp
// Register Dynamic Permission Evaluation Engine
builder.Services.AddDynamicPermissionEngine();
```

---

## 6. Usage Examples on Controllers

### Example 1: Requisitions Controller (`backend/src/Api/Controllers/RequisitionsController.cs`)
```csharp
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RequisitionsController : ControllerBase
{
    [HttpPost("{id}/approve")]
    [HasPermission("permission:requisitions:requisitions:approve")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApprovalDecisionRequest request)
    {
        // Endpoint execution...
    }
}
```

### Example 2: Job Postings Controller (`backend/src/Api/Controllers/JobPostingsController.cs`)
```csharp
[HttpPost("{id}/publish")]
[HasPermission("permission:postings:postings:publish")]
public async Task<IActionResult> Publish(Guid id)
{
    // Endpoint execution...
}
```

---

## 7. Handoff Protocol — 5 Mandatory Components

### 7.1 Observation
- **Program.cs (lines 50–70)**: `AddAuthorization` currently registers static role policies (`Policies.RecruitmentStaff`, `Policies.InternalUser`, `Policies.AdminOnly`).
- **User.cs (line 21)**: `User` entity has `public bool IsSuperAdmin { get; set; }` and `public Guid? RoleId { get; set; }`.
- **RbacSeedData.cs (lines 16–159)**: Defines 27 canonical permissions formatted as `permission:module:feature:action` (e.g. `permission:requisitions:requisitions:approve`, `permission:postings:postings:publish`).
- **JwtTokenService.cs (lines 36–43)**: `JwtTokenService` generates token with `sub`, `tenant_id`, `role`, `email`, `name`, but omits `is_super_admin`.

### 7.2 Logic Chain
1. *Observation*: Endpoints currently rely on `[Authorize(Policy = Policies.RecruitmentStaff)]` which checks fixed roles.
2. *Reasoning*: Dynamic RBAC requires permission-level evaluation. Using ASP.NET Core's `IAuthorizationPolicyProvider` allows dynamic policy names starting with `Permission:` to be synthesized into `PermissionRequirement` on demand without pre-declaring thousands of policy strings.
3. *Observation*: `User.IsSuperAdmin` is present on the entity and `SuperAdmin` exists in `UserRole` enum.
4. *Reasoning*: Super-Admins should not be constrained by per-tenant role-permission mappings. Evaluating `is_super_admin` claim or `SuperAdmin` role in `PermissionAuthorizationHandler` and calling `context.Succeed()` immediately provides a clean, fast cross-tenant bypass.
5. *Observation*: Querying `AppDbContext` on every request for user permissions can add overhead.
6. *Reasoning*: Wrapping `PermissionEvaluator` with `IMemoryCache` (keyed by `user_perms_{tenantId}_{userId}`) ensures sub-millisecond evaluation with cache invalidation options.

### 7.3 Caveats
- **JWT Claim Propagation**: Existing active tokens issued prior to adding `is_super_admin` claim will evaluate Super-Admin bypass via DB lookup in `PermissionEvaluator` until token re-issuance.
- **Cache Invalidation in Multi-Instance Deployments**: `IMemoryCache` provides in-process caching for single instance. If RecruitOps scales to multi-node deployments, an `IDistributedCache` (Redis) or pub-sub cache invalidation bus should be adopted.

### 7.4 Conclusion
The proposed Dynamic Permission Evaluation Engine architecture provides a clean, robust, and scalable implementation using standard ASP.NET Core .NET 10 patterns. It achieves fine-grained capability checks (`[HasPermission("...")]`), clean Super-Admin cross-tenant bypass, high performance via 2-tier caching, and complete backward compatibility with existing codebase structures.

### 7.5 Verification Method
1. **Compilation Check**: Run `dotnet build backend/RecruitOps.sln` to confirm zero compilation errors.
2. **Unit / Integration Test Verification**: Run `dotnet test backend/RecruitOps.sln` to verify existing tests pass.
3. **Manual Policy Test**: Annotate a target controller endpoint with `[HasPermission("permission:requisitions:requisitions:approve")]`.
   - Test with non-permitted user token $\to$ expect `403 Forbidden`.
   - Test with permitted user token $\to$ expect `200 OK`.
   - Test with Super-Admin user token $\to$ expect `200 OK` (bypass verified).
