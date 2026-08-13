using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RecruitOps.Api.Auth;
using RecruitOps.Application.DTOs.Search;
using RecruitOps.Domain.Entities;
using RecruitOps.Domain.Enums;
using RecruitOps.Infrastructure.Persistence;
using Xunit;

namespace RecruitOps.Api.Tests;

public class Milestone1EmpiricalAccessControlAndBoundaryTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;

    public Milestone1EmpiricalAccessControlAndBoundaryTests(CustomWebAppFactory factory)
    {
        _factory = factory;
        SeedTestData();
    }

    private HttpClient ClientFor(string role, Guid tenantId, Guid? userId = null)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Tenant", tenantId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Roles", role);
        if (userId is not null)
            client.DefaultRequestHeaders.Add("X-Test-UserId", userId.Value.ToString());
        return client;
    }

    private void SeedTestData()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (db.Candidates.IgnoreQueryFilters().Any(c => c.Email == "empirical.m1.test1@alpha.test"))
            return;

        var now = DateTimeOffset.UtcNow;

        // Tenant A Data
        var salesReq = new Requisition
        {
            TenantId = _factory.TenantA,
            DepartmentId = _factory.SalesDepartmentId,
            RequestedByUserId = _factory.HiringManagerUserId,
            Title = "Alpha Sales Spec Lead",
            JobDescription = "Managing sales pipelines in Alpha.",
            Headcount = 3,
            Status = RequisitionStatus.Approved
        };

        var finReq = new Requisition
        {
            TenantId = _factory.TenantA,
            DepartmentId = _factory.FinanceDepartmentId,
            RequestedByUserId = _factory.FinanceManagerUserId,
            Title = "Alpha Finance Controller",
            JobDescription = "Auditing finance ledgers in Alpha.",
            Headcount = 1,
            Status = RequisitionStatus.Approved
        };

        db.Requisitions.AddRange(salesReq, finReq);
        db.SaveChanges();

        var salesPosting = new JobPosting
        {
            TenantId = _factory.TenantA,
            DepartmentId = _factory.SalesDepartmentId,
            RequisitionId = salesReq.Id,
            Title = "Alpha Sales Specialist Posting",
            Description = "Join our high velocity sales team.",
            Status = JobStatus.Live
        };

        var finPosting = new JobPosting
        {
            TenantId = _factory.TenantA,
            DepartmentId = _factory.FinanceDepartmentId,
            RequisitionId = finReq.Id,
            Title = "Alpha Finance Auditor Posting",
            Description = "Join our corporate finance team.",
            Status = JobStatus.Live
        };

        db.JobPostings.AddRange(salesPosting, finPosting);
        db.SaveChanges();

        var candSales = new Candidate
        {
            TenantId = _factory.TenantA,
            FullName = "Alpha Candidate Sales",
            Email = "empirical.m1.test1@alpha.test",
            Phone = "0911111111",
            Source = SourceChannel.Direct
        };

        var candFin = new Candidate
        {
            TenantId = _factory.TenantA,
            FullName = "Alpha Candidate Finance",
            Email = "empirical.m1.test2@alpha.test",
            Phone = "0922222222",
            Source = SourceChannel.Referral
        };

        db.Candidates.AddRange(candSales, candFin);
        db.SaveChanges();

        var appSales = new JobApplication
        {
            TenantId = _factory.TenantA,
            JobPostingId = salesPosting.Id,
            CandidateId = candSales.Id,
            Status = PipelineStatus.Applied,
            Source = SourceChannel.Direct,
            AppliedAt = now.AddDays(-3),
            ResumeExtractedText = "Sales executive with 5 years experience in SaaS."
        };

        var appFin = new JobApplication
        {
            TenantId = _factory.TenantA,
            JobPostingId = finPosting.Id,
            CandidateId = candFin.Id,
            Status = PipelineStatus.Interview,
            Source = SourceChannel.Referral,
            AppliedAt = now.AddDays(-1),
            ResumeExtractedText = "Senior financial auditor specializing in IFRS compliance."
        };

        db.JobApplications.AddRange(appSales, appFin);
        db.SaveChanges();

        // Tenant B Data (Isolation check)
        var bravoDept = db.Departments.IgnoreQueryFilters().First(d => d.TenantId == _factory.TenantB);

        var bravoReq = new Requisition
        {
            TenantId = _factory.TenantB,
            DepartmentId = bravoDept.Id,
            RequestedByUserId = Guid.NewGuid(),
            Title = "Bravo Confidential Strategy Lead",
            JobDescription = "Top secret strategy in Bravo Corp.",
            Headcount = 1,
            Status = RequisitionStatus.Approved
        };
        db.Requisitions.Add(bravoReq);
        db.SaveChanges();

        var bravoPosting = new JobPosting
        {
            TenantId = _factory.TenantB,
            DepartmentId = bravoDept.Id,
            RequisitionId = bravoReq.Id,
            Title = "Bravo Confidential Posting",
            Description = "Secret mission.",
            Status = JobStatus.Live
        };
        db.JobPostings.Add(bravoPosting);
        db.SaveChanges();

        var bravoCand = new Candidate
        {
            TenantId = _factory.TenantB,
            FullName = "Bravo Secret Candidate",
            Email = "secret@bravo.test",
            Phone = "0999999999",
            Source = SourceChannel.Direct
        };
        db.Candidates.Add(bravoCand);
        db.SaveChanges();

        var bravoApp = new JobApplication
        {
            TenantId = _factory.TenantB,
            JobPostingId = bravoPosting.Id,
            CandidateId = bravoCand.Id,
            Status = PipelineStatus.Applied,
            Source = SourceChannel.Direct,
            AppliedAt = now,
            ResumeExtractedText = "Top secret espionage and strategy."
        };
        db.JobApplications.Add(bravoApp);
        db.SaveChanges();
    }

    #region 1. Department Reach Scoping Tests (ADR-0003 & ADR-0018)

    [Fact]
    public async Task ADR0003_HiringManager_Reaches_Only_Own_Department_Data()
    {
        // Sales Hiring Manager (_factory.HiringManagerUserId)
        var client = ClientFor(Roles.HiringManager, _factory.TenantA, _factory.HiringManagerUserId);

        var res = await client.GetAsync("/api/search?q=Alpha");
        res.EnsureSuccessStatusCode();

        var dto = await res.Content.ReadFromJsonAsync<SearchResponseDto>();
        Assert.NotNull(dto);

        // All items returned must belong to Sales department (or have null department for Sales candidate)
        foreach (var item in dto!.Items)
        {
            if (item.DepartmentId != null)
            {
                Assert.Equal(_factory.SalesDepartmentId, item.DepartmentId);
                Assert.NotEqual(_factory.FinanceDepartmentId, item.DepartmentId);
            }
        }

        // Must NOT find Finance candidates or postings
        Assert.DoesNotContain(dto.Items, i => i.Title.Contains("Finance"));
    }

    [Fact]
    public async Task ADR0003_UnscopedRoles_Admin_HrDirector_Recruiter_Reach_All_Departments()
    {
        var rolesToTest = new[] { Roles.Admin, Roles.HrDirector, Roles.Recruiter };

        foreach (var role in rolesToTest)
        {
            var client = ClientFor(role, _factory.TenantA, _factory.AdminUserId);
            var res = await client.GetAsync("/api/search?q=Alpha");
            res.EnsureSuccessStatusCode();

            var dto = await res.Content.ReadFromJsonAsync<SearchResponseDto>();
            Assert.NotNull(dto);
            Assert.Contains(dto!.Items, i => i.DepartmentId == _factory.SalesDepartmentId);
            Assert.Contains(dto.Items, i => i.DepartmentId == _factory.FinanceDepartmentId);
        }
    }

    [Fact]
    public async Task ADR0018_Approver_Role_Reaches_Requisitions_Across_Departments_But_Excluded_From_Candidates()
    {
        var client = ClientFor(Roles.Approver, _factory.TenantA, _factory.FinanceApproverUserId);

        // Approver searching for "Alpha"
        var res = await client.GetAsync("/api/search?q=Alpha");
        res.EnsureSuccessStatusCode();

        var dto = await res.Content.ReadFromJsonAsync<SearchResponseDto>();
        Assert.NotNull(dto);

        // 1. Approver MUST see requisitions from both departments
        Assert.Contains(dto!.Items, i => i.Category == "Requisitions" && i.DepartmentId == _factory.SalesDepartmentId);
        Assert.Contains(dto.Items, i => i.Category == "Requisitions" && i.DepartmentId == _factory.FinanceDepartmentId);

        // 2. Approver MUST NOT see any Candidates (ADR-0018 Exclusion)
        Assert.DoesNotContain(dto.Items, i => i.Category == "Candidates");
    }

    [Fact]
    public async Task ADR0018_Approver_Reaches_Candidate_Only_When_On_Interview_Panel()
    {
        Guid panelApproverId = Guid.NewGuid();
        Guid panelCandId = Guid.NewGuid();

        // Setup: Create panel approver and add to an interview panel for a dedicated candidate application
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var approverUser = new User
            {
                Id = panelApproverId,
                TenantId = _factory.TenantA,
                Email = "panel.approver@alpha.test",
                DisplayName = "Panel Approver",
                Role = UserRole.Approver,
                IsActive = true,
                PasswordHash = "not-used"
            };
            db.Users.Add(approverUser);

            var panelCand = new Candidate
            {
                Id = panelCandId,
                TenantId = _factory.TenantA,
                FullName = "Alpha Panel Candidate",
                Email = "panel.candidate@alpha.test",
                Phone = "0988888888",
                Source = SourceChannel.Direct
            };
            db.Candidates.Add(panelCand);

            var posting = db.JobPostings.IgnoreQueryFilters().First(p => p.DepartmentId == _factory.FinanceDepartmentId);

            var panelApp = new JobApplication
            {
                Id = Guid.NewGuid(),
                TenantId = _factory.TenantA,
                JobPostingId = posting.Id,
                CandidateId = panelCandId,
                Status = PipelineStatus.Interview,
                Source = SourceChannel.Direct,
                AppliedAt = DateTimeOffset.UtcNow
            };
            db.JobApplications.Add(panelApp);

            var interview = new Interview
            {
                Id = Guid.NewGuid(),
                TenantId = _factory.TenantA,
                JobApplicationId = panelApp.Id,
                Round = 1,
                Mode = InterviewMode.Video,
                Status = InterviewStatus.Scheduled,
                ScheduledStart = DateTimeOffset.UtcNow.AddDays(1),
                DurationMinutes = 60
            };
            db.Interviews.Add(interview);

            db.InterviewParticipants.Add(new InterviewParticipant
            {
                Id = Guid.NewGuid(),
                TenantId = _factory.TenantA,
                InterviewId = interview.Id,
                UserId = panelApproverId
            });

            db.SaveChanges();
        }

        // Test: Approver searching for "Panel"
        var client = ClientFor(Roles.Approver, _factory.TenantA, panelApproverId);
        var res = await client.GetAsync("/api/search?q=Panel");
        res.EnsureSuccessStatusCode();

        var dto = await res.Content.ReadFromJsonAsync<SearchResponseDto>();
        Assert.NotNull(dto);

        // Now panel approver SHOULD see Candidate Panel because of panel participation grant (ADR-0017 §4)
        Assert.Contains(dto!.Items, i => i.Category == "Candidates" && i.Title == "Alpha Panel Candidate");

        // BUT should NOT see Candidate Sales (not on panel for Sales)
        Assert.DoesNotContain(dto.Items, i => i.Category == "Candidates" && i.Title == "Alpha Candidate Sales");
    }

    #endregion

    #region 2. Tenant Isolation Tests

    [Fact]
    public async Task TenantIsolation_TenantA_User_Cannot_Access_TenantB_Data_Via_Search()
    {
        var client = ClientFor(Roles.Admin, _factory.TenantA, _factory.AdminUserId);

        var res = await client.GetAsync("/api/search?q=Bravo");
        res.EnsureSuccessStatusCode();

        var dto = await res.Content.ReadFromJsonAsync<SearchResponseDto>();
        Assert.NotNull(dto);
        Assert.Equal(0, dto!.TotalMatches);
        Assert.Empty(dto.Items);
    }

    [Fact]
    public async Task TenantIsolation_TenantB_User_Cannot_Access_TenantA_Data_Via_Search()
    {
        var client = ClientFor(Roles.Admin, _factory.TenantB, Guid.NewGuid());

        var res = await client.GetAsync("/api/search?q=Alpha");
        res.EnsureSuccessStatusCode();

        var dto = await res.Content.ReadFromJsonAsync<SearchResponseDto>();
        Assert.NotNull(dto);
        Assert.Equal(0, dto!.TotalMatches);
        Assert.Empty(dto.Items);
    }

    #endregion

    #region 3. Boundary Case & Input Resilience Tests

    [Fact]
    public async Task Boundary_Empty_And_Whitespace_Queries_Return_400BadRequest()
    {
        var client = ClientFor(Roles.Admin, _factory.TenantA, _factory.AdminUserId);

        var resEmpty = await client.GetAsync("/api/search?q=");
        Assert.Equal(HttpStatusCode.BadRequest, resEmpty.StatusCode);

        var resSpaces = await client.GetAsync("/api/search?q=%20%20%20");
        Assert.Equal(HttpStatusCode.BadRequest, resSpaces.StatusCode);

        var resTab = await client.GetAsync("/api/search?q=%09%0A");
        Assert.Equal(HttpStatusCode.BadRequest, resTab.StatusCode);
    }

    [Fact]
    public async Task Boundary_SqlInjection_And_Xss_Payloads_Degrade_Safely_Without_500_Error()
    {
        var client = ClientFor(Roles.Admin, _factory.TenantA, _factory.AdminUserId);

        string[] payloads = new[]
        {
            "' OR '1'='1",
            "'; DROP TABLE Candidates; --",
            "<script>alert('xss')</script>",
            "SELECT * FROM Users WHERE 1=1",
            "%\n%\r%\t",
            "'; EXEC sp_executesql N'SELECT 1'; --"
        };

        foreach (var payload in payloads)
        {
            var res = await client.GetAsync($"/api/search?q={Uri.EscapeDataString(payload)}");
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);

            var dto = await res.Content.ReadFromJsonAsync<SearchResponseDto>();
            Assert.NotNull(dto);
            // Snippets must encode HTML tags if any matched
            foreach (var item in dto!.Items)
            {
                if (item.DescriptionSnippet != null)
                {
                    Assert.DoesNotContain("<script>", item.DescriptionSnippet, StringComparison.OrdinalIgnoreCase);
                }
            }
        }
    }

    [Fact]
    public async Task Boundary_Pagination_Validation_Clamps_Or_Rejects_Invalid_Parameters()
    {
        var client = ClientFor(Roles.Admin, _factory.TenantA, _factory.AdminUserId);

        // Page < 1
        var resPage0 = await client.GetAsync("/api/search?q=Alpha&page=0");
        Assert.Equal(HttpStatusCode.BadRequest, resPage0.StatusCode);

        var resPageNeg = await client.GetAsync("/api/search?q=Alpha&page=-5");
        Assert.Equal(HttpStatusCode.BadRequest, resPageNeg.StatusCode);

        // PageSize < 1 or > 100
        var resSize0 = await client.GetAsync("/api/search?q=Alpha&pageSize=0");
        Assert.Equal(HttpStatusCode.BadRequest, resSize0.StatusCode);

        var resSize101 = await client.GetAsync("/api/search?q=Alpha&pageSize=101");
        Assert.Equal(HttpStatusCode.BadRequest, resSize101.StatusCode);

        // Valid max pageSize 100
        var resSize100 = await client.GetAsync("/api/search?q=Alpha&pageSize=100");
        Assert.Equal(HttpStatusCode.OK, resSize100.StatusCode);
    }

    #endregion
}
