using RecruitOps.Application.DTOs;

namespace RecruitOps.Application.Interfaces;

/// <summary>Module 3.3 — filling in and reading evaluations.</summary>
public interface IScorecardService
{
    /// <summary>The caller's own scorecard for this interview, with the criteria to answer.
    /// Creates nothing; a scorecard that hasn't been started comes back null inside the
    /// envelope so the form can still be rendered.</summary>
    Task<MyScorecardDto?> GetMineAsync(Guid interviewId, CancellationToken ct = default);

    /// <summary>Saves the caller's draft. Idempotent — answers are replaced wholesale.</summary>
    Task<MyScorecardDto?> SaveMineAsync(
        Guid interviewId, SaveScorecardRequest request, CancellationToken ct = default);

    /// <summary>Submits the caller's scorecard. Irreversible: a submitted evaluation that
    /// could be edited after reading the panel's would defeat the blind rule entirely.</summary>
    Task<MyScorecardDto?> SubmitMineAsync(
        Guid interviewId, SaveScorecardRequest request, CancellationToken ct = default);

    /// <summary>The panel's evaluations, filtered by the blind rule (ADR-0017 §3).</summary>
    Task<InterviewScorecardsDto?> GetForInterviewAsync(
        Guid interviewId, CancellationToken ct = default);
}
