using RecruitOps.Domain.Common;
using RecruitOps.Domain.Enums;

namespace RecruitOps.Domain.Entities;

/// <summary>A recurring thing to do — today, only Module 5.3's scheduled reports.
///
/// <para>It produces <see cref="OutboundMessage"/> rows and sends nothing itself. The same worker
/// reads both tables, so recurrence adds a row shape rather than a second mechanism, and there is
/// no cron container that exists only in the hosted deployment (ADR-0026 §5).</para>
/// </summary>
public class ScheduledJob : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public ScheduledJobKind Kind { get; set; }

    /// <summary>What to put on the <see cref="OutboundMessage"/> this job enqueues — which report,
    /// which filters, who receives it.</summary>
    public string PayloadJson { get; set; } = "{}";

    public ScheduledJobRecurrence Recurrence { get; set; }

    /// <summary>Which day, for <see cref="ScheduledJobRecurrence.Weekly"/>. Null otherwise.
    /// Stored as <see cref="System.DayOfWeek"/>'s numbering (Sunday = 0).</summary>
    public int? DayOfWeek { get; set; }

    /// <summary>Which day, for <see cref="ScheduledJobRecurrence.Monthly"/>. Null otherwise.
    /// <para><b>Capped at 28 by the configuration.</b> "The 31st" does not exist in February, and
    /// the alternatives — skip the month, or silently slide to the 28th — are both surprises. A
    /// range that is always valid has no edge case to get wrong.</para></summary>
    public int? DayOfMonth { get; set; }

    /// <summary>Minutes past local midnight, 0–1439. An int rather than a TimeOnly/TimeSpan
    /// because it maps cleanly and compares trivially in SQL.</summary>
    public int TimeOfDayMinutes { get; set; }

    /// <summary>IANA id, e.g. <c>Asia/Yangon</c>. <b>Required, with no default.</b>
    ///
    /// <para>Storing UTC alone would be the easy choice and quietly wrong: a customer who asks for
    /// "every Monday at 9" means 9 in their office, and UTC+6:30 turns that into Sunday evening.
    /// There is no default because guessing a company's timezone is the same bug with fewer
    /// symptoms.</para>
    ///
    /// <para>Open, and recorded in ADR-0026: a company-level timezone setting does not exist yet,
    /// so the caller supplies this per job. When that setting lands, this becomes its default
    /// rather than its replacement — a per-job override stays useful for a report that follows a
    /// regional office.</para></summary>
    public string TimeZoneId { get; set; } = string.Empty;

    /// <summary>When this job is next due, in UTC. Computed from the fields above on save and
    /// after each run, so the worker's query stays a plain comparison and no timezone maths
    /// happens in SQL.</summary>
    public DateTimeOffset NextRunAt { get; set; }

    public DateTimeOffset? LastRunAt { get; set; }

    /// <summary>Paused rather than deleted, for the same reason chains and departments are:
    /// the messages it already produced point at it.</summary>
    public bool IsActive { get; set; } = true;
}
