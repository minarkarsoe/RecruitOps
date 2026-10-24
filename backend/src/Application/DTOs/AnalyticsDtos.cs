using RecruitOps.Domain.Enums;

namespace RecruitOps.Application.DTOs;

/// <summary>The August KPI tiles, no longer on any screen (ADR-0031 §5). ⚠️ <c>AvgTimeToHireDays</c> here
/// is days from an application's first stage to the <b>Hired stage</b> — not Time-to-Hire, which ends at
/// offer acceptance (<see cref="ExecutiveDashboardDto"/>).</summary>
public record KpiMetricsDto(
    double AvgTimeToHireDays,
    int ActiveRequisitions,
    int TotalApplications,
    double OverallHireRate
);

public record StageDurationDto(
    string Stage,
    double AvgDays
);

public record DepartmentTimeDto(
    Guid DepartmentId,
    string DepartmentName,
    double AvgDays,
    int HiredCount
);

public record PostingTimeDto(
    Guid JobPostingId,
    string PostingTitle,
    double AvgDays,
    int HiredCount
);

public record TimeToHireAnalyticsDto(
    IReadOnlyList<StageDurationDto> StageDurations,
    IReadOnlyList<DepartmentTimeDto> DepartmentBreakdown,
    IReadOnlyList<PostingTimeDto> PostingBreakdown
);

/// <param name="DropOffRate">Percent of the previous stage's count that did not reach this one, 0–100.</param>
public record StageFunnelItemDto(
    string Stage,
    int Count,
    double DropOffRate
);

public record ConversionFunnelAnalyticsDto(
    IReadOnlyList<StageFunnelItemDto> Funnel
);

/// <param name="Percentage">This channel's share of all applications, 0–100.</param>
public record SourceDistributionItemDto(
    string Source,
    int Count,
    double Percentage
);

public record SourceOfHireAnalyticsDto(
    IReadOnlyList<SourceDistributionItemDto> Sources
);

public record ReportQueryRequestDto(
    DateTimeOffset? DateFrom = null,
    DateTimeOffset? DateTo = null,
    Guid? DepartmentId = null,
    Guid? JobPostingId = null,
    List<PipelineStatus>? Stages = null,
    List<string>? Columns = null
);

public record ReportQueryResultDto(
    IReadOnlyList<string> Headers,
    IReadOnlyList<Dictionary<string, object?>> Rows
);

// ── Executive dashboard (Module 5.1, ADR-0031) ──────────────────────────────

/// <param name="Kind"><c>Month</c>, <c>Quarter</c> or <c>Year</c>.</param>
public record ReportingPeriodDto(string Kind, DateOnly From, DateOnly To);

/// <summary>An average over accepted offers, in days. Null when none fell in the period — never 0.</summary>
public record DurationMetricDto(double? Days, int Count, double? PreviousDays, int PreviousCount);

/// <summary>Accepted ÷ (accepted + declined + expired), by when each outcome fell.</summary>
public record AcceptanceMetricDto(int Accepted, int Decided, double? Rate, int PreviousAccepted, int PreviousDecided, double? PreviousRate);

public record DepartmentVacanciesDto(Guid DepartmentId, string DepartmentName, int Open);

public record FilterOptionDto(Guid Id, string Name);

/// <param name="Scope"><c>Company</c> (Admin, HR Director), <c>Recruiter</c> (the offers they made) or
/// <c>Department</c> (a Hiring Manager's departments).</param>
/// <param name="TimeToHire">Null for a Hiring Manager — omitted, not hidden (ADR-0031 §3).</param>
/// <param name="Acceptance">Null for a Hiring Manager.</param>
/// <param name="Recruiters">The recruiter filter's choices: empty unless the caller sees company-wide.</param>
public record ExecutiveDashboardDto(
    string Scope,
    ReportingPeriodDto Period,
    ReportingPeriodDto Previous,
    DurationMetricDto? TimeToHire,
    DurationMetricDto TimeToFill,
    AcceptanceMetricDto? Acceptance,
    IReadOnlyList<DepartmentVacanciesDto> Vacancies,
    int OpenVacancies,
    IReadOnlyList<FilterOptionDto> Departments,
    IReadOnlyList<FilterOptionDto> Recruiters);

// ── Pipeline & source (Module 5.2, ADR-0032) ────────────────────────────────

/// <param name="Channel"><c>Facebook</c>, <c>LinkedIn</c>, <c>CareerPage</c>, <c>ManualUpload</c> or <c>Other</c>.</param>
/// <param name="Share">Percent of the period's applications, one decimal; null when there are none.</param>
/// <param name="HireRate">Percent of this channel's applications with an accepted offer; null when it has none.</param>
public record ChannelShareDto(string Channel, int Candidates, double? Share, int Hired, double? HireRate);

/// <param name="Band"><c>Sourcing</c>, <c>Screened</c>, <c>Interview</c> or <c>Offered</c>.</param>
/// <param name="DropOff">Percent of the band before that did not reach this one; null for the first band, or
/// when the band before is empty.</param>
public record FunnelBandDto(string Band, int Count, double? DropOff);

/// <summary>One person's work in the period — never a rank (ADR-0032 §5).</summary>
public record LeaderboardRowDto(Guid UserId, string Name, int CvsIn, int Interviews, int Hires);

/// <param name="Scope"><c>Company</c> (Admin, HR Director) or <c>Recruiter</c>.</param>
/// <param name="Window"><c>90d</c> or <c>year</c>.</param>
/// <param name="Leaderboard">Null unless the scope is <c>Company</c> — omitted by the API, not hidden by the
/// screen.</param>
/// <param name="Positions">The position filter's choices: postings with applications in the window, whatever
/// the other filters say.</param>
public record PipelineAnalyticsDto(
    string Scope,
    string Window,
    DateOnly From,
    DateOnly To,
    int Candidates,
    IReadOnlyList<ChannelShareDto> Sources,
    IReadOnlyList<FunnelBandDto> Funnel,
    IReadOnlyList<LeaderboardRowDto>? Leaderboard,
    IReadOnlyList<FilterOptionDto> Positions);
