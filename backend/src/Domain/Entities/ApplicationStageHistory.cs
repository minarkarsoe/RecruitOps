using RecruitOps.Domain.Common;
using RecruitOps.Domain.Enums;

namespace RecruitOps.Domain.Entities;

/// <summary>Append-only record of every pipeline stage an application has been in.
///
/// <para><b>Written from day one, before anything reads it.</b> Module 5's headline metrics
/// — time-to-hire, stage conversion, where candidates drop out — are differences between
/// timestamps. `JobApplication.Status` only stores the present, so history that wasn't
/// recorded as it happened cannot be reconstructed later: the analytics module would launch
/// blind and stay blind until enough new data accumulated. That is why this exists now,
/// months before the screen that uses it.</para>
///
/// <para>Nothing updates or deletes these rows. A correction is a new row.</para>
/// </summary>
public class ApplicationStageHistory : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid JobApplicationId { get; set; }

    /// <summary>Null on the first row — the application entering the pipeline.</summary>
    public PipelineStatus? FromStatus { get; set; }

    public PipelineStatus ToStatus { get; set; }

    public DateTimeOffset ChangedAt { get; set; }

    /// <summary>Null when the change wasn't made by a logged-in user — a public
    /// application creates the first row with no actor.</summary>
    public Guid? ChangedByUserId { get; set; }

    public string? Note { get; set; }
}
