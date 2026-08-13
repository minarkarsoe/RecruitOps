using RecruitOps.Domain.Enums;

namespace RecruitOps.Application.DTOs;

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

public record StageFunnelItemDto(
    string Stage,
    int Count,
    double DropOffRate
);

public record ConversionFunnelAnalyticsDto(
    IReadOnlyList<StageFunnelItemDto> Funnel
);

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

