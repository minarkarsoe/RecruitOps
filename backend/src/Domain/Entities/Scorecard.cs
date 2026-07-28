using RecruitOps.Domain.Common;
using RecruitOps.Domain.Enums;

namespace RecruitOps.Domain.Entities;

/// <summary>One interviewer's evaluation of one interview. Unique per (interview, interviewer).
///
/// <para><see cref="Status"/> is the hinge of the blind-scoring rule (ADR-0017 §3): a Draft
/// is visible to its author and nobody else, and submitting is what earns a panel member the
/// right to read everyone else's. An interviewer who has read "Strong Yes, 5/5" before
/// writing their own assessment writes a different assessment — a panel that can read each
/// other first is an expensive way to get one opinion four times.</para>
/// </summary>
public class Scorecard : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid InterviewId { get; set; }

    /// <summary>The interviewer. Always a participant on the interview — the service
    /// refuses to create a scorecard for anyone else.</summary>
    public Guid InterviewerUserId { get; set; }

    /// <summary>The template in force when this was filled in. Nullable for the same reason
    /// as <c>Interview.ScorecardTemplateId</c>: a company with no template configured still
    /// gets a recommendation and a comment.</summary>
    public Guid? ScorecardTemplateId { get; set; }

    public ScorecardStatus Status { get; set; } = ScorecardStatus.Draft;

    public DateTimeOffset? SubmittedAt { get; set; }

    public HireRecommendation? Recommendation { get; set; }

    public string? SummaryComment { get; set; }
}
