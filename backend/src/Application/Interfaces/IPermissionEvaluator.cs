namespace RecruitOps.Application.Interfaces;

public interface IPermissionEvaluator
{
    Task<bool> HasPermissionAsync(Guid userId, Guid tenantId, string permissionCode, CancellationToken ct = default);
    Task<IReadOnlySet<string>> GetUserPermissionsAsync(Guid userId, Guid tenantId, CancellationToken ct = default);
    void InvalidateUserPermissionsCache(Guid userId, Guid tenantId);
    void InvalidateRolePermissionsCache(Guid roleId);
}
