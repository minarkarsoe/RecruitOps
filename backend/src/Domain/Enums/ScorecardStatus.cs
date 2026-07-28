namespace RecruitOps.Domain.Enums;

/// <summary>Whether an evaluation is finished.
/// <para>Load-bearing for the blind-scoring rule (ADR-0017 §3): a <see cref="Draft"/> is
/// visible to its author alone, and submitting is what earns the right to read the
/// rest of the panel.</para></summary>
public enum ScorecardStatus
{
    Draft,
    Submitted,
}
