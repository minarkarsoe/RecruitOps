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
            var hasPermission = await _permissionEvaluator.HasPermissionAsync(
                userId, tenantId, requirement.PermissionCode);

            if (hasPermission)
            {
                context.Succeed(requirement);
                return;
            }
        }

        // -------------------------------------------------------------------------
        // 3. ROLE-BASED CLAIM FALLBACK FOR SYSTEM ROLES
        // -------------------------------------------------------------------------
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
