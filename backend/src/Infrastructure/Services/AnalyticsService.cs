using System.Text;
using Microsoft.EntityFrameworkCore;
using RecruitOps.Application.Common;
using RecruitOps.Application.DTOs;
using RecruitOps.Application.Interfaces;
using RecruitOps.Domain.Entities;
using RecruitOps.Domain.Enums;
using RecruitOps.Infrastructure.Persistence;

namespace RecruitOps.Infrastructure.Services;

public class AnalyticsService : IAnalyticsService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IDepartmentAccess _access;

    public AnalyticsService(AppDbContext db, ICurrentUser user, IDepartmentAccess access)
    {
        _db = db;
        _user = user;
        _access = access;
    }

    private async Task<(bool Denied, HashSet<Guid>? DepartmentIds)> GetAllowedDepartmentIdsAsync(CancellationToken ct)
    {
        // 1. Approvers are excluded from candidate data (ADR-0018)
        if (_user.IsExcludedFromCandidateData)
            return (true, null);

        // 2. Department-scoped roles (e.g., HiringManager) (ADR-0003)
        if (_user.IsDepartmentScoped)
        {
            var ids = await _access.AccessibleDepartmentIdsAsync(ct);
            if (ids.Count == 0)
                return (true, null);
            return (false, ids.ToHashSet());
        }

        // 3. Unscoped roles (Admin, HrDirector, Recruiter)
        return (false, null);
    }

    public async Task<KpiMetricsDto> GetKpiMetricsAsync(CancellationToken ct = default)
    {
        var (denied, allowedDeptIds) = await GetAllowedDepartmentIdsAsync(ct);
        if (denied)
            return new KpiMetricsDto(0.0, 0, 0, 0.0);

        // Active requisitions with Approved status in scoped departments
        var reqQuery = _db.Requisitions.AsNoTracking();
        if (allowedDeptIds is not null)
        {
            reqQuery = reqQuery.Where(r => allowedDeptIds.Contains(r.DepartmentId));
        }
        var activeRequisitions = await reqQuery.CountAsync(r => r.Status == RequisitionStatus.Approved, ct);

        // In-scope job postings
        var postingQuery = _db.JobPostings.AsNoTracking();
        if (allowedDeptIds is not null)
        {
            postingQuery = postingQuery.Where(p => allowedDeptIds.Contains(p.DepartmentId));
        }
        var postingIds = await postingQuery.Select(p => p.Id).ToListAsync(ct);

        var appQuery = _db.JobApplications.AsNoTracking()
            .Where(a => postingIds.Contains(a.JobPostingId));

        var totalApplications = await appQuery.CountAsync(ct);
        if (totalApplications == 0)
            return new KpiMetricsDto(0.0, activeRequisitions, 0, 0.0);

        var hiredApps = await appQuery
            .Where(a => a.Status == PipelineStatus.Hired)
            .Select(a => new { a.Id, a.AppliedAt, a.UpdatedAt })
            .ToListAsync(ct);

        var hiredCount = hiredApps.Count;
        var overallHireRate = Math.Round((double)hiredCount / totalApplications * 100.0, 1);

        double avgTimeToHireDays = 0.0;
        if (hiredCount > 0)
        {
            var hiredAppIds = hiredApps.Select(a => a.Id).ToList();

            var hiredHistories = await _db.ApplicationStageHistories.AsNoTracking()
                .Where(h => hiredAppIds.Contains(h.JobApplicationId) && h.ToStatus == PipelineStatus.Hired)
                .GroupBy(h => h.JobApplicationId)
                .Select(g => new { JobApplicationId = g.Key, HiredAt = g.Min(h => h.ChangedAt) })
                .ToDictionaryAsync(x => x.JobApplicationId, x => x.HiredAt, ct);

            var initialHistories = await _db.ApplicationStageHistories.AsNoTracking()
                .Where(h => hiredAppIds.Contains(h.JobApplicationId))
                .GroupBy(h => h.JobApplicationId)
                .Select(g => new { JobApplicationId = g.Key, StartAt = g.Min(h => h.ChangedAt) })
                .ToDictionaryAsync(x => x.JobApplicationId, x => x.StartAt, ct);

            var totalDaysList = new List<double>();
            foreach (var app in hiredApps)
            {
                var start = initialHistories.TryGetValue(app.Id, out var initHist) ? initHist : app.AppliedAt;
                var end = hiredHistories.TryGetValue(app.Id, out var hiredHist) ? hiredHist : app.UpdatedAt;

                var days = (end - start)?.TotalDays ?? 0.0;
                if (days < 0) days = 0;
                totalDaysList.Add(days);
            }

            avgTimeToHireDays = totalDaysList.Count > 0 ? Math.Round(totalDaysList.Average(), 1) : 0.0;
        }

        return new KpiMetricsDto(avgTimeToHireDays, activeRequisitions, totalApplications, overallHireRate);
    }

    public async Task<TimeToHireAnalyticsDto> GetTimeToHireAsync(CancellationToken ct = default)
    {
        var (denied, allowedDeptIds) = await GetAllowedDepartmentIdsAsync(ct);
        if (denied)
        {
            return new TimeToHireAnalyticsDto(
                Array.Empty<StageDurationDto>(),
                Array.Empty<DepartmentTimeDto>(),
                Array.Empty<PostingTimeDto>()
            );
        }

        var postingQuery = _db.JobPostings.AsNoTracking();
        if (allowedDeptIds is not null)
        {
            postingQuery = postingQuery.Where(p => allowedDeptIds.Contains(p.DepartmentId));
        }
        var postings = await postingQuery.ToListAsync(ct);
        var postingIds = postings.Select(p => p.Id).ToList();

        var appQuery = _db.JobApplications.AsNoTracking()
            .Where(a => postingIds.Contains(a.JobPostingId));
        var apps = await appQuery.ToListAsync(ct);
        var appIds = apps.Select(a => a.Id).ToList();

        var stageDurationsList = new List<StageDurationDto>();
        var stages = new[]
        {
            PipelineStatus.Sourced,
            PipelineStatus.Applied,
            PipelineStatus.Screening,
            PipelineStatus.Shortlisted,
            PipelineStatus.Interview,
            PipelineStatus.Offer,
            PipelineStatus.Hired
        };

        if (appIds.Count > 0)
        {
            var histories = await _db.ApplicationStageHistories.AsNoTracking()
                .Where(h => appIds.Contains(h.JobApplicationId))
                .OrderBy(h => h.JobApplicationId)
                .ThenBy(h => h.ChangedAt)
                .ToListAsync(ct);

            var stageDaysMap = stages.ToDictionary(s => s, _ => new List<double>());

            var groupedByApp = histories.GroupBy(h => h.JobApplicationId);
            foreach (var group in groupedByApp)
            {
                var list = group.ToList();
                for (int i = 0; i < list.Count - 1; i++)
                {
                    var cur = list[i];
                    var next = list[i + 1];
                    var stage = cur.ToStatus;
                    var duration = (next.ChangedAt - cur.ChangedAt).TotalDays;
                    if (duration >= 0 && stageDaysMap.ContainsKey(stage))
                    {
                        stageDaysMap[stage].Add(duration);
                    }
                }
            }

            foreach (var stage in stages)
            {
                var daysList = stageDaysMap[stage];
                var avgDays = daysList.Count > 0 ? Math.Round(daysList.Average(), 1) : 0.0;
                stageDurationsList.Add(new StageDurationDto(stage.ToString(), avgDays));
            }
        }
        else
        {
            foreach (var stage in stages)
            {
                stageDurationsList.Add(new StageDurationDto(stage.ToString(), 0.0));
            }
        }

        var hiredApps = apps.Where(a => a.Status == PipelineStatus.Hired).ToList();
        var hiredAppIds = hiredApps.Select(a => a.Id).ToList();

        var hiredHistories = hiredAppIds.Count > 0
            ? await _db.ApplicationStageHistories.AsNoTracking()
                .Where(h => hiredAppIds.Contains(h.JobApplicationId) && h.ToStatus == PipelineStatus.Hired)
                .GroupBy(h => h.JobApplicationId)
                .Select(g => new { JobApplicationId = g.Key, HiredAt = g.Min(h => h.ChangedAt) })
                .ToDictionaryAsync(x => x.JobApplicationId, x => x.HiredAt, ct)
            : new Dictionary<Guid, DateTimeOffset>();

        var initialHistories = hiredAppIds.Count > 0
            ? await _db.ApplicationStageHistories.AsNoTracking()
                .Where(h => hiredAppIds.Contains(h.JobApplicationId))
                .GroupBy(h => h.JobApplicationId)
                .Select(g => new { JobApplicationId = g.Key, StartAt = g.Min(h => h.ChangedAt) })
                .ToDictionaryAsync(x => x.JobApplicationId, x => x.StartAt, ct)
            : new Dictionary<Guid, DateTimeOffset>();

        var appTimeMap = new Dictionary<Guid, double>();
        foreach (var app in hiredApps)
        {
            var start = initialHistories.TryGetValue(app.Id, out var initHist) ? initHist : app.AppliedAt;
            var end = hiredHistories.TryGetValue(app.Id, out var hiredHist) ? hiredHist : app.UpdatedAt;
            var days = (end - start)?.TotalDays ?? 0.0;
            if (days < 0) days = 0;
            appTimeMap[app.Id] = days;
        }

        var deptMap = await _db.Departments.AsNoTracking()
            .ToDictionaryAsync(d => d.Id, d => d.Name, ct);

        var deptBreakdown = postings
            .GroupBy(p => p.DepartmentId)
            .Select(g =>
            {
                var deptId = g.Key;
                var deptName = deptMap.TryGetValue(deptId, out var name) ? name : "Unknown";
                var deptPostingIds = g.Select(p => p.Id).ToHashSet();
                var deptHiredApps = hiredApps.Where(a => deptPostingIds.Contains(a.JobPostingId)).ToList();
                var hiredCount = deptHiredApps.Count;
                var avgDays = hiredCount > 0
                    ? Math.Round(deptHiredApps.Average(a => appTimeMap.TryGetValue(a.Id, out var d) ? d : 0.0), 1)
                    : 0.0;
                return new DepartmentTimeDto(deptId, deptName, avgDays, hiredCount);
            })
            .OrderBy(d => d.DepartmentName)
            .ToList();

        var postingBreakdown = postings
            .Select(p =>
            {
                var pHiredApps = hiredApps.Where(a => a.JobPostingId == p.Id).ToList();
                var hiredCount = pHiredApps.Count;
                var avgDays = hiredCount > 0
                    ? Math.Round(pHiredApps.Average(a => appTimeMap.TryGetValue(a.Id, out var d) ? d : 0.0), 1)
                    : 0.0;
                return new PostingTimeDto(p.Id, p.Title, avgDays, hiredCount);
            })
            .OrderBy(p => p.PostingTitle)
            .ToList();

        return new TimeToHireAnalyticsDto(stageDurationsList, deptBreakdown, postingBreakdown);
    }

    public async Task<ConversionFunnelAnalyticsDto> GetConversionFunnelAsync(CancellationToken ct = default)
    {
        var (denied, allowedDeptIds) = await GetAllowedDepartmentIdsAsync(ct);
        if (denied)
        {
            return new ConversionFunnelAnalyticsDto(Array.Empty<StageFunnelItemDto>());
        }

        var postingQuery = _db.JobPostings.AsNoTracking();
        if (allowedDeptIds is not null)
        {
            postingQuery = postingQuery.Where(p => allowedDeptIds.Contains(p.DepartmentId));
        }
        var postingIds = await postingQuery.Select(p => p.Id).ToListAsync(ct);

        var apps = await _db.JobApplications.AsNoTracking()
            .Where(a => postingIds.Contains(a.JobPostingId))
            .ToListAsync(ct);

        var appIds = apps.Select(a => a.Id).ToList();

        var stageOrder = new[]
        {
            PipelineStatus.Sourced,
            PipelineStatus.Applied,
            PipelineStatus.Screening,
            PipelineStatus.Shortlisted,
            PipelineStatus.Interview,
            PipelineStatus.Offer,
            PipelineStatus.Hired
        };

        var stageOrderIndex = stageOrder
            .Select((stage, index) => new { stage, index })
            .ToDictionary(x => x.stage, x => x.index);

        var appHistories = appIds.Count > 0
            ? await _db.ApplicationStageHistories.AsNoTracking()
                .Where(h => appIds.Contains(h.JobApplicationId))
                .ToListAsync(ct)
            : new List<ApplicationStageHistory>();

        var appHistoryStages = appHistories
            .GroupBy(h => h.JobApplicationId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(h => h.ToStatus).Concat(g.Where(h => h.FromStatus.HasValue).Select(h => h.FromStatus!.Value)).ToHashSet()
            );

        var funnelItems = new List<StageFunnelItemDto>();
        int previousCount = 0;

        for (int i = 0; i < stageOrder.Length; i++)
        {
            var stage = stageOrder[i];
            var stageIndex = i;

            var count = apps.Count(app =>
            {
                if (stageOrderIndex.TryGetValue(app.Status, out var currIdx) && currIdx >= stageIndex)
                    return true;

                if (appHistoryStages.TryGetValue(app.Id, out var passedStages))
                {
                    if (passedStages.Any(s => stageOrderIndex.TryGetValue(s, out var idx) && idx >= stageIndex))
                        return true;
                }

                return false;
            });

            double dropOffRate = 0.0;
            if (i > 0 && previousCount > 0)
            {
                dropOffRate = Math.Round((1.0 - (double)count / previousCount) * 100.0, 1);
                if (dropOffRate < 0) dropOffRate = 0.0;
            }

            funnelItems.Add(new StageFunnelItemDto(stage.ToString(), count, dropOffRate));
            previousCount = count;
        }

        return new ConversionFunnelAnalyticsDto(funnelItems);
    }

    public async Task<SourceOfHireAnalyticsDto> GetSourceOfHireAsync(CancellationToken ct = default)
    {
        var (denied, allowedDeptIds) = await GetAllowedDepartmentIdsAsync(ct);
        if (denied)
        {
            return new SourceOfHireAnalyticsDto(Array.Empty<SourceDistributionItemDto>());
        }

        var postingQuery = _db.JobPostings.AsNoTracking();
        if (allowedDeptIds is not null)
        {
            postingQuery = postingQuery.Where(p => allowedDeptIds.Contains(p.DepartmentId));
        }
        var postingIds = await postingQuery.Select(p => p.Id).ToListAsync(ct);

        var apps = await _db.JobApplications.AsNoTracking()
            .Where(a => postingIds.Contains(a.JobPostingId))
            .ToListAsync(ct);

        var totalApps = apps.Count;
        var allChannels = Enum.GetValues<SourceChannel>();

        var items = allChannels.Select(channel =>
        {
            var count = apps.Count(a => a.Source == channel);
            var percentage = totalApps > 0 ? Math.Round((double)count / totalApps * 100.0, 1) : 0.0;
            return new SourceDistributionItemDto(channel.ToString(), count, percentage);
        }).ToList();

        return new SourceOfHireAnalyticsDto(items);
    }

    private record ReportDataRow(
        JobApplication Application,
        JobPosting Posting,
        Department Department,
        Candidate Candidate
    );

    private record ReportColumnDef(
        string Key,
        string Header,
        Func<ReportDataRow, string> Evaluator
    );

    private static readonly List<ReportColumnDef> AvailableColumns = new()
    {
        new("candidateName", "Candidate Name", x => x.Candidate.FullName),
        new("candidateEmail", "Candidate Email", x => x.Candidate.Email ?? ""),
        new("candidatePhone", "Candidate Phone", x => x.Candidate.Phone ?? ""),
        new("jobTitle", "Job Title", x => x.Posting.Title),
        new("department", "Department", x => x.Department.Name),
        new("stage", "Stage", x => x.Application.Status.ToString()),
        new("source", "Source", x => x.Application.Source.ToString()),
        new("appliedAt", "Applied Date", x => x.Application.AppliedAt.ToString("yyyy-MM-dd HH:mm:ss")),
        new("resumeFileName", "Resume File", x => x.Application.ResumeFileName ?? ""),
        new("applicationId", "Application ID", x => x.Application.Id.ToString())
    };

    private static readonly Dictionary<string, ReportColumnDef> ColumnLookup =
        AvailableColumns.ToDictionary(c => c.Key, c => c, StringComparer.OrdinalIgnoreCase);

    private static readonly List<string> DefaultColumnKeys = new()
    {
        "candidateName", "candidateEmail", "jobTitle", "department", "stage", "source", "appliedAt"
    };

    private static List<ReportColumnDef> ResolveColumns(List<string>? requestedColumns)
    {
        if (requestedColumns != null && requestedColumns.Count > 0)
        {
            var resolved = new List<ReportColumnDef>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var col in requestedColumns)
            {
                if (!string.IsNullOrWhiteSpace(col) && ColumnLookup.TryGetValue(col.Trim(), out var colDef) && seen.Add(colDef.Key))
                {
                    resolved.Add(colDef);
                }
            }
            if (resolved.Count > 0)
                return resolved;
        }

        return DefaultColumnKeys.Select(k => ColumnLookup[k]).ToList();
    }

    private async Task<List<ReportDataRow>> FetchReportDataAsync(ReportQueryRequestDto query, CancellationToken ct)
    {
        var (denied, allowedDeptIds) = await GetAllowedDepartmentIdsAsync(ct);
        if (denied)
            return new List<ReportDataRow>();

        var baseQuery = from app in _db.JobApplications.AsNoTracking()
                        join posting in _db.JobPostings.AsNoTracking() on app.JobPostingId equals posting.Id
                        join dept in _db.Departments.AsNoTracking() on posting.DepartmentId equals dept.Id
                        join candidate in _db.Candidates.AsNoTracking() on app.CandidateId equals candidate.Id
                        select new { Application = app, Posting = posting, Department = dept, Candidate = candidate };

        if (allowedDeptIds is not null)
        {
            if (query.DepartmentId.HasValue)
            {
                if (!allowedDeptIds.Contains(query.DepartmentId.Value))
                    return new List<ReportDataRow>();
                baseQuery = baseQuery.Where(x => x.Posting.DepartmentId == query.DepartmentId.Value);
            }
            else
            {
                baseQuery = baseQuery.Where(x => allowedDeptIds.Contains(x.Posting.DepartmentId));
            }
        }
        else if (query.DepartmentId.HasValue)
        {
            baseQuery = baseQuery.Where(x => x.Posting.DepartmentId == query.DepartmentId.Value);
        }

        if (query.JobPostingId.HasValue)
        {
            baseQuery = baseQuery.Where(x => x.Posting.Id == query.JobPostingId.Value);
        }

        if (query.DateFrom.HasValue)
        {
            baseQuery = baseQuery.Where(x => x.Application.AppliedAt >= query.DateFrom.Value);
        }

        if (query.DateTo.HasValue)
        {
            baseQuery = baseQuery.Where(x => x.Application.AppliedAt <= query.DateTo.Value);
        }

        if (query.Stages != null && query.Stages.Count > 0)
        {
            baseQuery = baseQuery.Where(x => query.Stages.Contains(x.Application.Status));
        }

        var list = await baseQuery
            .OrderByDescending(x => x.Application.AppliedAt)
            .ToListAsync(ct);

        return list
            .Select(x => new ReportDataRow(x.Application, x.Posting, x.Department, x.Candidate))
            .ToList();
    }

    public async Task<ReportQueryResultDto> QueryReportAsync(ReportQueryRequestDto query, CancellationToken ct = default)
    {
        query ??= new ReportQueryRequestDto();
        var selectedCols = ResolveColumns(query.Columns);
        var items = await FetchReportDataAsync(query, ct);

        var headers = selectedCols.Select(c => c.Header).ToList();
        var rows = new List<Dictionary<string, object?>>();

        foreach (var item in items)
        {
            var row = new Dictionary<string, object?>();
            foreach (var col in selectedCols)
            {
                row[col.Key] = col.Evaluator(item);
            }
            rows.Add(row);
        }

        return new ReportQueryResultDto(headers, rows);
    }

    public async Task<byte[]> ExportReportCsvAsync(ReportQueryRequestDto query, CancellationToken ct = default)
    {
        query ??= new ReportQueryRequestDto();
        var selectedCols = ResolveColumns(query.Columns);
        var items = await FetchReportDataAsync(query, ct);

        var builder = new StringBuilder();

        // Header row
        builder.AppendLine(string.Join(",", selectedCols.Select(c => EscapeCsvField(c.Header))));

        // Data rows
        foreach (var item in items)
        {
            var rowValues = selectedCols.Select(c => EscapeCsvField(c.Evaluator(item)));
            builder.AppendLine(string.Join(",", rowValues));
        }

        var encoding = new UTF8Encoding(true);
        var contentBytes = encoding.GetBytes(builder.ToString());
        var bom = encoding.GetPreamble();

        var result = new byte[bom.Length + contentBytes.Length];
        Buffer.BlockCopy(bom, 0, result, 0, bom.Length);
        Buffer.BlockCopy(contentBytes, 0, result, bom.Length, contentBytes.Length);

        return result;
    }

    private static string EscapeCsvField(string? field)
    {
        if (string.IsNullOrEmpty(field))
            return string.Empty;

        if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }

        return field;
    }
}
