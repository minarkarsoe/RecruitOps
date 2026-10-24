using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RecruitOps.Api.Attributes;
using RecruitOps.Api.Auth;
using RecruitOps.Api.Authorization;
using RecruitOps.Application.DTOs;
using RecruitOps.Application.Interfaces;
using RecruitOps.Domain;

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
    private readonly IExecutiveDashboardService _executive;
    private readonly IPipelineAnalyticsService _pipeline;

    public AnalyticsController(
        IAnalyticsService analyticsService, IExecutiveDashboardService executive, IPipelineAnalyticsService pipeline)
    {
        _analyticsService = analyticsService;
        _executive = executive;
        _pipeline = pipeline;
    }

    /// <summary>Module 5.1 — Time-to-Hire, Time-to-Fill, the offer acceptance rate and active vacancies for a
    /// month, quarter (the default) or year to date (ADR-0031). Computed over the caller's own rows: a
    /// Recruiter's offers, a Hiring Manager's departments (without Time-to-Hire or the acceptance rate). An
    /// approver or interviewer, or a department out of reach, is a 404.
    /// <para>Needs <c>requisitions:read</c>, as the SPA's link does: the vacancies are requisitions' seats, and a
    /// custom role trimmed of requisitions should not read them here instead (ADR-0031 §3).</para></summary>
    [HttpGet("executive")]
    [HasPermission("permission:requisitions:requisitions:read")]
    public async Task<ActionResult<ExecutiveDashboardDto>> GetExecutive(
        [FromQuery] string? period, [FromQuery] Guid? departmentId, [FromQuery] Guid? recruiterId, CancellationToken ct)
    {
        if ((period is null ? ReportingPeriod.Kind.Quarter : ReportingPeriod.Parse(period)) is not { } kind)
            return BadRequest(new ProblemDetails { Title = "Invalid period", Detail = "The period is Month, Quarter or Year." });

        return await _executive.GetAsync(kind, departmentId, recruiterId, ct) is { } dto ? Ok(dto) : NotFound();
    }

    /// <summary>Module 5.2 — where candidates came from, how many of each channel were hired, how far they got
    /// in four bands, and (HR Director and Admin only) what each recruiter did, over the applications that arrived
    /// in the last 90 days (the default) or this year (ADR-0032). A Hiring Manager, approver or interviewer has no
    /// access to it: 404, or 403 for an interviewer.</summary>
    /// <param name="source">A channel group: <c>Facebook</c>, <c>LinkedIn</c>, <c>CareerPage</c>,
    /// <c>ManualUpload</c> or <c>Other</c>.</param>
    [HttpGet("pipeline")]
    [HasPermission("permission:requisitions:requisitions:read")]
    public async Task<ActionResult<PipelineAnalyticsDto>> GetPipeline(
        [FromQuery] string? window, [FromQuery] Guid? postingId, [FromQuery] string? source, CancellationToken ct)
    {
        if ((window is null ? PipelineAnalytics.Window.Last90Days : PipelineAnalytics.ParseWindow(window)) is not { } w)
            return BadRequest(new ProblemDetails { Title = "Invalid window", Detail = "The window is 90d or year." });

        PipelineAnalytics.ChannelGroup? group = null;
        if (source is not null)
        {
            if (PipelineAnalytics.ParseGroup(source) is not { } g)
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid source",
                    Detail = "The source is Facebook, LinkedIn, CareerPage, ManualUpload or Other.",
                });
            group = g;
        }

        return await _pipeline.GetAsync(w, postingId, group, ct) is { } dto ? Ok(dto) : NotFound();
    }

    /// <summary>The August KPI tiles — no longer on any screen (ADR-0031 §5). Its <c>AvgTimeToHireDays</c> is
    /// days to the <b>Hired stage</b>; Time-to-Hire, to offer acceptance, is <c>GET executive</c>.</summary>
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
