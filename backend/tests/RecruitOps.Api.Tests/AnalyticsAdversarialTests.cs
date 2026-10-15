using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RecruitOps.Api.Auth;
using RecruitOps.Application.DTOs;
using RecruitOps.Domain.Entities;
using RecruitOps.Domain.Enums;
using RecruitOps.Infrastructure.Persistence;
using Xunit;

namespace RecruitOps.Api.Tests;

public class AnalyticsAdversarialTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;

    public AnalyticsAdversarialTests(CustomWebAppFactory factory)
    {
        _factory = factory;
    }

    private HttpClient ClientFor(string role, Guid? userId = null, Guid? tenantId = null)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Tenant", (tenantId ?? _factory.TenantA).ToString());
        client.DefaultRequestHeaders.Add("X-Test-Roles", role);
        if (userId is not null)
            client.DefaultRequestHeaders.Add("X-Test-UserId", userId.Value.ToString());
        return client;
    }

    [Fact]
    public async Task Adversarial_ZeroDataTenant_ReturnsValidZeroMetricsForAllEndpoints()
    {
        var emptyTenantId = Guid.NewGuid();
        var client = ClientFor(Roles.Admin, tenantId: emptyTenantId);

        // 1. KPIs
        var kpiRes = await client.GetAsync("/api/analytics/kpis");
        Assert.Equal(HttpStatusCode.OK, kpiRes.StatusCode);
        var kpi = await kpiRes.Content.ReadFromJsonAsync<KpiMetricsDto>();
        Assert.NotNull(kpi);
        Assert.Equal(0, kpi!.ActiveRequisitions);
        Assert.Equal(0, kpi.TotalApplications);
        Assert.Equal(0.0, kpi.OverallHireRate);
        Assert.Equal(0.0, kpi.AvgTimeToHireDays);

        // 2. Time to hire
        var timeRes = await client.GetAsync("/api/analytics/time-to-hire");
        Assert.Equal(HttpStatusCode.OK, timeRes.StatusCode);
        var time = await timeRes.Content.ReadFromJsonAsync<TimeToHireAnalyticsDto>();
        Assert.NotNull(time);
        Assert.Equal(7, time!.StageDurations.Count);
        Assert.Empty(time.DepartmentBreakdown);
        Assert.Empty(time.PostingBreakdown);
        foreach (var stage in time.StageDurations)
        {
            Assert.Equal(0.0, stage.AvgDays);
        }

        // 3. Conversion
        var convRes = await client.GetAsync("/api/analytics/conversion");
        Assert.Equal(HttpStatusCode.OK, convRes.StatusCode);
        var conv = await convRes.Content.ReadFromJsonAsync<ConversionFunnelAnalyticsDto>();
        Assert.NotNull(conv);
        Assert.Equal(7, conv!.Funnel.Count);
        foreach (var item in conv.Funnel)
        {
            Assert.Equal(0, item.Count);
            Assert.Equal(0.0, item.DropOffRate);
        }

        // 4. Source of hire
        var srcRes = await client.GetAsync("/api/analytics/source-of-hire");
        Assert.Equal(HttpStatusCode.OK, srcRes.StatusCode);
        var src = await srcRes.Content.ReadFromJsonAsync<SourceOfHireAnalyticsDto>();
        Assert.NotNull(src);
        Assert.Equal(Enum.GetValues<SourceChannel>().Length, src!.Sources.Count);
        foreach (var item in src.Sources)
        {
            Assert.Equal(0, item.Count);
            Assert.Equal(0.0, item.Percentage);
        }
    }

    [Fact]
    public async Task Adversarial_ApproverRole_ReturnsZeroMetricsForAllEndpoints()
    {
        var client = ClientFor(Roles.Approver, _factory.FinanceApproverUserId);

        var kpiRes = await client.GetAsync("/api/analytics/kpis");
        var kpi = await kpiRes.Content.ReadFromJsonAsync<KpiMetricsDto>();
        Assert.Equal(0, kpi!.TotalApplications);

        var timeRes = await client.GetAsync("/api/analytics/time-to-hire");
        var time = await timeRes.Content.ReadFromJsonAsync<TimeToHireAnalyticsDto>();
        Assert.Empty(time!.StageDurations);
        Assert.Empty(time.DepartmentBreakdown);
        Assert.Empty(time.PostingBreakdown);

        var convRes = await client.GetAsync("/api/analytics/conversion");
        var conv = await convRes.Content.ReadFromJsonAsync<ConversionFunnelAnalyticsDto>();
        Assert.Empty(conv!.Funnel);

        var srcRes = await client.GetAsync("/api/analytics/source-of-hire");
        var src = await srcRes.Content.ReadFromJsonAsync<SourceOfHireAnalyticsDto>();
        Assert.Empty(src!.Sources);
    }

    [Fact]
    public async Task Adversarial_OutofOrderTimestamps_DoesNotCauseNegativeDaysOrCrash()
    {
        var tenantId = Guid.NewGuid();
        Guid deptId = Guid.NewGuid();
        Guid postingId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var dept = new Department { TenantId = tenantId, Id = deptId, Name = "Adversarial Dept", Code = "ADV" };
            db.Departments.Add(dept);

            var req = new Requisition
            {
                TenantId = tenantId,
                DepartmentId = deptId,
                Title = "Adversarial Req",
                JobDescription = "Desc",
                Status = RequisitionStatus.Approved
            };
            db.Requisitions.Add(req);

            var posting = new JobPosting
            {
                TenantId = tenantId,
                Id = postingId,
                DepartmentId = deptId,
                RequisitionId = req.Id,
                Title = "Adversarial Posting",
                Description = "Desc",
                Status = JobStatus.Live
            };
            db.JobPostings.Add(posting);

            var cand = new Candidate { TenantId = tenantId, FullName = "Anomaly Cand", Email = "anomaly@test.com", Source = SourceChannel.Telegram };
            db.Candidates.Add(cand);

            // App with AppliedAt in FUTURE, HiredAt in PAST
            var app = new JobApplication
            {
                TenantId = tenantId,
                JobPostingId = postingId,
                CandidateId = cand.Id,
                Status = PipelineStatus.Hired,
                Source = SourceChannel.Telegram,
                AppliedAt = now.AddDays(+5), // Anomaly: future applied date
                UpdatedAt = now.AddDays(-2)
            };
            db.JobApplications.Add(app);
            db.SaveChanges();

            // Stage history out of order
            var hist1 = new ApplicationStageHistory { TenantId = tenantId, JobApplicationId = app.Id, FromStatus = null, ToStatus = PipelineStatus.Applied, ChangedAt = now.AddDays(+5) };
            var hist2 = new ApplicationStageHistory { TenantId = tenantId, JobApplicationId = app.Id, FromStatus = PipelineStatus.Applied, ToStatus = PipelineStatus.Hired, ChangedAt = now.AddDays(-2) };
            db.ApplicationStageHistories.AddRange(hist1, hist2);
            db.SaveChanges();
        }

        var client = ClientFor(Roles.Admin, tenantId: tenantId);

        var kpiRes = await client.GetAsync("/api/analytics/kpis");
        var kpi = await kpiRes.Content.ReadFromJsonAsync<KpiMetricsDto>();
        Assert.NotNull(kpi);
        Assert.True(kpi!.AvgTimeToHireDays >= 0.0, "AvgTimeToHireDays must never be negative");

        var timeRes = await client.GetAsync("/api/analytics/time-to-hire");
        var time = await timeRes.Content.ReadFromJsonAsync<TimeToHireAnalyticsDto>();
        Assert.NotNull(time);
        foreach (var dept in time!.DepartmentBreakdown)
        {
            Assert.True(dept.AvgDays >= 0.0, "Department AvgDays must never be negative");
        }
    }

    [Fact]
    public async Task Adversarial_SourceChannelPercentages_SumTo100Percent()
    {
        var tenantId = Guid.NewGuid();
        Guid deptId = Guid.NewGuid();
        Guid postingId = Guid.NewGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var dept = new Department { TenantId = tenantId, Id = deptId, Name = "Source Dept", Code = "SRC" };
            db.Departments.Add(dept);

            var posting = new JobPosting
            {
                TenantId = tenantId,
                Id = postingId,
                DepartmentId = deptId,
                Title = "Source Posting",
                Description = "Desc",
                Status = JobStatus.Live
            };
            db.JobPostings.Add(posting);

            var c1 = new Candidate { TenantId = tenantId, FullName = "C1", Email = "c1@src.com", Source = SourceChannel.Direct };
            var c2 = new Candidate { TenantId = tenantId, FullName = "C2", Email = "c2@src.com", Source = SourceChannel.Facebook };
            var c3 = new Candidate { TenantId = tenantId, FullName = "C3", Email = "c3@src.com", Source = SourceChannel.LinkedIn };

            db.Candidates.AddRange(c1, c2, c3);
            db.SaveChanges();

            db.JobApplications.AddRange(
                new JobApplication { TenantId = tenantId, JobPostingId = postingId, CandidateId = c1.Id, Status = PipelineStatus.Applied, Source = SourceChannel.Direct },
                new JobApplication { TenantId = tenantId, JobPostingId = postingId, CandidateId = c2.Id, Status = PipelineStatus.Applied, Source = SourceChannel.Facebook },
                new JobApplication { TenantId = tenantId, JobPostingId = postingId, CandidateId = c3.Id, Status = PipelineStatus.Applied, Source = SourceChannel.LinkedIn }
            );
            db.SaveChanges();
        }

        var client = ClientFor(Roles.Admin, tenantId: tenantId);
        var srcRes = await client.GetAsync("/api/analytics/source-of-hire");
        var src = await srcRes.Content.ReadFromJsonAsync<SourceOfHireAnalyticsDto>();

        Assert.NotNull(src);
        double totalPct = src!.Sources.Sum(s => s.Percentage);
        // 33.3 + 33.3 + 33.3 = 99.9 or 100.0 due to rounding
        Assert.InRange(totalPct, 99.5, 100.5);
    }
}
