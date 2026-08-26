namespace RecruitOps.Infrastructure.Services.Delivery;

/// <summary>Tuning for the delivery worker (ADR-0026). Defaults are chosen for one instance per
/// company (ADR-0004) and a queue that is usually empty.</summary>
public sealed class OutboundDeliveryOptions
{
    public const string SectionName = "OutboundDelivery";

    /// <summary>How long to wait between polls when there was nothing to do. Short enough that an
    /// interview invitation feels immediate, long enough that an idle install is not hammering
    /// its own database.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>How many messages one pass claims.
    ///
    /// <para>⚠️ <b>The relationship worth knowing before tuning any of these.</b> A pass handles
    /// its batch sequentially, so its worst case is <c>BatchSize × Smtp:TimeoutSeconds</c> — at the
    /// defaults, 20 × 30 s = <b>10 minutes</b>, against a 5-minute
    /// <see cref="VisibilityTimeout"/>. Two consequences, raised by the 2026-08-20 security review:
    ///
    /// <list type="bullet">
    /// <item>A company whose relay is <i>slow</i> rather than down sees its own queue crawl. It is
    /// only ever that company's queue — ADR-0004 gives one instance per company — so this is a
    /// throughput ceiling, not a way for anyone to affect anyone else.</item>
    /// <item>It does <b>not</b> cause duplicate sends today, because there is exactly one worker
    /// and passes never overlap: <c>ExecuteAsync</c> awaits a whole pass before delaying. The
    /// moment a customer is given two replicas that stops being true — a pass outliving the
    /// visibility timeout is precisely how the second worker re-sends the first one's batch. Add
    /// it to the list of in-process assumptions ADR-0026 §3 says to audit together.</item>
    /// </list>
    ///
    /// <para>Bounded parallelism would fix the first point and is deliberately not here: it is the
    /// kind of complexity ADR-0026 chose not to take on, and it is a product call about throughput
    /// rather than a defect.</para></para></summary>
    public int BatchSize { get; set; } = 20;

    /// <summary>How long a claimed message stays invisible before it is considered abandoned.
    ///
    /// <para>This is what makes a crash mid-send safe: the row is never marked "in flight", it is
    /// just pushed into the future, so a process that dies leaves work that becomes due again on
    /// its own. It must comfortably exceed the slowest send, or a slow SMTP server produces
    /// duplicates.</para></summary>
    public TimeSpan VisibilityTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Attempts before a message is given up on and marked Failed. A poison message that
    /// retries forever occupies the queue and hides real traffic in the delivery log.</summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>First retry delay. Doubles per attempt, capped by <see cref="MaxBackoff"/>.</summary>
    public TimeSpan BaseBackoff { get; set; } = TimeSpan.FromMinutes(1);

    public TimeSpan MaxBackoff { get; set; } = TimeSpan.FromHours(1);
}
