using RecruitOps.Application.Common;
using RecruitOps.Application.Interfaces;
using RecruitOps.Domain;
using RecruitOps.Domain.Enums;

namespace RecruitOps.Infrastructure.Services;

/// <summary>ADR-0021 option A, decision 4.
/// <para>The actor's role and SuperAdmin flag come from the token, like every other authorization
/// decision in the API — role policies, <c>RoleScope</c>, the permission handler's SuperAdmin
/// bypass. That is safe because <c>OnTokenValidated</c> holds both claims to the database on every
/// request (<c>UserSessionValidator</c>). What the actor <i>holds</i> comes from the evaluator, the
/// same answer <c>[HasPermission]</c> gets.</para></summary>
public class GrantorCeiling : IGrantorCeiling
{
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentTenant _tenant;
    private readonly IPermissionEvaluator _permissions;

    public GrantorCeiling(ICurrentUser currentUser, ICurrentTenant tenant, IPermissionEvaluator permissions)
    {
        _currentUser = currentUser;
        _tenant = tenant;
        _permissions = permissions;
    }

    public async Task EnsureCanGrantAsync(UserRole holderRole, IEnumerable<string> permissionCodes, CancellationToken ct = default)
    {
        if (_currentUser.IsSuperAdmin) return;
        var (actorId, actorRole) = Actor();

        if (!RoleScope.Covers(actorRole, holderRole))
            throw new UnauthorizedAccessException(
                $"You cannot give {holderRole} access: it reaches further than your own role ({actorRole}).");

        var held = await _permissions.GetUserPermissionsAsync(actorId, _tenant.TenantId, ct);
        var missing = permissionCodes.Where(c => !held.Contains(c)).Distinct().Order(StringComparer.Ordinal).ToList();
        if (missing.Count > 0)
            throw new UnauthorizedAccessException(
                $"You cannot grant permissions you do not hold yourself: {string.Join(", ", missing)}.");
    }

    public Task EnsureCanManageAsync(UserRole currentRole, CancellationToken ct = default)
    {
        if (_currentUser.IsSuperAdmin) return Task.CompletedTask;
        var (_, actorRole) = Actor();

        if (!RoleScope.Covers(actorRole, currentRole))
            throw new UnauthorizedAccessException(
                $"You cannot change a user or role with {currentRole} access: it reaches further than your own role ({actorRole}).");

        return Task.CompletedTask;
    }

    /// <summary>No identity, or a role claim that is not exactly a role, is a refusal rather than a
    /// pass: every route here is authenticated, so reaching this without one is a defect, and a
    /// defect should not grant.</summary>
    private (Guid Id, UserRole Role) Actor() =>
        _currentUser.UserId is { } id && RoleScope.Parse(_currentUser.Role) is { } role
            ? (id, role)
            : throw new UnauthorizedAccessException("Only a signed-in user can grant or change access.");
}
