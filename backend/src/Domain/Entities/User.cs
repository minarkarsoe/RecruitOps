using RecruitOps.Domain.Common;
using RecruitOps.Domain.Enums;

namespace RecruitOps.Domain.Entities;

/// <summary>A staff member of an agency. RBAC via <see cref="UserRole"/> (Module 1).</summary>
public class User : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public UserRole Role { get; set; } = UserRole.JuniorRecruiter;
    // TODO: Email, DisplayName, PasswordHash / external identity ref, IsActive ...
}
