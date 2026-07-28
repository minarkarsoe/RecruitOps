using RecruitOps.Domain.Common;
using RecruitOps.Domain.Enums;

namespace RecruitOps.Domain.Entities;

/// <summary>One thing an interviewer is asked to assess.</summary>
public class ScorecardCriterion : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid ScorecardTemplateId { get; set; }

    /// <summary>Display order. Derived from list position when a template is saved, so
    /// gaps and duplicates are impossible — the same trick as <c>ApprovalChainStep</c>.</summary>
    public int Sequence { get; set; }

    public string Label { get; set; } = string.Empty;

    /// <summary>What "5" is supposed to mean. The single cheapest thing that makes two
    /// managers' scores comparable, so it is a first-class field rather than a note.</summary>
    public string? Guidance { get; set; }

    public CriterionType Type { get; set; } = CriterionType.Rating;

    /// <summary>Whether the interviewer must answer this before they can submit.</summary>
    public bool IsRequired { get; set; } = true;
}
