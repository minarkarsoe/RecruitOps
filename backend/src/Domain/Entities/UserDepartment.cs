using RecruitOps.Domain.Common;

namespace RecruitOps.Domain.Entities;

/// <summary>Grants a user access to a department. Modelled as a set, not a single
/// DepartmentId, because a Hiring Manager may own more than one department (ADR-0003).</summary>
public class UserDepartment : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid DepartmentId { get; set; }
}
