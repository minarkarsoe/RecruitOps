using RecruitOps.Domain.Common;
using RecruitOps.Domain.Enums;

namespace RecruitOps.Domain.Entities;

/// <summary>An accepted offer's pre-boarding: what the candidate was asked for, and the three things that
/// happen once (ADR-0030 §5b). Written in the same transaction as the acceptance.
/// <para>Three concurrency tokens, each for a different "once": "Send for checking" notifies once per
/// change, "Remind" at most once a day, the handoff once ever. A change that loses one of these races is
/// told to reload, or retried by the code that made it.</para></summary>
public class PreboardingRequest : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public Guid OfferId { get; set; }

    /// <summary>The company's required items when the offer was accepted, kept: a candidate is never asked
    /// for more halfway, and the handoff's condition cannot move under them.</summary>
    public PreboardingItems Required { get; set; }

    /// <summary>When the candidate last pressed "Send for checking".</summary>
    public DateTimeOffset? SubmittedAt { get; set; }

    public DateTimeOffset? RemindedAt { get; set; }

    public int ReminderCount { get; set; }

    /// <summary>When the IT and Admin emails were queued — the last required document's approval.</summary>
    public DateTimeOffset? HandoffAt { get; set; }
}
