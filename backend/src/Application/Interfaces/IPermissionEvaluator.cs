namespace RecruitOps.Application.Interfaces;

public interface IPermissionEvaluator
{
    Task<bool> HasPermissionAsync(Guid userId, Guid tenantId, string permissionCode, CancellationToken ct = default);
    Task<IReadOnlySet<string>> GetUserPermissionsAsync(Guid userId, Guid tenantId, CancellationToken ct = default);

    /// <summary>Everything <c>PermissionAuthorizationHandler</c> would grant this user: their role's
    /// permissions plus the seed permissions of each system role they carry as a claim. This is
    /// what the client is told at login, so the UI hides exactly what the API would refuse.
    /// <c>PermissionSetAgreementTests</c> pins it to the handler for every role and permission.</summary>
    Task<IReadOnlySet<string>> GetEffectivePermissionsAsync(
        Guid userId, Guid tenantId, IEnumerable<string> roleCodes, CancellationToken ct = default);
    void InvalidateUserPermissionsCache(Guid userId, Guid tenantId);
    void InvalidateRolePermissionsCache(Guid roleId);
}
