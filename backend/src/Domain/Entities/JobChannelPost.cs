using RecruitOps.Domain.Common;
using RecruitOps.Domain.Enums;

namespace RecruitOps.Domain.Entities;

/// <summary>Record of a job auto-posted to a social channel + inbound tracking (Module 3).</summary>
public class JobChannelPost : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid JobId { get; set; }
    public SourceChannel Channel { get; set; }
    // TODO: ExternalPostId, PostedAt, ApplicantsFromChannel (tracking count) ...
}
