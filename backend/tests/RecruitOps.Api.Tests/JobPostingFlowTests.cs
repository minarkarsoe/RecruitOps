using System.Net;
using System.Net.Http.Json;
using RecruitOps.Api.Auth;
using RecruitOps.Application.DTOs;
using Xunit;

namespace RecruitOps.Api.Tests;

/// <summary>Module 2.1 — approved requisition → posting → public link.
/// The governing rule under test is that nothing reaches the public without approval.</summary>
public class JobPostingFlowTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;

    public JobPostingFlowTests(CustomWebAppFactory factory) => _factory = factory;

    private HttpClient ClientFor(string role, Guid? userId = null)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Tenant", _factory.TenantA.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Roles", role);
        if (userId is not null)
            client.DefaultRequestHeaders.Add("X-Test-UserId", userId.Value.ToString());
        return client;
    }

    /// <summary>Drives a requisition all the way to Approved so a posting can be made from it.</summary>
    private async Task<Guid> ApprovedRequisitionAsync(string title)
    {
        var create = await ClientFor(Roles.HiringManager, _factory.HiringManagerUserId)
            .PostAsJsonAsync("/api/requisitions", new CreateRequisitionRequest
            {
                DepartmentId = _factory.SalesDepartmentId,
                Title = title,
                JobDescription = "Approved internal JD.",
                Headcount = 2,
                SalaryBudget = 900_000m,
            });
        create.EnsureSuccessStatusCode();
        var draft = (await create.Content.ReadFromJsonAsync<RequisitionDetailDto>())!;

        await ClientFor(Roles.HiringManager, _factory.HiringManagerUserId)
            .PostAsync($"/api/requisitions/{draft.Id}/submit", null);
        await ClientFor(Roles.Admin, _factory.AdminUserId)
            .PostAsJsonAsync($"/api/requisitions/{draft.Id}/decision", new ApprovalDecisionRequest { Approve = true });
        await ClientFor(Roles.Approver, _factory.FinanceApproverUserId)
            .PostAsJsonAsync($"/api/requisitions/{draft.Id}/decision", new ApprovalDecisionRequest { Approve = true });

        return draft.Id;
    }

    private async Task<JobPostingDetailDto> PostingFromAsync(Guid requisitionId)
    {
        var res = await ClientFor(Roles.Recruiter)
            .PostAsJsonAsync("/api/jobpostings", new CreateJobPostingRequest { RequisitionId = requisitionId });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<JobPostingDetailDto>())!;
    }

    [Fact]
    public async Task Posting_Copies_The_Approved_Requisition_And_Starts_As_Draft()
    {
        var requisitionId = await ApprovedRequisitionAsync("Regional Sales Lead");
        var posting = await PostingFromAsync(requisitionId);

        Assert.Equal("Draft", posting.Status);
        Assert.Equal("Regional Sales Lead", posting.Title);
        Assert.Equal("Approved internal JD.", posting.Description);
        Assert.Equal(2, posting.Headcount);
        Assert.Equal(requisitionId, posting.RequisitionId);

        // The budget travels across, but must not be public until someone opts in —
        // otherwise publishing a job leaks the company's pay bands.
        Assert.False(posting.ShowSalary);

        // No link before publishing: there is nothing to share yet.
        Assert.Null(posting.PublicToken);
    }

    [Fact]
    public async Task An_Unapproved_Requisition_Cannot_Become_A_Posting()
    {
        // Draft only — never submitted, so nobody has approved this headcount.
        var create = await ClientFor(Roles.HiringManager, _factory.HiringManagerUserId)
            .PostAsJsonAsync("/api/requisitions", new CreateRequisitionRequest
            {
                DepartmentId = _factory.SalesDepartmentId,
                Title = "Unapproved Role",
                JobDescription = "Not yet blessed.",
                Headcount = 1,
            });
        var draft = (await create.Content.ReadFromJsonAsync<RequisitionDetailDto>())!;

        var res = await ClientFor(Roles.Recruiter)
            .PostAsJsonAsync("/api/jobpostings", new CreateJobPostingRequest { RequisitionId = draft.Id });

        // This is the product's central guarantee: no advert without an approval behind it.
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task A_Requisition_Cannot_Be_Published_Twice()
    {
        var requisitionId = await ApprovedRequisitionAsync("Sales Enablement Lead");
        await PostingFromAsync(requisitionId);

        var second = await ClientFor(Roles.Recruiter)
            .PostAsJsonAsync("/api/jobpostings", new CreateJobPostingRequest { RequisitionId = requisitionId });

        // One approval, one advert — otherwise approved headcount could be advertised twice.
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Publishing_Mints_A_Link_And_Republishing_Keeps_It()
    {
        var posting = await PostingFromAsync(await ApprovedRequisitionAsync("Sales Ops Manager"));

        var published = await ClientFor(Roles.Recruiter).PostAsync($"/api/jobpostings/{posting.Id}/publish", null);
        published.EnsureSuccessStatusCode();
        var live = (await published.Content.ReadFromJsonAsync<JobPostingDetailDto>())!;

        Assert.Equal("Live", live.Status);
        Assert.NotNull(live.PostedAt);
        Assert.False(string.IsNullOrWhiteSpace(live.PublicToken));
        // Guessability is the only thing protecting the page.
        Assert.True(live.PublicToken!.Length >= 32);

        // Publishing an already-Live posting is a conflict, and the token must survive:
        // re-issuing it would break every share already posted to Facebook.
        var again = await ClientFor(Roles.Recruiter).PostAsync($"/api/jobpostings/{posting.Id}/publish", null);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);

        var reread = await ClientFor(Roles.Recruiter)
            .GetFromJsonAsync<JobPostingDetailDto>($"/api/jobpostings/{posting.Id}");
        Assert.Equal(live.PublicToken, reread!.PublicToken);
    }

    [Fact]
    public async Task A_Hiring_Manager_Cannot_Create_A_Posting()
    {
        var requisitionId = await ApprovedRequisitionAsync("Sales Systems Analyst");

        // Raising a requisition and advertising it are different jobs. The hiring manager
        // asks for the headcount; a recruiter writes and publishes the advert.
        var res = await ClientFor(Roles.HiringManager, _factory.HiringManagerUserId)
            .PostAsJsonAsync("/api/jobpostings", new CreateJobPostingRequest { RequisitionId = requisitionId });

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task A_Closed_Posting_Cannot_Be_Edited()
    {
        var posting = await PostingFromAsync(await ApprovedRequisitionAsync("Sales Trainer"));
        await ClientFor(Roles.Recruiter).PostAsync($"/api/jobpostings/{posting.Id}/publish", null);
        await ClientFor(Roles.Recruiter).PostAsync($"/api/jobpostings/{posting.Id}/close", null);

        // People already applied against the advert as written; rewriting it afterwards
        // would change what they were shown with no way to tell them.
        var res = await ClientFor(Roles.Recruiter)
            .PutAsJsonAsync($"/api/jobpostings/{posting.Id}", new UpdateJobPostingRequest
            {
                Title = "Sales Trainer (revised)",
                Description = "Changed after the fact.",
                EmploymentType = "FullTime",
                Headcount = 1,
            });

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task Salary_Range_Must_Be_The_Right_Way_Round()
    {
        var posting = await PostingFromAsync(await ApprovedRequisitionAsync("Sales Data Analyst"));

        var res = await ClientFor(Roles.Recruiter)
            .PutAsJsonAsync($"/api/jobpostings/{posting.Id}", new UpdateJobPostingRequest
            {
                Title = "Sales Data Analyst",
                Description = "Public copy.",
                EmploymentType = "FullTime",
                Headcount = 1,
                SalaryMin = 2_000_000m,
                SalaryMax = 1_000_000m,
                ShowSalary = true,
            });

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }
}
