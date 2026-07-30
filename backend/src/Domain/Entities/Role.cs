using RecruitOps.Domain.Common;

namespace RecruitOps.Domain.Entities;

/// <summary>Represents an RBAC role within a tenant or system-wide (Requirement R2).</summary>
public class Role : BaseEntity
{
    public Guid? TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsSystemRole { get; set; }
    public bool IsSuperAdmin { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    public ICollection<User> Users { get; set; } = new List<User>();
}
