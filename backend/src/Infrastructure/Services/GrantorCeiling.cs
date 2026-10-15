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
    private readonly IAuditTrail _audit;
    private readonly IRequestOrigin _origin;

    public GrantorCeiling(
        ICurrentUser currentUser, ICurrentTenant tenant, IPermissionEvaluator permissions, IAuditTrail audit,
        IRequestOrigin origin)
    {
        _currentUser = currentUser;
        _tenant = tenant;
        _permissions = permissions;
        _audit = audit;
        _origin = origin;
    }

    public async Task EnsureCanGrantAsync(UserRole holderRole, IEnumerable<string> permissionCodes, CancellationToken ct = default)
    {
        if (_currentUser.IsSuperAdmin) return;
        var (actorId, actorRole) = await ActorAsync(ct);

        if (!RoleScope.Covers(actorRole, holderRole))
            await RefuseAsync(
                $"You cannot give {holderRole} access: it reaches further than your own role ({actorRole}).",
                $"Tried to give {holderRole} access, which reaches further than their own role ({actorRole}).",
                new { reason = "reach", actorRole = actorRole.ToString(), holderRole = holderRole.ToString() }, ct);

        var held = await _permissions.GetUserPermissionsAsync(actorId, _tenant.TenantId, ct);
        var missing = permissionCodes.Where(c => !held.Contains(c)).Distinct().Order(StringComparer.Ordinal).ToList();
        if (missing.Count > 0)
            await RefuseAsync(
                $"You cannot grant permissions you do not hold yourself: {string.Join(", ", missing)}.",
                $"Tried to grant permissions they do not hold: {string.Join(", ", missing)}.",
                new { reason = "permissions", actorRole = actorRole.ToString(), missing }, ct);
    }

    public async Task EnsureCanManageAsync(UserRole currentRole, CancellationToken ct = default)
    {
        if (_currentUser.IsSuperAdmin) return;
        var (_, actorRole) = await ActorAsync(ct);

        if (!RoleScope.Covers(actorRole, currentRole))
            await RefuseAsync(
                $"You cannot change a user or role with {currentRole} access: it reaches further than your own role ({actorRole}).",
                $"Tried to change a user or role with {currentRole} access, which reaches further than their own role ({actorRole}).",
                new { reason = "manage", actorRole = actorRole.ToString(), targetRole = currentRole.ToString() }, ct);
    }

    /// <summary>No identity, or a role claim that is not exactly a role, is a refusal rather than a
    /// pass: every route here is authenticated, so reaching this without one is a defect, and a
    /// defect should not grant.</summary>
    private async Task<(Guid Id, UserRole Role)> ActorAsync(CancellationToken ct)
    {
        if (_currentUser.UserId is { } id && RoleScope.Parse(_currentUser.Role) is { } role)
            return (id, role);

        await RefuseAsync("Only a signed-in user can grant or change access.",
            "Tried to grant or change access without a recognisable signed-in role.",
            new { reason = "no_actor", roleClaim = _currentUser.Role }, ct);
        return default; // unreachable: RefuseAsync always throws
    }

    /// <summary>Records the refusal (ADR-0027) — on its own context, because the request's may hold
    /// the change being refused — then refuses. <paramref name="message"/> is what the caller reads;
    /// <paramref name="summary"/> is what an auditor reads about them.</summary>
    private async Task RefuseAsync(string message, string summary, object details, CancellationToken ct)
    {
        await _audit.RecordStandaloneAsync(AuditAction.AccessChangeRefused, null, summary,
            new { endpoint = _origin.Endpoint, details }, ct: ct);
        throw new UnauthorizedAccessException(message);
    }
}
