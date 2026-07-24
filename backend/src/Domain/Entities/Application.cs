using RecruitOps.Domain.Common;
using RecruitOps.Domain.Enums;

namespace RecruitOps.Domain.Entities;

/// <summary>A candidate's application to a job; tracks pipeline stage + client feedback.</summary>
public class Application : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid JobId { get; set; }
    public Guid CandidateId { get; set; }
    public PipelineStatus Status { get; set; } = PipelineStatus.Sourced;
    public ClientFeedback? ClientFeedback { get; set; }
    // TODO: AppliedAt, StageHistory, RecruiterNotes ...
}
