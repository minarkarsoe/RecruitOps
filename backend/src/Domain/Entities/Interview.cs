using RecruitOps.Domain.Common;
using RecruitOps.Domain.Enums;

namespace RecruitOps.Domain.Entities;

/// <summary>One scheduled conversation with a candidate, against one job application.
///
/// <para>The recruiter fixes the slot by hand. Calendar free/busy (3.1) and invitation
/// email (3.2) are Module 7's to add — see ADR-0017; nothing here assumes either, so
/// adding them later does not reshape this row.</para>
///
/// <para>Creating one of these <b>moves the application to the Interview stage in the same
/// transaction</b> (ADR-0017 §5). An interview that exists against an application still
/// sitting at Screening is a gap in the stage history that Module 5 cannot reconstruct.</para>
/// </summary>
public class Interview : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid JobApplicationId { get; set; }

    /// <summary>1 for the first round, 2 for the second, and so on. Derived from the count
    /// of existing interviews so it cannot be set inconsistently by a caller.</summary>
    public int Round { get; set; } = 1;

    public DateTimeOffset ScheduledStart { get; set; }
    public int DurationMinutes { get; set; } = 60;

    public InterviewMode Mode { get; set; } = InterviewMode.OnSite;

    /// <summary>Room, meeting URL or phone number, depending on <see cref="Mode"/>.</summary>
    public string? Location { get; set; }

    public InterviewStatus Status { get; set; } = InterviewStatus.Scheduled;

    /// <summary>Which criteria this interview is scored against, resolved at scheduling time
    /// (posting override → department → company-wide, ADR-0017 §1). Stored rather than
    /// re-resolved on read, so that moving a posting to another department later does not
    /// change the criteria an interview was actually conducted against.
    /// <para>Null when the company has no template at all — the interview still happens,
    /// the panel just leaves an overall recommendation and a comment.</para></summary>
    public Guid? ScorecardTemplateId { get; set; }

    public Guid? ScheduledByUserId { get; set; }

    /// <summary>Why the round was called off. Kept on the row rather than in a note so the
    /// reason survives even when nobody wrote one.</summary>
    public string? CancellationReason { get; set; }

    /// <summary>Recruiter-facing agenda or brief for the panel — not shown to the candidate.</summary>
    public string? Agenda { get; set; }
}
