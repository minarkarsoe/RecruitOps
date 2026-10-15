using RecruitOps.Domain.Common;
using RecruitOps.Domain.Enums;

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

    /// <summary>A custom role's reach through the role policies — what its holders carry as their
    /// <see cref="UserRole"/> (ADR-0021 option A). One of <see cref="RoleScope.AssignableBaseRoles"/>,
    /// chosen when the role is built. Null on system roles: their code is their enum.</summary>
    public UserRole? BaseRole { get; set; }

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    public ICollection<User> Users { get; set; } = new List<User>();

    /// <summary>The enum a holder of this custom role carries: its base role while it is active,
    /// and <see cref="UserRole.Interviewer"/> — the enum no role policy admits — while it is not,
    /// or if it somehow has no base role. Never a guess with reach: that guess was Recruiter, and
    /// it is what ADR-0021 removed.</summary>
    public UserRole HolderRole() => IsActive && BaseRole is { } baseRole ? baseRole : UserRole.Interviewer;
}
