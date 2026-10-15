namespace RecruitOps.Application.Interfaces;

/// <summary>The one way this product puts an email on the wire (ADR-0026 §1).
///
/// <para><b>SMTP is the floor, not the fallback.</b> ADR-0004 sells on-premise installs, and some
/// of those application servers have no outbound internet at all — an internal mail relay is the
/// only transport they have. A product whose only send path is a vendor API simply does not
/// deliver mail for such a customer, and it fails at the worst possible moment: the offer was
/// "sent" and the candidate never heard. A hosted install may add SES, SendGrid or Microsoft 365
/// as an additional implementation; nothing in the product may depend on one existing.</para>
///
/// <para><b>Nobody calls this from a request.</b> Sending is the delivery worker's job, from a
/// row in <c>OutboundMessage</c> — see ADR-0026 §2. Calling it inline would make approving an
/// offer as slow as the customer's mail server, and would leave no record of whether the message
/// actually went.</para>
/// </summary>
public interface IEmailSender
{
    /// <summary>Hands one message to the transport.
    ///
    /// <para>Returns when the transport has accepted it — which is not the same as the candidate
    /// having received it. Bounce handling is a provider feature and is out of scope for plain
    /// SMTP; the delivery log records what we were told, honestly.</para>
    ///
    /// <para>Throws <see cref="EmailDeliveryException"/> on failure, carrying whether it is worth
    /// trying again. Anything else that escapes is treated as retryable by the worker, which is
    /// the safe direction.</para></summary>
    Task SendAsync(EmailMessage message, CancellationToken ct = default);
}

/// <summary>One email, already rendered.
///
/// <para>Rendering happens in the handler at send time, not here and not at enqueue time — a body
/// frozen when the row was written goes stale before it is sent (ADR-0026 §2).</para>
/// </summary>
/// <param name="To">A single recipient. One row in the delivery log means one recipient, because
/// "was this candidate told?" has no useful answer for a message sent to four people at once.</param>
/// <param name="Subject">Single-line. Newlines in a subject are a header-injection vector, and the
/// sender strips them rather than trusting every caller to remember.</param>
/// <param name="PlainTextBody">Required. Candidate-facing mail in this product is plain text on
/// purpose: it renders identically on every phone, it carries Burmese without a font stack, and a
/// candidate-supplied name cannot inject markup into it.</param>
/// <param name="HtmlBody">Optional, for internal mail that wants formatting. <b>Whoever sets this
/// owns escaping every value interpolated into it.</b></param>
public sealed record EmailMessage(
    string To,
    string Subject,
    string PlainTextBody,
    string? HtmlBody = null);

/// <summary>A send that did not happen, and whether trying again could change that.
///
/// <para>The split is the only thing the worker needs from a transport, and it is deliberately
/// narrow: <see cref="IsPermanent"/> is true only when the <i>address</i> is unusable. A refused
/// connection, a timeout, a rejected password, a server that is down — all of those are somebody's
/// to fix, and burning the message would mean the fix arrives too late to help.</para>
/// </summary>
public sealed class EmailDeliveryException : Exception
{
    public EmailDeliveryException(string message, bool isPermanent, Exception? innerException = null)
        : base(message, innerException)
    {
        IsPermanent = isPermanent;
    }

    /// <summary>True when the next attempt would fail identically. The handler turns this into
    /// <c>DeliveryOutcome.Failed</c>; false becomes <c>Retry</c>.</summary>
    public bool IsPermanent { get; }
}
