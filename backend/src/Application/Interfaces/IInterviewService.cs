using RecruitOps.Application.DTOs;

namespace RecruitOps.Application.Interfaces;

/// <summary>Module 3.1 (manual slice) — scheduling interview rounds.
/// <para>Every method returns <c>null</c> for "no such thing, or not yours" so the caller
/// answers 404 and never leaks existence, and throws
/// <see cref="InvalidOperationException"/> for a rule violation the caller should see
/// as a 409. Same contract as <c>IRequisitionService</c>.</para></summary>
public interface IInterviewService
{
    /// <summary>Schedules a round and, in the same transaction, moves the application to
    /// the Interview stage with a stage-history row (ADR-0017 §5).</summary>
    Task<InterviewDto?> ScheduleAsync(
        Guid jobApplicationId, ScheduleInterviewRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<InterviewDto>?> ListForApplicationAsync(
        Guid jobApplicationId, CancellationToken ct = default);

    /// <summary>Every interview the caller may see, newest first (Module 3, the interviews list).
    ///
    /// <para><b>Reach is the same rule the detail screen uses</b>, and that is the point: a list
    /// that shows less than the detail screen will open produces "I can open it from the board
    /// but it is not in my list", and a list that shows more leaks. Both clauses of
    /// <c>IApplicationAccess.ResolveAsync</c> apply — the candidate axis
    /// (<see cref="IDepartmentAccess.CanReachCandidatesInAsync"/>, which bundles ADR-0003 scoping
    /// with the ADR-0018 exclusion), <b>or</b> sitting on the interview's panel (ADR-0017 §4).
    /// The second clause is why a role excluded from candidate data still sees the rounds it was
    /// invited to and nothing else.</para></summary>
    /// <param name="statuses">Statuses to include. Empty or null means the default view, which
    /// omits <c>Cancelled</c> — a cancelled round is kept as history, not shown by default.</param>
    /// <param name="onlyMine">Restrict to interviews the caller is on the panel for.</param>
    Task<IReadOnlyList<InterviewListItemDto>> ListAsync(
        IReadOnlyCollection<string>? statuses = null,
        bool onlyMine = false,
        CancellationToken ct = default);

    Task<InterviewDto?> GetAsync(Guid interviewId, CancellationToken ct = default);

    Task<InterviewDto?> RescheduleAsync(
        Guid interviewId, RescheduleInterviewRequest request, CancellationToken ct = default);

    Task<InterviewDto?> SetPanelAsync(
        Guid interviewId, SetPanelRequest request, CancellationToken ct = default);

    Task<InterviewDto?> CancelAsync(
        Guid interviewId, CancelInterviewRequest request, CancellationToken ct = default);

    Task<InterviewDto?> CompleteAsync(
        Guid interviewId, CompleteInterviewRequest request, CancellationToken ct = default);
}
