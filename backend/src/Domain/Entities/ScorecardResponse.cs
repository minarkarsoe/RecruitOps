using RecruitOps.Domain.Common;
using RecruitOps.Domain.Enums;

namespace RecruitOps.Domain.Entities;

/// <summary>One answer, against one criterion, on one scorecard.
///
/// <para><b>The criterion is snapshotted here</b> (ADR-0017 §2). <see cref="CriterionLabel"/>
/// and <see cref="CriterionType"/> record how the question read when it was answered, not
/// how it reads today. Without that, renaming a criterion from "Communication" to
/// "Stakeholder management" in September retroactively changes the meaning of every score
/// recorded against it in July, and the evaluation history quietly becomes a lie — the same
/// reason Module 1 snapshots the approval chain on submit.</para>
///
/// <para>The FK is kept as well, so analytics over the <i>current</i> template still group
/// correctly. The snapshot is what makes an old scorecard readable on its own terms.</para>
/// </summary>
public class ScorecardResponse : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid ScorecardId { get; set; }
    public Guid ScorecardCriterionId { get; set; }

    /// <summary>The criterion's label as it read at submission.</summary>
    public string CriterionLabel { get; set; } = string.Empty;

    /// <summary>The criterion's type as it read at submission — which of the three value
    /// columns below is the meaningful one.</summary>
    public CriterionType CriterionType { get; set; }

    /// <summary>1–5, for <see cref="Enums.CriterionType.Rating"/>.</summary>
    public int? Rating { get; set; }

    /// <summary>For <see cref="Enums.CriterionType.YesNo"/>.</summary>
    public bool? YesNo { get; set; }

    /// <summary>Free text for <see cref="Enums.CriterionType.Text"/>, and optional
    /// supporting evidence for the other two.</summary>
    public string? Comment { get; set; }
}
