namespace RecruitOps.Application.DTOs;

public record UserRoleInfoDto(
    Guid Id,
    string Name,
    string Code,
    string Description,
    bool IsSystemRole,
    bool IsSuperAdmin);

public record UserDetailDto(
    Guid Id,
    string Email,
    string DisplayName,
    string Role,
    Guid? RoleId,
    UserRoleInfoDto? RoleDetails,
    IReadOnlyList<string> Permissions,
    bool IsActive,
    bool IsSuperAdmin,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
