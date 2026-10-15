namespace RecruitOps.Application.Interfaces;

/// <summary>Whether a signed token still describes its account.
/// <para>A token records who the user was at sign-in, for 8 hours. Deactivation, a role change or a
/// removed account must take effect on the next request — the API's role policies and the
/// permission handler's role-claim fallback both read the token, not the database.</para></summary>
public interface IUserSessionValidator
{
    /// <summary>True only for an existing, active user in that tenant whose role and SuperAdmin
    /// flag are still exactly what the token says.</summary>
    Task<bool> IsCurrentAsync(
        Guid userId, Guid tenantId, string? roleClaim, bool superAdminClaim, CancellationToken ct = default);
}
