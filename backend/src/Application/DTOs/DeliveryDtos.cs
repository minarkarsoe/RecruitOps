using RecruitOps.Domain.Enums;

namespace RecruitOps.Application.DTOs;

/// <summary>One row of the delivery log — the read side of ADR-0026.
///
/// <para><b>Why this exists at all.</b> The outbox has been recording every send since 2026-08-20
/// and nothing rendered it, so a <see cref="OutboundMessageStatus.Failed"/> invitation — wrong
/// address, dead relay — was written down faithfully and shown to nobody. "Was this candidate
/// told?" was answerable only by someone with a psql prompt. Silence is the failure mode that
/// costs a hire (`design/internal/channels.html`).</para>
///
/// <para>⚠️ <b><see cref="Domain.Entities.OutboundMessage.PayloadJson"/> is deliberately absent.</b>
/// It holds render inputs and, for an offer, a salary. This DTO crosses to the browser and is
/// read by a Hiring Manager; the payload is not theirs to see and has no business being in a
/// list view. If a future column needs something from it, project that one field, not the blob.</para>
/// </summary>
/// <param name="Kind">The machine value, so the UI can filter without matching on prose.</param>
/// <param name="KindLabel">What the recruiter reads in the "Message" column. Resolved server-side
/// so the log and its filter cannot disagree about what a kind is called.</param>
/// <param name="Channel">How it went out. <b>Derived from <paramref name="Kind"/> today, because
/// the entity has no channel field</b> — everything the product currently sends is email. Module 8
/// adds Viber/Telegram/Facebook, and when it does this becomes a stored column rather than a
/// derivation; the design already draws the column expecting real values.</param>
/// <param name="Recipient">Email address or channel handle. Shown because "we sent it to the wrong
/// address" is one of the two failures this screen exists to make visible.</param>
/// <param name="CandidateName">Resolved through the subject where the subject leads to a candidate.
/// Null when it does not — a scheduled report goes to a colleague, not an applicant.</param>
/// <param name="LastError">Written for a recruiter, not for a log file. The handlers already do
/// this: "The round was cancelled before the invitation went out — the candidate was not emailed."</param>
public sealed record DeliveryLogEntryDto(
    Guid Id,
    OutboundMessageKind Kind,
    string KindLabel,
    string Channel,
    string Recipient,
    string? CandidateName,
    string? SubjectType,
    Guid? SubjectId,
    OutboundMessageStatus Status,
    int Attempts,
    DateTimeOffset? NextAttemptAt,
    string? LastError,
    DateTimeOffset? SentAt,
    DateTimeOffset CreatedAt);

/// <summary>Filters for the delivery log.
/// <para><see cref="SubjectType"/> and <see cref="SubjectId"/> travel together and answer the
/// question a recruiter actually asks — "what have we sent this candidate about this interview" —
/// which is why <see cref="Domain.Entities.OutboundMessage"/> carries the pair in the first place.</para>
/// </summary>
public sealed class DeliveryLogQuery
{
    public OutboundMessageStatus? Status { get; set; }

    public OutboundMessageKind? Kind { get; set; }

    public string? SubjectType { get; set; }

    public Guid? SubjectId { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 25;
}
