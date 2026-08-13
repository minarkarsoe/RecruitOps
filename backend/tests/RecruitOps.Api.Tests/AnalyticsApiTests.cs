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

public class AnalyticsApiTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;

    public AnalyticsApiTests(CustomWebAppFactory factory)
    {
        _factory = factory;
        SeedTestData();
    }

    private HttpClient ClientFor(string role, Guid? userId = null)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Tenant", _factory.TenantA.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Roles", role);
        if (userId is not null)
            client.DefaultRequestHeaders.Add("X-Test-UserId", userId.Value.ToString());
        return client;
    }

    private void SeedTestData()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Check if analytics test data is already seeded
        if (db.JobApplications.IgnoreQueryFilters().Any(a => a.CoverNote == "AnalyticsTestApp"))
            return;

        var now = DateTimeOffset.UtcNow;

        // Requisitions
        var salesReq = new Requisition
        {
            TenantId = _factory.TenantA,
            DepartmentId = _factory.SalesDepartmentId,
            RequestedByUserId = _factory.HiringManagerUserId,
            Title = "Sales Analytics Requisition",
            JobDescription = "Approved Sales Req",
            Headcount = 2,
            Status = RequisitionStatus.Approved
        };

        var finReq = new Requisition
        {
            TenantId = _factory.TenantA,
            DepartmentId = _factory.FinanceDepartmentId,
            RequestedByUserId = _factory.FinanceManagerUserId,
            Title = "Finance Analytics Requisition",
            JobDescription = "Approved Finance Req",
            Headcount = 1,
            Status = RequisitionStatus.Approved
        };

        db.Requisitions.AddRange(salesReq, finReq);
        db.SaveChanges();

        // Job Postings
        var salesPosting = new JobPosting
        {
            TenantId = _factory.TenantA,
            DepartmentId = _factory.SalesDepartmentId,
            RequisitionId = salesReq.Id,
            Title = "Sales Manager Vacancy",
            Description = "Posting for Sales",
            Status = JobStatus.Live
        };

        var finPosting = new JobPosting
        {
            TenantId = _factory.TenantA,
            DepartmentId = _factory.FinanceDepartmentId,
            RequisitionId = finReq.Id,
            Title = "Financial Analyst Vacancy",
            Description = "Posting for Finance",
            Status = JobStatus.Live
        };

        db.JobPostings.AddRange(salesPosting, finPosting);
        db.SaveChanges();

        // Candidates
        var cand1 = new Candidate { TenantId = _factory.TenantA, FullName = "Cand One", Email = "c1@test.com", Source = SourceChannel.Direct };
        var cand2 = new Candidate { TenantId = _factory.TenantA, FullName = "Cand Two", Email = "c2@test.com", Source = SourceChannel.Referral };
        var cand3 = new Candidate { TenantId = _factory.TenantA, FullName = "Cand Three", Email = "c3@test.com", Source = SourceChannel.LinkedIn };
        var cand4 = new Candidate { TenantId = _factory.TenantA, FullName = "Cand Four", Email = "c4@test.com", Source = SourceChannel.Facebook };

        db.Candidates.AddRange(cand1, cand2, cand3, cand4);
        db.SaveChanges();

        // Applications
        var app1 = new JobApplication
        {
            TenantId = _factory.TenantA,
            JobPostingId = salesPosting.Id,
            CandidateId = cand1.Id,
            Status = PipelineStatus.Hired,
            Source = SourceChannel.Direct,
            AppliedAt = now.AddDays(-10),
            UpdatedAt = now,
            CoverNote = "AnalyticsTestApp"
        };

        var app2 = new JobApplication
        {
            TenantId = _factory.TenantA,
            JobPostingId = salesPosting.Id,
            CandidateId = cand2.Id,
            Status = PipelineStatus.Interview,
            Source = SourceChannel.Referral,
            AppliedAt = now.AddDays(-5),
            UpdatedAt = now,
            CoverNote = "AnalyticsTestApp"
        };

        var app3 = new JobApplication
        {
            TenantId = _factory.TenantA,
            JobPostingId = finPosting.Id,
            CandidateId = cand3.Id,
            Status = PipelineStatus.Hired,
            Source = SourceChannel.LinkedIn,
            AppliedAt = now.AddDays(-6),
            UpdatedAt = now,
            CoverNote = "AnalyticsTestApp"
        };

        var app4 = new JobApplication
        {
            TenantId = _factory.TenantA,
            JobPostingId = finPosting.Id,
            CandidateId = cand4.Id,
            Status = PipelineStatus.Applied,
            Source = SourceChannel.Facebook,
            AppliedAt = now.AddDays(-1),
            UpdatedAt = now,
            CoverNote = "AnalyticsTestApp"
        };

        db.JobApplications.AddRange(app1, app2, app3, app4);
        db.SaveChanges();

        // Stage History for app1 (Sales - Hired: 10 days)
        var app1Hist1 = new ApplicationStageHistory { TenantId = _factory.TenantA, JobApplicationId = app1.Id, FromStatus = null, ToStatus = PipelineStatus.Applied, ChangedAt = now.AddDays(-10) };
        var app1Hist2 = new ApplicationStageHistory { TenantId = _factory.TenantA, JobApplicationId = app1.Id, FromStatus = PipelineStatus.Applied, ToStatus = PipelineStatus.Screening, ChangedAt = now.AddDays(-8) };
        var app1Hist3 = new ApplicationStageHistory { TenantId = _factory.TenantA, JobApplicationId = app1.Id, FromStatus = PipelineStatus.Screening, ToStatus = PipelineStatus.Shortlisted, ChangedAt = now.AddDays(-6) };
        var app1Hist4 = new ApplicationStageHistory { TenantId = _factory.TenantA, JobApplicationId = app1.Id, FromStatus = PipelineStatus.Shortlisted, ToStatus = PipelineStatus.Interview, ChangedAt = now.AddDays(-4) };
        var app1Hist5 = new ApplicationStageHistory { TenantId = _factory.TenantA, JobApplicationId = app1.Id, FromStatus = PipelineStatus.Interview, ToStatus = PipelineStatus.Offer, ChangedAt = now.AddDays(-2) };
        var app1Hist6 = new ApplicationStageHistory { TenantId = _factory.TenantA, JobApplicationId = app1.Id, FromStatus = PipelineStatus.Offer, ToStatus = PipelineStatus.Hired, ChangedAt = now };

        // Stage History for app2 (Sales - Interview)
        var app2Hist1 = new ApplicationStageHistory { TenantId = _factory.TenantA, JobApplicationId = app2.Id, FromStatus = null, ToStatus = PipelineStatus.Applied, ChangedAt = now.AddDays(-5) };
        var app2Hist2 = new ApplicationStageHistory { TenantId = _factory.TenantA, JobApplicationId = app2.Id, FromStatus = PipelineStatus.Applied, ToStatus = PipelineStatus.Screening, ChangedAt = now.AddDays(-4) };
        var app2Hist3 = new ApplicationStageHistory { TenantId = _factory.TenantA, JobApplicationId = app2.Id, FromStatus = PipelineStatus.Screening, ToStatus = PipelineStatus.Shortlisted, ChangedAt = now.AddDays(-3) };
        var app2Hist4 = new ApplicationStageHistory { TenantId = _factory.TenantA, JobApplicationId = app2.Id, FromStatus = PipelineStatus.Shortlisted, ToStatus = PipelineStatus.Interview, ChangedAt = now.AddDays(-2) };

        // Stage History for app3 (Finance - Hired: 6 days)
        var app3Hist1 = new ApplicationStageHistory { TenantId = _factory.TenantA, JobApplicationId = app3.Id, FromStatus = null, ToStatus = PipelineStatus.Applied, ChangedAt = now.AddDays(-6) };
        var app3Hist2 = new ApplicationStageHistory { TenantId = _factory.TenantA, JobApplicationId = app3.Id, FromStatus = PipelineStatus.Applied, ToStatus = PipelineStatus.Hired, ChangedAt = now };

        // Stage History for app4 (Finance - Applied)
        var app4Hist1 = new ApplicationStageHistory { TenantId = _factory.TenantA, JobApplicationId = app4.Id, FromStatus = null, ToStatus = PipelineStatus.Applied, ChangedAt = now.AddDays(-1) };

        db.ApplicationStageHistories.AddRange(
            app1Hist1, app1Hist2, app1Hist3, app1Hist4, app1Hist5, app1Hist6,
            app2Hist1, app2Hist2, app2Hist3, app2Hist4,
            app3Hist1, app3Hist2,
            app4Hist1
        );
        db.SaveChanges();
    }

    [Fact]
    public async Task Unauthenticated_Analytics_Endpoints_Return_401()
    {
        var client = _factory.CreateClient();

        var resKpi = await client.GetAsync("/api/analytics/kpis");
        Assert.Equal(HttpStatusCode.Unauthorized, resKpi.StatusCode);

        var resTime = await client.GetAsync("/api/analytics/time-to-hire");
        Assert.Equal(HttpStatusCode.Unauthorized, resTime.StatusCode);

        var resConv = await client.GetAsync("/api/analytics/conversion");
        Assert.Equal(HttpStatusCode.Unauthorized, resConv.StatusCode);

        var resSrc = await client.GetAsync("/api/analytics/source-of-hire");
        Assert.Equal(HttpStatusCode.Unauthorized, resSrc.StatusCode);

        var resQuery = await client.PostAsJsonAsync("/api/analytics/reports/query", new ReportQueryRequestDto());
        Assert.Equal(HttpStatusCode.Unauthorized, resQuery.StatusCode);

        var resExport = await client.GetAsync("/api/analytics/reports/export");
        Assert.Equal(HttpStatusCode.Unauthorized, resExport.StatusCode);
    }

    [Fact]
    public async Task Admin_GetKpis_Returns_Aggregated_Metrics()
    {
        var client = ClientFor(Roles.Admin, _factory.AdminUserId);
        var res = await client.GetAsync("/api/analytics/kpis");
        res.EnsureSuccessStatusCode();

        var dto = await res.Content.ReadFromJsonAsync<KpiMetricsDto>();
        Assert.NotNull(dto);
        Assert.True(dto!.ActiveRequisitions >= 2);
        Assert.True(dto.TotalApplications >= 4);
        Assert.True(dto.OverallHireRate > 0);
        Assert.True(dto.AvgTimeToHireDays > 0);
    }

    [Fact]
    public async Task HiringManager_GetKpis_Enforces_Department_Scoping()
    {
        var salesManagerClient = ClientFor(Roles.HiringManager, _factory.HiringManagerUserId);
        var resSales = await salesManagerClient.GetAsync("/api/analytics/kpis");
        resSales.EnsureSuccessStatusCode();

        var salesDto = await resSales.Content.ReadFromJsonAsync<KpiMetricsDto>();
        Assert.NotNull(salesDto);
        Assert.Equal(1, salesDto!.ActiveRequisitions);
        Assert.Equal(2, salesDto.TotalApplications);
        Assert.Equal(50.0, salesDto.OverallHireRate); // 1 hired out of 2 = 50%
        Assert.Equal(10.0, salesDto.AvgTimeToHireDays); // 10 days for app1

        var finManagerClient = ClientFor(Roles.HiringManager, _factory.FinanceManagerUserId);
        var resFin = await finManagerClient.GetAsync("/api/analytics/kpis");
        resFin.EnsureSuccessStatusCode();

        var finDto = await resFin.Content.ReadFromJsonAsync<KpiMetricsDto>();
        Assert.NotNull(finDto);
        Assert.Equal(1, finDto!.ActiveRequisitions);
        Assert.Equal(2, finDto.TotalApplications);
        Assert.Equal(50.0, finDto.OverallHireRate); // 1 hired out of 2 = 50%
        Assert.Equal(6.0, finDto.AvgTimeToHireDays); // 6 days for app3
    }

    [Fact]
    public async Task GetTimeToHire_Returns_Stage_Department_And_Posting_Breakdown()
    {
        var client = ClientFor(Roles.Admin, _factory.AdminUserId);
        var res = await client.GetAsync("/api/analytics/time-to-hire");
        res.EnsureSuccessStatusCode();

        var dto = await res.Content.ReadFromJsonAsync<TimeToHireAnalyticsDto>();
        Assert.NotNull(dto);
        Assert.NotEmpty(dto!.StageDurations);
        Assert.NotEmpty(dto.DepartmentBreakdown);
        Assert.NotEmpty(dto.PostingBreakdown);

        var salesDept = dto.DepartmentBreakdown.FirstOrDefault(d => d.DepartmentId == _factory.SalesDepartmentId);
        Assert.NotNull(salesDept);
        Assert.Equal(1, salesDept!.HiredCount);
        Assert.Equal(10.0, salesDept.AvgDays);

        var finDept = dto.DepartmentBreakdown.FirstOrDefault(d => d.DepartmentId == _factory.FinanceDepartmentId);
        Assert.NotNull(finDept);
        Assert.Equal(1, finDept!.HiredCount);
        Assert.Equal(6.0, finDept.AvgDays);
    }

    [Fact]
    public async Task GetConversionFunnel_Calculates_Counts_And_Dropoff()
    {
        var client = ClientFor(Roles.Admin, _factory.AdminUserId);
        var res = await client.GetAsync("/api/analytics/conversion");
        res.EnsureSuccessStatusCode();

        var dto = await res.Content.ReadFromJsonAsync<ConversionFunnelAnalyticsDto>();
        Assert.NotNull(dto);
        Assert.Equal(7, dto!.Funnel.Count);

        var appliedStage = dto.Funnel.First(s => s.Stage == "Applied");
        Assert.True(appliedStage.Count >= 4);

        var hiredStage = dto.Funnel.First(s => s.Stage == "Hired");
        Assert.Equal(2, hiredStage.Count);
    }

    [Fact]
    public async Task GetSourceOfHire_Calculates_Channel_Distribution()
    {
        var client = ClientFor(Roles.Admin, _factory.AdminUserId);
        var res = await client.GetAsync("/api/analytics/source-of-hire");
        res.EnsureSuccessStatusCode();

        var dto = await res.Content.ReadFromJsonAsync<SourceOfHireAnalyticsDto>();
        Assert.NotNull(dto);
        Assert.NotEmpty(dto!.Sources);

        var direct = dto.Sources.FirstOrDefault(s => s.Source == "Direct");
        Assert.NotNull(direct);
        Assert.True(direct!.Count >= 1);

        var referral = dto.Sources.FirstOrDefault(s => s.Source == "Referral");
        Assert.NotNull(referral);
        Assert.True(referral!.Count >= 1);
    }

    [Fact]
    public async Task HiringManager_With_No_Assigned_Departments_Returns_Zero_Metrics()
    {
        var unassignedUserId = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Users.Add(new User
            {
                Id = unassignedUserId,
                TenantId = _factory.TenantA,
                Email = "unassigned.hm@alpha.test",
                DisplayName = "Unassigned Manager",
                Role = UserRole.HiringManager,
                IsActive = true,
                PasswordHash = "not-used"
            });
            db.SaveChanges();
        }

        var client = ClientFor(Roles.HiringManager, unassignedUserId);
        var res = await client.GetAsync("/api/analytics/kpis");
        res.EnsureSuccessStatusCode();

        var dto = await res.Content.ReadFromJsonAsync<KpiMetricsDto>();
        Assert.NotNull(dto);
        Assert.Equal(0, dto!.ActiveRequisitions);
        Assert.Equal(0, dto.TotalApplications);
        Assert.Equal(0.0, dto.OverallHireRate);
        Assert.Equal(0.0, dto.AvgTimeToHireDays);
    }

    [Fact]
    public async Task Approver_Role_Is_Excluded_From_Candidate_Analytics()
    {
        var client = ClientFor(Roles.Approver, _factory.FinanceApproverUserId);
        var res = await client.GetAsync("/api/analytics/kpis");
        res.EnsureSuccessStatusCode();

        var dto = await res.Content.ReadFromJsonAsync<KpiMetricsDto>();
        Assert.NotNull(dto);
        Assert.Equal(0, dto!.TotalApplications);
        Assert.Equal(0.0, dto.OverallHireRate);
        Assert.Equal(0.0, dto.AvgTimeToHireDays);
    }

    [Fact]
    public async Task Zero_Data_Edge_Case_Returns_Zero_Metrics()
    {
        var emptyTenantId = Guid.NewGuid();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Tenant", emptyTenantId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Roles", Roles.Admin);

        var res = await client.GetAsync("/api/analytics/kpis");
        res.EnsureSuccessStatusCode();

        var dto = await res.Content.ReadFromJsonAsync<KpiMetricsDto>();
        Assert.NotNull(dto);
        Assert.Equal(0, dto!.ActiveRequisitions);
        Assert.Equal(0, dto.TotalApplications);
        Assert.Equal(0.0, dto.OverallHireRate);
        Assert.Equal(0.0, dto.AvgTimeToHireDays);
    }

    [Fact]
    public async Task Admin_QueryReport_With_Filtering_And_Custom_Columns()
    {
        var client = ClientFor(Roles.Admin, _factory.AdminUserId);
        var request = new ReportQueryRequestDto(
            DepartmentId: _factory.SalesDepartmentId,
            Stages: new List<PipelineStatus> { PipelineStatus.Hired },
            Columns: new List<string> { "jobTitle", "candidateName", "stage" }
        );

        var response = await client.PostAsJsonAsync("/api/analytics/reports/query", request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ReportQueryResultDto>();
        Assert.NotNull(result);
        Assert.Equal(3, result!.Headers.Count);
        Assert.Equal(new[] { "Job Title", "Candidate Name", "Stage" }, result.Headers);

        Assert.Single(result.Rows);
        var row = result.Rows[0];
        Assert.Equal("Sales Manager Vacancy", row["jobTitle"]?.ToString());
        Assert.Equal("Cand One", row["candidateName"]?.ToString());
        Assert.Equal("Hired", row["stage"]?.ToString());
    }

    [Fact]
    public async Task Admin_ExportReportCsv_Returns_Csv_File_With_Headers_And_Utf8_Bom()
    {
        var client = ClientFor(Roles.Admin, _factory.AdminUserId);
        var response = await client.GetAsync($"/api/analytics/reports/export?departmentId={_factory.SalesDepartmentId}");
        response.EnsureSuccessStatusCode();

        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        Assert.NotNull(response.Content.Headers.ContentDisposition);
        Assert.Equal("report.csv", response.Content.Headers.ContentDisposition?.FileName);

        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length >= 3);
        // Verify UTF-8 BOM: 0xEF, 0xBB, 0xBF
        Assert.Equal(0xEF, bytes[0]);
        Assert.Equal(0xBB, bytes[1]);
        Assert.Equal(0xBF, bytes[2]);

        var csvText = System.Text.Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        Assert.Contains("Candidate Name,Candidate Email,Job Title,Department,Stage,Source,Applied Date", csvText);
        Assert.Contains("Cand One", csvText);
        Assert.Contains("Sales Manager Vacancy", csvText);
    }

    [Fact]
    public async Task HiringManager_QueryReport_Enforces_Department_Scoping()
    {
        // Sales manager can query Sales department
        var salesClient = ClientFor(Roles.HiringManager, _factory.HiringManagerUserId);
        var salesReq = new ReportQueryRequestDto(DepartmentId: _factory.SalesDepartmentId);
        var salesRes = await salesClient.PostAsJsonAsync("/api/analytics/reports/query", salesReq);
        salesRes.EnsureSuccessStatusCode();

        var salesResult = await salesRes.Content.ReadFromJsonAsync<ReportQueryResultDto>();
        Assert.NotNull(salesResult);
        Assert.Equal(2, salesResult!.Rows.Count);

        // Sales manager trying to query Finance department gets empty result (ADR-0003 scoping)
        var finReq = new ReportQueryRequestDto(DepartmentId: _factory.FinanceDepartmentId);
        var finRes = await salesClient.PostAsJsonAsync("/api/analytics/reports/query", finReq);
        finRes.EnsureSuccessStatusCode();

        var finResult = await finRes.Content.ReadFromJsonAsync<ReportQueryResultDto>();
        Assert.NotNull(finResult);
        Assert.Empty(finResult!.Rows);
    }

    [Fact]
    public async Task Approver_Role_QueryReport_And_Export_Returns_Empty_Report()
    {
        var client = ClientFor(Roles.Approver, _factory.FinanceApproverUserId);

        var queryRes = await client.PostAsJsonAsync("/api/analytics/reports/query", new ReportQueryRequestDto());
        queryRes.EnsureSuccessStatusCode();
        var queryDto = await queryRes.Content.ReadFromJsonAsync<ReportQueryResultDto>();
        Assert.NotNull(queryDto);
        Assert.Empty(queryDto!.Rows);

        var exportRes = await client.GetAsync("/api/analytics/reports/export");
        exportRes.EnsureSuccessStatusCode();
        var bytes = await exportRes.Content.ReadAsByteArrayAsync();
        var csvText = System.Text.Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        // Header line present, but no data rows
        var lines = csvText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        Assert.Single(lines);
    }

    [Fact]
    public async Task ExportReportCsv_Escapes_Special_Characters_Per_Rfc4180()
    {
        Guid specialPostingId;
        var specialTenantId = Guid.NewGuid();
        var specialDeptId = Guid.NewGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var dept = new Department
            {
                Id = specialDeptId,
                TenantId = specialTenantId,
                Name = "Special Dept, \"Escaped\""
            };
            db.Departments.Add(dept);

            var cand = new Candidate
            {
                TenantId = specialTenantId,
                FullName = "Doe, Jane \"Special\"",
                Email = "jane.special@test.com",
                Source = SourceChannel.Direct
            };
            db.Candidates.Add(cand);

            var posting = new JobPosting
            {
                TenantId = specialTenantId,
                DepartmentId = specialDeptId,
                Title = "Special, Job \"Title\"",
                Description = "Test Posting",
                Status = JobStatus.Live
            };
            db.JobPostings.Add(posting);

            var app = new JobApplication
            {
                TenantId = specialTenantId,
                JobPostingId = posting.Id,
                CandidateId = cand.Id,
                Status = PipelineStatus.Applied,
                Source = SourceChannel.Direct,
                AppliedAt = DateTimeOffset.UtcNow
            };
            db.JobApplications.Add(app);
            db.SaveChanges();
            specialPostingId = posting.Id;
        }

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Tenant", specialTenantId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Roles", Roles.Admin);

        var res = await client.GetAsync($"/api/analytics/reports/export?jobPostingId={specialPostingId}");
        res.EnsureSuccessStatusCode();

        var bytes = await res.Content.ReadAsByteArrayAsync();
        var csvText = System.Text.Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);

        // Verify RFC 4180 escaped fields
        Assert.Contains("\"Doe, Jane \"\"Special\"\"\"", csvText);
        Assert.Contains("\"Special, Job \"\"Title\"\"\"", csvText);
        Assert.Contains("\"Special Dept, \"\"Escaped\"\"\"", csvText);
    }
}

