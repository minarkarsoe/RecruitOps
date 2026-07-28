namespace RecruitOps.Domain.Enums;

/// <summary>Lifecycle of a single interview.
/// <para><see cref="Cancelled"/> and <see cref="NoShow"/> are kept rather than deleted:
/// "the candidate didn't turn up" is a fact Module 5 will want, and a cancelled round is
/// part of how long the loop actually took.</para></summary>
public enum InterviewStatus
{
    Scheduled,
    Completed,
    Cancelled,
    NoShow,
}
