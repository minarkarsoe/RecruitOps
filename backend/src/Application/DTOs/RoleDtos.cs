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
    int PermissionCount,
    string? BaseRole
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
    DateTimeOffset? UpdatedAt,
    string? BaseRole
);

/// <param name="BaseRole">Required: the system role whose reach holders get (ADR-0021 option A) —
/// HrDirector, Recruiter, HiringManager, Approver or Interviewer.</param>
public record CreateRoleRequest(
    string Name,
    string? Code,
    string? Description,
    IReadOnlyList<string> PermissionCodes,
    string? BaseRole = null
);

/// <param name="BaseRole">Null keeps the current base role.</param>
public record UpdateRoleRequest(
    string Name,
    string? Description,
    bool IsActive,
    IReadOnlyList<string> PermissionCodes,
    string? BaseRole = null
);
