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
