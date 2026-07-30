using RecruitOps.Application.DTOs;

namespace RecruitOps.Application.Interfaces;

public interface IRoleService
{
    Task<IReadOnlyList<PermissionModuleDto>> GetPermissionsGroupedAsync(CancellationToken ct = default);
    Task<IReadOnlyList<RoleListItemDto>> GetRolesAsync(CancellationToken ct = default);
    Task<RoleDetailDto?> GetRoleByIdAsync(Guid id, CancellationToken ct = default);
    Task<RoleDetailDto> CreateRoleAsync(CreateRoleRequest request, CancellationToken ct = default);
    Task<RoleDetailDto?> UpdateRoleAsync(Guid id, UpdateRoleRequest request, CancellationToken ct = default);
    Task<bool> DeleteRoleAsync(Guid id, CancellationToken ct = default);
}
