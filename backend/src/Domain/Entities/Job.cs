using RecruitOps.Domain.Common;
using RecruitOps.Domain.Enums;

namespace RecruitOps.Domain.Entities;

/// <summary>A job order/vacancy opened by a client (Module 3).</summary>
public class Job : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid ClientId { get; set; }
    public JobStatus Status { get; set; } = JobStatus.Draft;
    // TODO: Title, Description, Location, SalaryRange, PostedAt, ClosedAt ...
}
