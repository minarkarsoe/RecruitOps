using RecruitOps.Domain.Common;

namespace RecruitOps.Domain.Entities;

/// <summary>An org unit that owns requisitions and vacancies. Replaces the agency-era
/// Client entity — an in-house system hires for departments, not external clients.</summary>
public class Department : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public bool IsActive { get; set; } = true;
}
