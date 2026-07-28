using RecruitOps.Domain.Common;
using RecruitOps.Domain.Enums;

namespace RecruitOps.Domain.Entities;

/// <summary>A staff member of an agency. RBAC via <see cref="UserRole"/> (Module 1).</summary>
public class User : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Hashed with IPasswordHasher — never store plaintext.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public UserRole Role { get; set; } = UserRole.Recruiter;
    public bool IsActive { get; set; } = true;
}
