namespace RecruitOps.Domain.Enums;

/// <summary>What kind of answer a scorecard criterion expects.
/// <para>Deliberately small. Every type added here has to be validated on submit,
/// snapshotted onto the response, and rendered — so the bar for a fourth is a real
/// customer need, not a hypothetical one.</para></summary>
public enum CriterionType
{
    /// <summary>Integer 1–5. The only type that contributes to a numeric comparison.</summary>
    Rating,

    /// <summary>Pass/fail — "has the required certification".</summary>
    YesNo,

    /// <summary>Free text — evidence, not a score.</summary>
    Text,
}
