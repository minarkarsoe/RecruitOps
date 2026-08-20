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

    /// <summary>How many messages one pass claims.</summary>
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
