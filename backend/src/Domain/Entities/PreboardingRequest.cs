using RecruitOps.Domain.Common;
using RecruitOps.Domain.Enums;

namespace RecruitOps.Domain.Entities;

/// <summary>An accepted offer's pre-boarding: what the candidate was asked for, and the three things that
/// happen once (ADR-0030 §5b). Written in the same transaction as the acceptance (or, for an offer accepted
/// while an older release ran, on first use).
/// <para><b><see cref="Version"/> is the one concurrency token, and every change to the row bumps it</b>:
/// "Send for checking", "Remind", and every approval of a document. So two approvals of the last two
/// required documents cannot both commit without one seeing the other — the one that sees both approved
/// queues the handoff in the same transaction as its approval. A change that loses is told to try again;
/// nothing it did was saved.</para></summary>
public class PreboardingRequest : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public Guid OfferId { get; set; }

    /// <summary>The company's required items when the offer was accepted, kept: a candidate is never asked
    /// for more halfway, and the handoff's condition cannot move under them.</summary>
    public PreboardingItems Required { get; set; }

    public int Version { get; set; }

    /// <summary>When the candidate last pressed "Send for checking".</summary>
    public DateTimeOffset? SubmittedAt { get; set; }

    /// <summary>When the recruiter was last emailed that documents were sent — at most once an hour, however
    /// often the candidate presses the button.</summary>
    public DateTimeOffset? NotifiedAt { get; set; }

    public DateTimeOffset? RemindedAt { get; set; }

    public int ReminderCount { get; set; }

    /// <summary>When the IT and Admin emails were queued — with the last required document's approval.</summary>
    public DateTimeOffset? HandoffAt { get; set; }
}
