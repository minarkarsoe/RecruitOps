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

namespace RecruitOps.Api.Tests.Search;

public class SearchApiTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;

    public SearchApiTests(CustomWebAppFactory factory)
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

        // Idempotency check for search test data
        if (db.Candidates.IgnoreQueryFilters().Any(c => c.Email == "search.test1@alpha.test"))
            return;

        var now = DateTimeOffset.UtcNow;

        // 1. Requisitions
        var salesReq = new Requisition
        {
            TenantId = _factory.TenantA,
            DepartmentId = _factory.SalesDepartmentId,
            RequestedByUserId = _factory.HiringManagerUserId,
            Title = "Senior Lead Software Architect",
            JobDescription = "Designing microservices and distributed systems.",
            Headcount = 2,
            Status = RequisitionStatus.Approved
        };

        var finReq = new Requisition
        {
            TenantId = _factory.TenantA,
            DepartmentId = _factory.FinanceDepartmentId,
            RequestedByUserId = _factory.FinanceManagerUserId,
            Title = "Senior Financial Accountant",
            JobDescription = "Auditing financial statements and treasury balances.",
            Headcount = 1,
            Status = RequisitionStatus.Approved
        };

        db.Requisitions.AddRange(salesReq, finReq);
        db.SaveChanges();

        // 2. Job Postings
        var salesPosting = new JobPosting
        {
            TenantId = _factory.TenantA,
            DepartmentId = _factory.SalesDepartmentId,
            RequisitionId = salesReq.Id,
            Title = "Software Architect Vacancy",
            Description = "Looking for a seasoned C# .NET and React engineer.",
            Status = JobStatus.Live
        };

        var finPosting = new JobPosting
        {
            TenantId = _factory.TenantA,
            DepartmentId = _factory.FinanceDepartmentId,
            RequisitionId = finReq.Id,
            Title = "Financial Accountant Vacancy",
            Description = "Full-time position for financial controller.",
            Status = JobStatus.Live
        };

        db.JobPostings.AddRange(salesPosting, finPosting);
        db.SaveChanges();

        // 3. Candidates (English & Unicode Burmese)
        var cand1 = new Candidate
        {
            TenantId = _factory.TenantA,
            FullName = "Aung San Suu Kyi",
            Email = "search.test1@alpha.test",
            Phone = "0912345678",
            Source = SourceChannel.Direct
        };

        var cand2 = new Candidate
        {
            TenantId = _factory.TenantA,
            FullName = "အောင်အောင်", // Unicode Burmese "Aung Aung"
            Email = "search.test2@alpha.test",
            Phone = "0987654321",
            Source = SourceChannel.Referral
        };

        db.Candidates.AddRange(cand1, cand2);
        db.SaveChanges();

        // 4. Job Applications with CV extracted text
        var app1 = new JobApplication
        {
            TenantId = _factory.TenantA,
            JobPostingId = salesPosting.Id,
            CandidateId = cand1.Id,
            Status = PipelineStatus.Interview,
            Source = SourceChannel.Direct,
            AppliedAt = now.AddDays(-5),
            ResumeFileName = "AungSan_CV.pdf",
            ResumeExtractedText = "Proficient in Docker Kubernetes Microservices Cloud Architecture and .NET Core.",
            IsZawgyiNormalized = false
        };

        var app2 = new JobApplication
        {
            TenantId = _factory.TenantA,
            JobPostingId = finPosting.Id,
            CandidateId = cand2.Id,
            Status = PipelineStatus.Applied,
            Source = SourceChannel.Referral,
            AppliedAt = now.AddDays(-2),
            ResumeFileName = "Burmese_CV.pdf",
            ResumeExtractedText = "အဆင့်မြင့် စာရင်းကိုင် ဝန်ထမ်း အတွေ့အကြုံ ၅ နှစ် ရှိသည်။", // Unicode Burmese CV text
            IsZawgyiNormalized = true
        };

        db.JobApplications.AddRange(app1, app2);
        db.SaveChanges();

        // 5. Link targets. A candidate who applied to Sales first and Finance later, so "the most
        //    recent application" and "the most recent one a Sales manager can reach" differ.
        var dual = new Candidate
        {
            TenantId = _factory.TenantA,
            FullName = "Thura Dual Applicant",
            Email = "search.dual@alpha.test",
            Source = SourceChannel.Direct
        };
        // Reached only through an interview panel — by the Sales manager and the Finance approver.
        var panelOnly = new Candidate
        {
            TenantId = _factory.TenantA,
            FullName = "Nandar Panel Only",
            Email = "search.panel@alpha.test",
            Source = SourceChannel.Direct
        };
        db.Candidates.AddRange(dual, panelOnly);
        db.SaveChanges();

        var dualSales = new JobApplication
        {
            TenantId = _factory.TenantA, JobPostingId = salesPosting.Id, CandidateId = dual.Id,
            Status = PipelineStatus.Applied, Source = SourceChannel.Direct,
            AppliedAt = now.AddDays(-10), ResumeExtractedText = "Linktarget dual applicant CV."
        };
        var dualFinance = new JobApplication
        {
            TenantId = _factory.TenantA, JobPostingId = finPosting.Id, CandidateId = dual.Id,
            Status = PipelineStatus.Applied, Source = SourceChannel.Direct,
            AppliedAt = now.AddDays(-1), ResumeExtractedText = "Linktarget dual applicant CV."
        };
        var panelApp = new JobApplication
        {
            TenantId = _factory.TenantA, JobPostingId = finPosting.Id, CandidateId = panelOnly.Id,
            Status = PipelineStatus.Interview, Source = SourceChannel.Direct,
            AppliedAt = now.AddDays(-3), ResumeExtractedText = "Panelreach candidate CV."
        };
        db.JobApplications.AddRange(dualSales, dualFinance, panelApp);
        db.SaveChanges();

        var olderRound = new Interview
        {
            TenantId = _factory.TenantA, JobApplicationId = panelApp.Id, Round = 1,
            ScheduledStart = now.AddDays(-2), Status = InterviewStatus.Completed
        };
        var laterRound = new Interview
        {
            TenantId = _factory.TenantA, JobApplicationId = panelApp.Id, Round = 2,
            ScheduledStart = now.AddDays(2)
        };
        db.Interviews.AddRange(olderRound, laterRound);
        db.SaveChanges();

        foreach (var round in new[] { olderRound, laterRound })
        {
            db.InterviewParticipants.AddRange(
                new InterviewParticipant { TenantId = _factory.TenantA, InterviewId = round.Id, UserId = _factory.HiringManagerUserId },
                new InterviewParticipant { TenantId = _factory.TenantA, InterviewId = round.Id, UserId = _factory.FinanceApproverUserId });
        }
        db.SaveChanges();
    }

    private async Task<SearchResultItemDto> OnlyCandidateAsync(HttpClient client, string q)
    {
        var res = await client.GetAsync($"/api/search?q={q}&category=Candidates");
        res.EnsureSuccessStatusCode();
        var dto = await res.Content.ReadFromJsonAsync<SearchResponseDto>();
        return Assert.Single(dto!.Items);
    }

    private (JobApplication Sales, JobApplication Finance, Interview LaterRound) LinkTargets()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var dualId = db.Candidates.IgnoreQueryFilters().Single(c => c.Email == "search.dual@alpha.test").Id;
        var panelId = db.Candidates.IgnoreQueryFilters().Single(c => c.Email == "search.panel@alpha.test").Id;
        var salesPostingId = db.JobPostings.IgnoreQueryFilters().Single(p => p.Title == "Software Architect Vacancy").Id;
        var dualApps = db.JobApplications.IgnoreQueryFilters().Where(a => a.CandidateId == dualId).ToList();
        var panelAppId = db.JobApplications.IgnoreQueryFilters().Single(a => a.CandidateId == panelId).Id;
        var laterRound = db.Interviews.IgnoreQueryFilters().Single(i => i.JobApplicationId == panelAppId && i.Round == 2);
        return (
            dualApps.Single(a => a.JobPostingId == salesPostingId),
            dualApps.Single(a => a.JobPostingId != salesPostingId),
            laterRound);
    }

    // ── Where a result goes when it is clicked ────────────────────────────────────────────
    //
    // `TargetUrl` is a route in the internal SPA, and until 2026-09-17 two of the three kinds
    // pointed at routes that do not exist: `/jobs/{id}` (the SPA serves `/jobpostings/:id`) and
    // `/candidates/{id}` (there is no candidate page at all). Both fell through the SPA's `*`
    // catch-all to /requisitions, silently. The palette's own test fed it `/jobpostings/...`,
    // which is what the SPA wanted rather than what this service sent.
    //
    // These strings are asserted EXACTLY, and `App.routes.test.tsx` asserts the same shapes
    // resolve to a real page. Change one side, change the other.

    [Fact]
    public async Task Posting_And_Requisition_Results_Link_To_Routes_The_Spa_Serves()
    {
        var client = ClientFor(Roles.Admin, _factory.AdminUserId);
        var res = await client.GetAsync("/api/search?q=Architect");
        var dto = await res.Content.ReadFromJsonAsync<SearchResponseDto>();

        var posting = Assert.Single(dto!.Items, i => i.Category == "Postings");
        Assert.Equal($"/jobpostings/{posting.Id}", posting.TargetUrl);

        var requisition = Assert.Single(dto.Items, i => i.Category == "Requisitions");
        Assert.Equal($"/requisitions/{requisition.Id}", requisition.TargetUrl);
    }

    [Fact]
    public async Task A_Candidate_Result_Opens_Their_Most_Recent_Application_On_Its_Board()
    {
        var (_, finance, _) = LinkTargets();
        var item = await OnlyCandidateAsync(ClientFor(Roles.Recruiter, _factory.RecruiterUserId), "Linktarget");

        Assert.Equal($"/jobpostings/{finance.JobPostingId}?application={finance.Id}", item.TargetUrl);
    }

    [Fact]
    public async Task A_Scoped_Managers_Candidate_Link_Is_An_Application_In_Their_Own_Department()
    {
        // The Finance application is newer, and a Sales manager cannot open that board. Linking
        // to it would be a dead end at best, and a Finance posting id handed to Sales at worst.
        var (sales, _, _) = LinkTargets();
        var item = await OnlyCandidateAsync(ClientFor(Roles.HiringManager, _factory.HiringManagerUserId), "Linktarget");

        Assert.Equal($"/jobpostings/{sales.JobPostingId}?application={sales.Id}", item.TargetUrl);
    }

    [Fact]
    public async Task Reach_Through_A_Panel_Only_Links_To_The_Interview_Not_The_Board()
    {
        // ADR-0017 §4: panel membership grants one application, not the posting around it — so
        // the board would 404, and /interviews/:id is the page built for exactly this caller.
        var (_, _, laterRound) = LinkTargets();

        var manager = await OnlyCandidateAsync(ClientFor(Roles.HiringManager, _factory.HiringManagerUserId), "Panelreach");
        Assert.Equal($"/interviews/{laterRound.Id}", manager.TargetUrl);

        // ADR-0018: an Approver never reaches a board, even on a panel.
        var approver = await OnlyCandidateAsync(ClientFor(Roles.Approver, _factory.FinanceApproverUserId), "Panelreach");
        Assert.Equal($"/interviews/{laterRound.Id}", approver.TargetUrl);
    }

    [Fact]
    public async Task Test1_Unauthenticated_Search_Returns_401()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/api/search?q=architect");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Test2_Search_WithEmptyQuery_Returns_400BadRequest()
    {
        var client = ClientFor(Roles.Admin, _factory.AdminUserId);

        var resEmpty = await client.GetAsync("/api/search?q=");
        Assert.Equal(HttpStatusCode.BadRequest, resEmpty.StatusCode);

        var resWhitespace = await client.GetAsync("/api/search?q=   ");
        Assert.Equal(HttpStatusCode.BadRequest, resWhitespace.StatusCode);
    }

    [Fact]
    public async Task Test3_Admin_Search_Returns_Ranked_Results_Across_All_Categories()
    {
        var client = ClientFor(Roles.Admin, _factory.AdminUserId);
        var res = await client.GetAsync("/api/search?q=Architect");
        res.EnsureSuccessStatusCode();

        var dto = await res.Content.ReadFromJsonAsync<SearchResponseDto>();
        Assert.NotNull(dto);
        Assert.Equal("Architect", dto!.Query);
        Assert.True(dto.TotalMatches >= 2);
        Assert.NotEmpty(dto.Items);

        Assert.Contains(dto.Items, i => i.Category == "Postings" && i.Title.Contains("Architect"));
        Assert.Contains(dto.Items, i => i.Category == "Requisitions" && i.Title.Contains("Architect"));
    }

    [Fact]
    public async Task Test4_Search_WithZawgyiBurmeseQuery_Normalizes_To_Unicode_And_Matches()
    {
        var client = ClientFor(Roles.Admin, _factory.AdminUserId);

        // Zawgyi input for "အောင်" (\u1031\u1021\u102B\u1004\u103A)
        string zawgyiQuery = "\u1031\u1021\u102B\u1004\u103A";

        var res = await client.GetAsync($"/api/search?q={Uri.EscapeDataString(zawgyiQuery)}");
        res.EnsureSuccessStatusCode();

        var dto = await res.Content.ReadFromJsonAsync<SearchResponseDto>();
        Assert.NotNull(dto);
        Assert.NotEqual(zawgyiQuery, dto!.NormalizedQuery); // Proves Zawgyi -> Unicode NFC conversion occurred
        Assert.Equal("အောင်", dto.NormalizedQuery);
        Assert.True(dto.TotalMatches >= 1);
        Assert.Contains(dto.Items, i => i.Title.Contains("အောင်အောင်"));
    }

    [Fact]
    public async Task Test5_Search_Candidates_By_ResumeExtractedText_Returns_Matching_Candidate()
    {
        var client = ClientFor(Roles.Admin, _factory.AdminUserId);
        var res = await client.GetAsync("/api/search?q=Kubernetes");
        res.EnsureSuccessStatusCode();

        var dto = await res.Content.ReadFromJsonAsync<SearchResponseDto>();
        Assert.NotNull(dto);
        Assert.True(dto!.TotalMatches >= 1);

        var candidateMatch = dto.Items.FirstOrDefault(i => i.Category == "Candidates");
        Assert.NotNull(candidateMatch);
        Assert.Equal("Aung San Suu Kyi", candidateMatch!.Title);
        Assert.Contains("Kubernetes", candidateMatch.DescriptionSnippet);
    }

    [Fact]
    public async Task Test6_Search_Category_Filter_Candidates_Only_Returns_Only_Candidates()
    {
        var client = ClientFor(Roles.Admin, _factory.AdminUserId);
        var res = await client.GetAsync("/api/search?q=Architect&category=Candidates");
        res.EnsureSuccessStatusCode();

        var dto = await res.Content.ReadFromJsonAsync<SearchResponseDto>();
        Assert.NotNull(dto);
        Assert.Equal("Candidates", dto!.Category);
        Assert.All(dto.Items, item => Assert.Equal("Candidates", item.Category));
    }

    [Fact]
    public async Task Test7_HiringManager_Search_Enforces_Department_Scoping_ADR0003()
    {
        // Sales Hiring Manager only owns SalesDepartmentId
        var client = ClientFor(Roles.HiringManager, _factory.HiringManagerUserId);

        // Search for "Accountant" (which belongs to Finance department)
        var res = await client.GetAsync("/api/search?q=Accountant");
        res.EnsureSuccessStatusCode();

        var dto = await res.Content.ReadFromJsonAsync<SearchResponseDto>();
        Assert.NotNull(dto);
        // Sales manager should NOT see Finance requisitions or postings
        Assert.DoesNotContain(dto!.Items, i => i.DepartmentId == _factory.FinanceDepartmentId);
    }

    [Fact]
    public async Task Test8_Approver_Role_Search_Excludes_Candidate_Data_ADR0018()
    {
        var client = ClientFor(Roles.Approver, _factory.FinanceApproverUserId);

        // Approvers can search requisitions but are strictly excluded from candidate data (ADR-0018)
        var res = await client.GetAsync("/api/search?q=Aung");
        res.EnsureSuccessStatusCode();

        var dto = await res.Content.ReadFromJsonAsync<SearchResponseDto>();
        Assert.NotNull(dto);
        Assert.DoesNotContain(dto!.Items, i => i.Category == "Candidates");
    }

    [Fact]
    public async Task Test9_Search_Pagination_Returns_Correct_Page_And_PageSize()
    {
        var client = ClientFor(Roles.Admin, _factory.AdminUserId);
        var res = await client.GetAsync("/api/search?q=a&page=1&pageSize=1");
        res.EnsureSuccessStatusCode();

        var dto = await res.Content.ReadFromJsonAsync<SearchResponseDto>();
        Assert.NotNull(dto);
        Assert.Equal(1, dto!.Page);
        Assert.Equal(1, dto.PageSize);
        Assert.True(dto.Items.Count <= 1);
    }

    [Fact]
    public async Task Test10_Tenant_Isolation_Search_Does_Not_Leak_Cross_Tenant_Data()
    {
        // Query as Tenant B user
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Tenant", _factory.TenantB.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Roles", Roles.Admin);

        var res = await client.GetAsync("/api/search?q=Architect");
        res.EnsureSuccessStatusCode();

        var dto = await res.Content.ReadFromJsonAsync<SearchResponseDto>();
        Assert.NotNull(dto);
        // Tenant A data must NOT be visible to Tenant B
        Assert.Equal(0, dto!.TotalMatches);
        Assert.Empty(dto.Items);
    }
}
