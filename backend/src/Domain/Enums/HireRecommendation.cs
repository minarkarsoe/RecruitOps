namespace RecruitOps.Domain.Enums;

/// <summary>The interviewer's overall call, separate from the per-criterion scores.
/// <para>Four options with no middle: a neutral option is chosen by default when someone
/// is unsure, and a panel of "maybes" tells the hiring manager nothing.</para></summary>
public enum HireRecommendation
{
    StrongNo,
    No,
    Yes,
    StrongYes,
}
