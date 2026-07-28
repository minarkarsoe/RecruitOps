using RecruitOps.Domain.Common;

namespace RecruitOps.Domain.Entities;

/// <summary>Reusable Job Description master template (Module 1.2).</summary>
public class JdTemplate : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;

    /// <summary>Optional: template belongs to one department rather than the whole company.</summary>
    public Guid? DepartmentId { get; set; }

    public bool IsActive { get; set; } = true;
}
