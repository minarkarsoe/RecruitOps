namespace RecruitOps.Application.Interfaces;

public interface IPermissionEvaluator
{
    Task<bool> HasPermissionAsync(Guid userId, Guid tenantId, string permissionCode, CancellationToken ct = default);
    Task<IReadOnlySet<string>> GetUserPermissionsAsync(Guid userId, Guid tenantId, CancellationToken ct = default);

    /// <summary>What holding this role grants: the role row's matrix, or — for a system role
    /// without rows — its seeded one. A custom role never borrows its enum's matrix.</summary>
    Task<IReadOnlySet<string>> GetRolePermissionsAsync(Guid? roleId, Domain.Enums.UserRole role, CancellationToken ct = default);

    void InvalidateUserPermissionsCache(Guid userId, Guid tenantId);
    void InvalidateRolePermissionsCache(Guid roleId);
}
