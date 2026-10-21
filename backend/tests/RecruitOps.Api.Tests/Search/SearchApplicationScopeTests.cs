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

/// <summary>Reaching a candidate is not reading every application (security review of Module 2.7,
/// M1). One person can hold applications in two departments — the public path reuses a match, and a
/// merge moves applications onto one record — so a scoped role's search may match and quote only
/// the applications that make the candidate reachable. Each test carries its own unique words, so
/// the shared fixture's other data cannot satisfy it.</summary>
public class SearchApplicationScopeTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;

    public SearchApplicationScopeTests(CustomWebAppFactory factory) => _factory = factory;

    private HttpClient ClientFor(string role, Guid userId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Tenant", _factory.TenantA.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Roles", role);
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString());
        return client;
    }

    private async Task<IReadOnlyList<SearchResultItemDto>> SearchAsync(HttpClient client, string term)
        => (await client.GetFromJsonAsync<SearchResponseDto>(
            $"/api/search?q={Uri.EscapeDataString(term)}&category=Candidates"))!.Items;

    private static string Word() => "w" + Guid.NewGuid().ToString("N")[..12];

    /// <summary>A live posting in <paramref name="departmentId"/>.</summary>
    private static async Task<JobPosting> PostingAsync(AppDbContext db, Guid tenantId, Guid departmentId, Guid requestedBy)
    {
        var requisition = new Requisition
        {
            TenantId = tenantId,
            DepartmentId = departmentId,
            RequestedByUserId = requestedBy,
            Title = "Scope test",
            JobDescription = "Scope test",
            Headcount = 1,
            Status = RequisitionStatus.Approved,
        };
        db.Requisitions.Add(requisition);
        var posting = new JobPosting
        {
            TenantId = tenantId,
            DepartmentId = departmentId,
            RequisitionId = requisition.Id,
            Title = "Scope test",
            Description = "Scope test",
            Status = JobStatus.Live,
        };
        db.JobPostings.Add(posting);
        await db.SaveChangesAsync();
        return posting;
    }

    [Fact]
    public async Task A_Hiring_Manager_Does_Not_Match_Or_Quote_Another_Departments_Application()
    {
        var name = "Scope Person " + Word();
        var salesWord = Word();
        var financeWord = Word();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var sales = await PostingAsync(db, _factory.TenantA, _factory.SalesDepartmentId, _factory.HiringManagerUserId);
            var finance = await PostingAsync(db, _factory.TenantA, _factory.FinanceDepartmentId, _factory.FinanceManagerUserId);
            var person = new Candidate
            {
                TenantId = _factory.TenantA,
                FullName = name,
                Email = $"{Word()}@example.test",
                Source = SourceChannel.Direct,
            };
            db.Candidates.Add(person);
            db.JobApplications.AddRange(
                new JobApplication
                {
                    TenantId = _factory.TenantA,
                    JobPostingId = sales.Id,
                    CandidateId = person.Id,
                    Status = PipelineStatus.Applied,
                    Source = SourceChannel.Direct,
                    AppliedAt = DateTimeOffset.UtcNow,
                    CoverNote = $"Sales cover note {salesWord}",
                },
                new JobApplication
                {
                    TenantId = _factory.TenantA,
                    JobPostingId = finance.Id,
                    CandidateId = person.Id,
                    Status = PipelineStatus.Applied,
                    Source = SourceChannel.Direct,
                    AppliedAt = DateTimeOffset.UtcNow,
                    CoverNote = $"Confidential finance cover note {financeWord}",
                });
            await db.SaveChangesAsync();
        }

        var salesManager = ClientFor(Roles.HiringManager, _factory.HiringManagerUserId);

        // Positive controls: the Sales manager reaches this person, through its own department's text…
        Assert.Contains(await SearchAsync(salesManager, salesWord), i => i.Title == name);
        // …and a company-wide role finds the Finance text.
        Assert.Contains(await SearchAsync(ClientFor(Roles.Recruiter, _factory.RecruiterUserId), financeWord), i => i.Title == name);

        // The Finance application is not the Sales manager's to match — or to have quoted back.
        var leaked = await SearchAsync(salesManager, financeWord);
        Assert.DoesNotContain(leaked, i => i.Title == name);
        Assert.DoesNotContain(leaked, i => i.DescriptionSnippet?.Contains(financeWord) == true);
    }

    [Fact]
    public async Task A_Cancelled_Round_Grants_No_Search_Reach()
    {
        var name = "Panel Person " + Word();
        Guid interviewId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var finance = await PostingAsync(db, _factory.TenantA, _factory.FinanceDepartmentId, _factory.FinanceManagerUserId);
            var person = new Candidate { TenantId = _factory.TenantA, FullName = name, Source = SourceChannel.Direct };
            var application = new JobApplication
            {
                TenantId = _factory.TenantA,
                JobPostingId = finance.Id,
                CandidateId = person.Id,
                Status = PipelineStatus.Interview,
                Source = SourceChannel.Direct,
                AppliedAt = DateTimeOffset.UtcNow,
            };
            var interview = new Interview
            {
                TenantId = _factory.TenantA,
                JobApplicationId = application.Id,
                ScheduledStart = DateTimeOffset.UtcNow.AddDays(2),
                Status = InterviewStatus.Scheduled,
            };
            db.Candidates.Add(person);
            db.JobApplications.Add(application);
            db.Interviews.Add(interview);
            db.InterviewParticipants.Add(new InterviewParticipant
            {
                TenantId = _factory.TenantA,
                InterviewId = interview.Id,
                UserId = _factory.FinanceApproverUserId,
            });
            await db.SaveChangesAsync();
            interviewId = interview.Id;
        }

        var approver = ClientFor(Roles.Approver, _factory.FinanceApproverUserId);

        // Positive control: a seat on a live round is the panel exception.
        Assert.Contains(await SearchAsync(approver, name), i => i.Title == name);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var interview = await db.Interviews.IgnoreQueryFilters().SingleAsync(i => i.Id == interviewId);
            interview.Status = InterviewStatus.Cancelled;
            await db.SaveChangesAsync();
        }

        Assert.DoesNotContain(await SearchAsync(approver, name), i => i.Title == name);
    }
}
