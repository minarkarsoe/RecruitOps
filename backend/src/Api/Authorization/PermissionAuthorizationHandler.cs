using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using RecruitOps.Api.Auth;
using RecruitOps.Application.Interfaces;
using RecruitOps.Infrastructure.Persistence;

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
        var roleClaims = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();

        if (string.Equals(isSuperAdminClaim, "true", StringComparison.OrdinalIgnoreCase) ||
            roleClaims.Any(r => string.Equals(r, "SuperAdmin", StringComparison.OrdinalIgnoreCase)))
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

        var tenantClaim = user.FindFirstValue(AppClaims.TenantId);

        if (Guid.TryParse(sub, out var userId) && Guid.TryParse(tenantClaim, out var tenantId))
        {
            // The token carries a real identity, so the database is authoritative and its
            // answer is final — including when that answer is "no".
            //
            // A claim-based fallback used to run after this on a DENIED result, matching the
            // JWT's role claim against the static RbacSeedData list and granting on a hit.
            // That made the Role Builder unable to *withhold* anything (ADR-0022):
            // AssignRoleToUserAsync sets `user.Role = UserRole.Recruiter` for every custom
            // role (UserService.cs:318-325) — a custom role's generated code never parses as
            // a UserRole, so that is the only reachable branch — and that literal becomes the
            // JWT role claim (JwtTokenService.cs:45). So a tenant who built a read-only role
            // and deliberately withheld requisitions:create got users who could create
            // requisitions anyway, topped up to Recruiter's entire seeded set.
            //
            // "Denied" is not "unknown". Returning here is the whole fix.
            var hasPermission = await _permissionEvaluator.HasPermissionAsync(
                userId, tenantId, requirement.PermissionCode);

            if (hasPermission)
            {
                context.Succeed(requirement);
            }
            else
            {
                _logger.LogWarning(
                    "User {Sub} in Tenant {Tenant} denied permission '{PermissionCode}'",
                    sub, tenantClaim, requirement.PermissionCode);
            }
            return;
        }

        // -------------------------------------------------------------------------
        // 3. NO RESOLVABLE IDENTITY — SEEDED SYSTEM-ROLE FALLBACK
        // -------------------------------------------------------------------------
        // Only reached when the token has no parseable `sub`/`tenant_id`, so the evaluator
        // was never consulted and there is no database answer to override. This is the
        // narrow case the fallback was written for — an authenticated principal carrying a
        // system role claim but no usable identity. It mirrors PermissionEvaluator.cs:99,
        // which applies the seed only when the resolved permission set is empty.
        //
        // It must never run after a denial: that is what let a custom role inherit
        // Recruiter's floor.
        var systemRoles = RbacSeedData.GetSystemRoles();
        foreach (var roleName in roleClaims)
        {
            var matchedSeedRole = systemRoles.FirstOrDefault(r =>
                string.Equals(r.Code, roleName, StringComparison.OrdinalIgnoreCase));

            if (matchedSeedRole != null && matchedSeedRole.PermissionCodes.Contains(requirement.PermissionCode, StringComparer.OrdinalIgnoreCase))
            {
                context.Succeed(requirement);
                return;
            }
        }

        _logger.LogWarning(
            "User {Sub} in Tenant {Tenant} denied permission '{PermissionCode}'",
            sub, tenantClaim, requirement.PermissionCode);
    }
}
