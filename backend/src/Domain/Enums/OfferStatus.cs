namespace RecruitOps.Domain.Enums;

/// <summary>Module 4 offer lifecycle (ADR-0030 §3).
/// <para>⚠️ <see cref="Rejected"/> means <b>the candidate declined</b>. It is not
/// <see cref="PipelineStatus.Rejected"/> — same word, different enum, never conflated. An
/// approver's rejection returns the offer to <see cref="Draft"/> instead.</para></summary>
public enum OfferStatus
{
    Draft,

    /// <summary>Submitted to the offer approval chain; a round is live.</summary>
    PendingApproval,

    /// <summary>Cleared to send, not yet sent. Added to the 2026-08-18 vocabulary by ADR-0030.</summary>
    Approved,

    Sent,
    Viewed,
    Accepted,

    /// <summary>The candidate declined.</summary>
    Rejected,

    Expired,
}
