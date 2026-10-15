using RecruitOps.Domain.Common;
using RecruitOps.Domain.Enums;

namespace RecruitOps.Domain.Entities;

/// <summary>One thing the product owes the outside world: an email, or a message on a sourcing
/// channel. The durable half of ADR-0026.
///
/// <para><b>Written in the same transaction as the thing that caused it</b>, then sent by the
/// worker. Never sent inline. A send that happens without a row here is a send nobody can prove
/// happened, and Modules 4 and 8 both turn on exactly that question — the recruiter's next
/// action depends on whether the candidate was actually told.</para>
///
/// <para><b>Claiming.</b> The worker takes due rows (<see cref="OutboundMessageStatus.Pending"/>
/// with <see cref="NextAttemptAt"/> in the past) under <c>FOR UPDATE SKIP LOCKED</c>, pushing
/// <see cref="NextAttemptAt"/> forward by a visibility timeout as it claims. A process that dies
/// mid-send therefore loses nothing: the row is still Pending and becomes due again. See
/// <see cref="OutboundMessageStatus"/> for why there is no in-flight state.</para>
///
/// <para>⚠️ <b>The worker's claim query must use <c>IgnoreQueryFilters()</c>, and it is the only
/// place in the job runner that may.</b> The worker runs with no HTTP request, so
/// <c>ICurrentTenant.TenantId</c> is <c>Guid.Empty</c> and the global filter on this table would
/// match nothing — the queue would silently never drain. The worker then establishes the tenant
/// for the handler's scope from <see cref="TenantId"/> on the claimed row, so handler code runs
/// with the filters on and looks exactly like request code (ADR-0026 §4).</para>
/// </summary>
public class OutboundMessage : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public OutboundMessageKind Kind { get; set; }

    /// <summary>Email address, or the channel handle for <see cref="OutboundMessageKind.ChannelNotification"/>.
    /// Not validated here — the transport is what knows what a valid address looks like.</summary>
    public string Recipient { get; set; } = string.Empty;

    /// <summary>The entity this message is about, e.g. <c>"Offer"</c>, <c>"Interview"</c>. Paired
    /// with <see cref="SubjectId"/> so the delivery log can be filtered to one record — "what have
    /// we sent this candidate about this offer" is the question a recruiter actually asks.</summary>
    public string? SubjectType { get; set; }

    public Guid? SubjectId { get; set; }

    /// <summary>The <i>data</i> needed to render the message, not the rendered message.
    ///
    /// <para>Rendering happens at send time on purpose. A body frozen at enqueue time goes stale
    /// between the offer being approved and the mail going out, and a reminder queued for next
    /// week would carry last week's figures.</para>
    ///
    /// <para>⚠️ This will contain a candidate's name and, for an offer, their salary. It is
    /// therefore in scope for the Module 7.4 retention policy and must not become the one table
    /// that quietly keeps everything.</para></summary>
    public string PayloadJson { get; set; } = "{}";

    public OutboundMessageStatus Status { get; set; } = OutboundMessageStatus.Pending;

    /// <summary>How many times the worker has claimed this row. Drives the backoff, and the cap
    /// that moves a row to <see cref="OutboundMessageStatus.Failed"/> rather than letting a
    /// poison message occupy the queue forever.</summary>
    public int Attempts { get; set; }

    /// <summary>When this row next becomes claimable. Set at enqueue for a delayed send (an offer
    /// reminder in three days), pushed forward on each claim, and used as the backoff timer.</summary>
    public DateTimeOffset NextAttemptAt { get; set; }

    /// <summary>Why the last attempt did not succeed. Shown in the delivery log, so it is written
    /// for a recruiter rather than for a log file: the design's example is "outside the 24-hour
    /// messaging window — she was not told, send by email instead", not a stack trace.</summary>
    public string? LastError { get; set; }

    public DateTimeOffset? SentAt { get; set; }
}
