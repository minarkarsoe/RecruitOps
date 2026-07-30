namespace RecruitOps.Application.DTOs;

public record PermissionDto(
    Guid Id,
    string Code,
    string Name,
    string Description,
    string Module,
    string Feature,
    string Action
);

public record PermissionFeatureDto(
    string Feature,
    IReadOnlyList<PermissionDto> Permissions
);

public record PermissionModuleDto(
    string Module,
    IReadOnlyList<PermissionFeatureDto> Features
);

public record RoleListItemDto(
    Guid Id,
    string Name,
    string Code,
    string Description,
    bool IsSystemRole,
    bool IsSuperAdmin,
    bool IsActive,
    int UserCount,
    int PermissionCount
);

public record RoleDetailDto(
    Guid Id,
    string Name,
    string Code,
    string Description,
    bool IsSystemRole,
    bool IsSuperAdmin,
    bool IsActive,
    IReadOnlyList<PermissionDto> AssignedPermissions,
    IReadOnlyList<string> AssignedPermissionCodes,
    int UserCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt
);

public record CreateRoleRequest(
    string Name,
    string? Code,
    string? Description,
    IReadOnlyList<string> PermissionCodes
);

public record UpdateRoleRequest(
    string Name,
    string? Description,
    bool IsActive,
    IReadOnlyList<string> PermissionCodes
);
