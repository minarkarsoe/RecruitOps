using RecruitOps.Domain.Common;
using RecruitOps.Domain.Enums;

namespace RecruitOps.Domain.Entities;

/// <summary>A person in the talent pool. Dedup keyed on Email/Phone (Module 4).</summary>
public class Candidate : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public SourceChannel Source { get; set; } = SourceChannel.Direct;
    // TODO: FullName, Email, Phone, Skills, Experience, CvFileUrl, MergedIntoId ...
}
