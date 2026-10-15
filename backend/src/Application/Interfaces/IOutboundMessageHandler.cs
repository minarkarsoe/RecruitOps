using RecruitOps.Domain.Entities;
using RecruitOps.Domain.Enums;

namespace RecruitOps.Application.Interfaces;

/// <summary>What the delivery worker does with one claimed message (ADR-0026).
///
/// <para>One handler per <see cref="OutboundMessageKind"/>. A handler runs inside a DI scope
/// whose tenant has already been established from the message row, so it queries with the global
/// filters ON and <b>must not</b> call <c>IgnoreQueryFilters()</c>. If it needs to, something is
/// wrong upstream.</para>
///
/// <para>A handler must not decide retry policy — it reports what happened and the worker owns
/// attempts, backoff and the cap. Two handlers disagreeing about how many times to retry is how
/// a queue develops moods.</para>
/// </summary>
public interface IOutboundMessageHandler
{
    OutboundMessageKind Kind { get; }

    Task<DeliveryOutcome> HandleAsync(OutboundMessage message, CancellationToken ct = default);
}

/// <summary>What happened to one delivery attempt.
///
/// <para>Four outcomes, because collapsing any two of them loses something a recruiter needs.
/// In particular <see cref="Suppressed"/> is not a failure: an honoured opt-out is the system
/// working, and the delivery log must be able to render it neutrally.</para>
/// </summary>
public readonly record struct DeliveryOutcome
{
    private DeliveryOutcome(DeliveryOutcomeKind kind, string? detail)
    {
        Kind = kind;
        Detail = detail;
    }

    public DeliveryOutcomeKind Kind { get; }

    /// <summary>Why, for everything except <see cref="Sent"/>. Written for a recruiter reading the
    /// delivery log, not for a log file — "outside the 24-hour messaging window, send by email
    /// instead" rather than an exception type.</summary>
    public string? Detail { get; }

    /// <summary>Handed to the transport without error.</summary>
    public static DeliveryOutcome Sent() => new(DeliveryOutcomeKind.Sent, null);

    /// <summary>Correctly not sent — opted out, or the message became irrelevant before it went
    /// (offer withdrawn, interview cancelled). Terminal, and not an error.</summary>
    public static DeliveryOutcome Suppressed(string reason) => new(DeliveryOutcomeKind.Suppressed, reason);

    /// <summary>Failed in a way that might not fail again: a timeout, a 503, a rate limit. The
    /// worker will back off and try again until the attempt cap.</summary>
    public static DeliveryOutcome Retry(string error) => new(DeliveryOutcomeKind.Retry, error);

    /// <summary>Failed in a way that will fail identically next time: a malformed address, a
    /// payload that cannot be rendered. Terminal — retrying is just a slower way to give up, and
    /// it keeps a poison message circulating.</summary>
    public static DeliveryOutcome Failed(string error) => new(DeliveryOutcomeKind.Failed, error);
}

public enum DeliveryOutcomeKind
{
    Sent,
    Suppressed,
    Retry,
    Failed,
}
