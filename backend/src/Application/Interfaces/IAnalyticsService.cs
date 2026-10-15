using RecruitOps.Application.DTOs;

namespace RecruitOps.Application.Interfaces;

public interface IAnalyticsService
{
    Task<KpiMetricsDto> GetKpiMetricsAsync(CancellationToken ct = default);
    Task<TimeToHireAnalyticsDto> GetTimeToHireAsync(CancellationToken ct = default);
    Task<ConversionFunnelAnalyticsDto> GetConversionFunnelAsync(CancellationToken ct = default);
    Task<SourceOfHireAnalyticsDto> GetSourceOfHireAsync(CancellationToken ct = default);
    Task<ReportQueryResultDto> QueryReportAsync(ReportQueryRequestDto query, CancellationToken ct = default);
    Task<byte[]> ExportReportCsvAsync(ReportQueryRequestDto query, CancellationToken ct = default);
}
