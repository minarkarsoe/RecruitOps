namespace RecruitOps.Domain.Enums;

/// <summary>What an <see cref="Entities.OutboundMessage"/> is for (ADR-0026).
/// <para>One value per thing the product actually sends. A new value means a new handler,
/// so the bar for adding one is a real feature, not a variation of an existing message.</para>
/// </summary>
public enum OutboundMessageKind
{
    /// <summary>Module 3.2 — an interview invitation to a candidate and the panel.</summary>
    InterviewInvitation,

    /// <summary>Module 4.2 — the offer letter link to the candidate.</summary>
    OfferSent,

    /// <summary>Module 4.1 — a nudge on an offer that has been sent and not answered.</summary>
    OfferReminder,

    /// <summary>Module 4.3 — the IT and Admin handoff once pre-boarding is complete.</summary>
    PreboardingHandoff,

    /// <summary>Module 5.3 — a recurring report, enqueued by a <see cref="Entities.ScheduledJob"/>.</summary>
    ScheduledReport,

    /// <summary>Module 8 — a status notification over Viber / Telegram / Facebook.</summary>
    ChannelNotification,
}

/// <summary>Where a message is in its life (ADR-0026).
///
/// <para>There is deliberately <b>no "Sending" state</b>. The worker claims a row by pushing
/// <see cref="Entities.OutboundMessage.NextAttemptAt"/> forward by a visibility timeout inside
/// the claiming transaction, so a process that dies mid-send leaves the row <see cref="Pending"/>
/// and it simply becomes due again. A separate in-flight state would need a reaper to clean up
/// after crashes, which is a second mechanism doing the same job.</para>
/// </summary>
public enum OutboundMessageStatus
{
    /// <summary>Waiting to be sent, or waiting to be retried. The only state the worker claims.</summary>
    Pending,

    /// <summary>Handed to the transport without error.</summary>
    Sent,

    /// <summary>Gave up. <see cref="Entities.OutboundMessage.LastError"/> says why, and it is
    /// terminal — the worker will not pick it up again.</summary>
    Failed,

    /// <summary><b>A correct outcome, not an error.</b> The recipient opted out, or the message
    /// became irrelevant before it was sent (the offer was withdrawn, the interview cancelled).
    /// Kept as its own state so the delivery log can show it neutrally rather than in red —
    /// Module 8 requires opt-out, and rendering an honoured opt-out as a failure teaches
    /// recruiters to ignore the failure colour.</summary>
    Suppressed,
}

/// <summary>What a <see cref="Entities.ScheduledJob"/> produces on each run.
/// <para>One value, because one feature needs recurrence today. Listing speculative ones would
/// be inventing scope.</para>
/// </summary>
public enum ScheduledJobKind
{
    /// <summary>Module 5.3 — enqueues an <see cref="OutboundMessageKind.ScheduledReport"/>.</summary>
    ScheduledReport,
}

/// <summary>How often a <see cref="Entities.ScheduledJob"/> runs.
/// <para>Deliberately not a cron expression: parsing cron needs a library, and ADR-0026 chose
/// to add no package. These three cover what Module 5.3 asks for.</para>
/// </summary>
public enum ScheduledJobRecurrence
{
    Daily,
    Weekly,
    Monthly,
}
