using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RecruitOps.Api.Attributes;
using RecruitOps.Api.Auth;
using RecruitOps.Application.DTOs;
using RecruitOps.Application.Interfaces;

namespace RecruitOps.Api.Controllers;

/// <summary>Module 5 — Reporting &amp; Analytics APIs.
/// <para>Uses <see cref="Policies.InternalUser"/> policy so internal roles (Admin, HR Director,
/// Recruiter, Hiring Manager) can access analytics. Row-level visibility and department
/// reach scoping (ADR-0003) are enforced by <see cref="IAnalyticsService"/>.</para></summary>
[ApiController]
[Route("api/analytics")]
[Authorize(Policy = Policies.InternalUser)]
[FeatureGate("EnableAnalytics")]
public class AnalyticsController : ControllerBase
{
    private readonly IAnalyticsService _analyticsService;

    public AnalyticsController(IAnalyticsService analyticsService)
    {
        _analyticsService = analyticsService;
    }

    [HttpGet("kpis")]
    public async Task<ActionResult<KpiMetricsDto>> GetKpis(CancellationToken ct)
    {
        var result = await _analyticsService.GetKpiMetricsAsync(ct);
        return Ok(result);
    }

    [HttpGet("time-to-hire")]
    public async Task<ActionResult<TimeToHireAnalyticsDto>> GetTimeToHire(CancellationToken ct)
    {
        var result = await _analyticsService.GetTimeToHireAsync(ct);
        return Ok(result);
    }

    [HttpGet("conversion")]
    public async Task<ActionResult<ConversionFunnelAnalyticsDto>> GetConversion(CancellationToken ct)
    {
        var result = await _analyticsService.GetConversionFunnelAsync(ct);
        return Ok(result);
    }

    [HttpGet("source-of-hire")]
    public async Task<ActionResult<SourceOfHireAnalyticsDto>> GetSourceOfHire(CancellationToken ct)
    {
        var result = await _analyticsService.GetSourceOfHireAsync(ct);
        return Ok(result);
    }

    [HttpPost("reports/query")]
    public async Task<ActionResult<ReportQueryResultDto>> QueryReport([FromBody] ReportQueryRequestDto request, CancellationToken ct)
    {
        var result = await _analyticsService.QueryReportAsync(request, ct);
        return Ok(result);
    }

    [HttpGet("reports/export")]
    public async Task<IActionResult> ExportReport([FromQuery] ReportQueryRequestDto request, CancellationToken ct)
    {
        var csvBytes = await _analyticsService.ExportReportCsvAsync(request, ct);
        return File(csvBytes, "text/csv", "report.csv");
    }
}
