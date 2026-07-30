using RecruitOps.Domain.Common;

namespace RecruitOps.Domain.Entities;

/// <summary>Represents a granular permission in the format permission:module:feature:action (Requirement R2).</summary>
public class Permission : BaseEntity
{
    public string Module { get; set; } = string.Empty;
    public string Feature { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
