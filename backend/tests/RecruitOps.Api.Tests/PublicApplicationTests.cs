using System.Net;
using System.Net.Http.Json;
using RecruitOps.Api.Auth;
using RecruitOps.Application.DTOs;
using Xunit;

namespace RecruitOps.Api.Tests;

/// <summary>Module 2.1/2.2/2.5/2.7 — the anonymous applicant path and what it feeds.
///
/// <para>This is the only endpoint in the product that is both unauthenticated and writing,
/// so most of what is asserted here is about what it must <em>refuse</em> to do.</para>
/// </summary>
public class PublicApplicationTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;

    public PublicApplicationTests(CustomWebAppFactory factory) => _factory = factory;

    private HttpClient Internal(string role, Guid? userId = null)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Tenant", _factory.TenantA.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Roles", role);
        if (userId is not null)
            client.DefaultRequestHeaders.Add("X-Test-UserId", userId.Value.ToString());
        return client;
    }

    /// <summary>No tenant header, no role — exactly what a member of the public sends.</summary>
    private HttpClient Anonymous() => _factory.CreateClient();

    private async Task<JobPostingDetailDto> LivePostingAsync(
        string title, bool showSalary = false, string? formFieldsJson = null)
    {
        var create = await Internal(Roles.HiringManager, _factory.HiringManagerUserId)
            .PostAsJsonAsync("/api/requisitions", new CreateRequisitionRequest
            {
                DepartmentId = _factory.SalesDepartmentId,
                Title = title,
                JobDescription = "Internal JD.",
                Headcount = 1,
                SalaryBudget = 750_000m,
            });
        var draft = (await create.Content.ReadFromJsonAsync<RequisitionDetailDto>())!;

        await Internal(Roles.HiringManager, _factory.HiringManagerUserId)
            .PostAsync($"/api/requisitions/{draft.Id}/submit", null);
        await Internal(Roles.Admin, _factory.AdminUserId)
            .PostAsJsonAsync($"/api/requisitions/{draft.Id}/decision", new ApprovalDecisionRequest { Approve = true });
        await Internal(Roles.Approver, _factory.FinanceApproverUserId)
            .PostAsJsonAsync($"/api/requisitions/{draft.Id}/decision", new ApprovalDecisionRequest { Approve = true });

        var postingRes = await Internal(Roles.Recruiter)
            .PostAsJsonAsync("/api/jobpostings", new CreateJobPostingRequest { RequisitionId = draft.Id });
        var posting = (await postingRes.Content.ReadFromJsonAsync<JobPostingDetailDto>())!;

        await Internal(Roles.Recruiter).PutAsJsonAsync($"/api/jobpostings/{posting.Id}", new UpdateJobPostingRequest
        {
            Title = title,
            Description = "Public-facing copy.",
            Location = "Yangon",
            EmploymentType = "FullTime",
            Headcount = 1,
            SalaryMin = 700_000m,
            SalaryMax = 900_000m,
            ShowSalary = showSalary,
            ApplicationFormFieldsJson = formFieldsJson,
        });

        var live = await Internal(Roles.Recruiter).PostAsync($"/api/jobpostings/{posting.Id}/publish", null);
        return (await live.Content.ReadFromJsonAsync<JobPostingDetailDto>())!;
    }

    [Fact]
    public async Task Anyone_Can_View_A_Published_Job_Without_Logging_In()
    {
        var posting = await LivePostingAsync("Public Sales Role");

        var res = await Anonymous().GetAsync($"/api/public/jobs/{posting.PublicToken}");
        res.EnsureSuccessStatusCode();

        var job = (await res.Content.ReadFromJsonAsync<PublicJobDto>())!;
        Assert.Equal("Public Sales Role", job.Title);
        Assert.Equal("Yangon", job.Location);
        Assert.True(job.IsOpen);
    }

    [Fact]
    public async Task Salary_Is_Hidden_Unless_The_Posting_Opted_In()
    {
        var hidden = await LivePostingAsync("Quiet Salary Role", showSalary: false);
        var shown = await LivePostingAsync("Open Salary Role", showSalary: true);

        var hiddenJob = await Anonymous()
            .GetFromJsonAsync<PublicJobDto>($"/api/public/jobs/{hidden.PublicToken}");
        var shownJob = await Anonymous()
            .GetFromJsonAsync<PublicJobDto>($"/api/public/jobs/{shown.PublicToken}");

        // The requisition's budget is internal. It reaching a page indexed by Facebook
        // because someone reused the internal DTO is the failure this guards against.
        Assert.Null(hiddenJob!.SalaryRange);
        Assert.NotNull(shownJob!.SalaryRange);
    }

    [Fact]
    public async Task An_Unknown_Token_Is_Indistinguishable_From_An_Unpublished_One()
    {
        // Draft: a posting exists, but has no public existence.
        var create = await Internal(Roles.HiringManager, _factory.HiringManagerUserId)
            .PostAsJsonAsync("/api/requisitions", new CreateRequisitionRequest
            {
                DepartmentId = _factory.SalesDepartmentId,
                Title = "Never Published",
                JobDescription = "Internal only.",
                Headcount = 1,
            });
        var draft = (await create.Content.ReadFromJsonAsync<RequisitionDetailDto>())!;

        var unknown = await Anonymous().GetAsync("/api/public/jobs/definitely-not-a-real-token");
        var byRequisitionGuid = await Anonymous().GetAsync($"/api/public/jobs/{draft.Id}");

        // Same answer for both, so nobody can tell a near-miss from a wrong guess.
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, byRequisitionGuid.StatusCode);
    }

    [Fact]
    public async Task Applying_Creates_A_Candidate_And_An_Applied_Pipeline_Entry()
    {
        var posting = await LivePostingAsync("Sales Associate (public)");

        var res = await Anonymous().PostAsJsonAsync(
            $"/api/public/jobs/{posting.PublicToken}/apply",
            new SubmitApplicationRequest
            {
                FullName = "Aung Aung",
                Email = "Aung.Aung@Example.com",
                Phone = "+95 9 765 432 100",
                CoverNote = "I would like to apply.",
            });
        res.EnsureSuccessStatusCode();

        var pipeline = await Internal(Roles.Recruiter)
            .GetFromJsonAsync<List<PipelineItemDto>>($"/api/jobpostings/{posting.Id}/pipeline");

        var item = Assert.Single(pipeline!);
        Assert.Equal("Aung Aung", item.CandidateName);
        // Applied, not Sourced: they came in through the form rather than being added.
        Assert.Equal("Applied", item.Status);
        // Normalised on the way in, so a later duplicate check can actually match.
        Assert.Equal("aung.aung@example.com", item.Email);
        Assert.Equal("09765432100", item.Phone);
    }

    [Fact]
    public async Task The_Same_Person_Applying_Twice_Is_One_Candidate()
    {
        var first = await LivePostingAsync("Sales Role A");
        var second = await LivePostingAsync("Sales Role B");

        // Same phone, formatted differently, and no email the second time — which is how
        // real applicants behave, and exactly what naive matching misses (Module 2.7).
        await Anonymous().PostAsJsonAsync($"/api/public/jobs/{first.PublicToken}/apply",
            new SubmitApplicationRequest { FullName = "Ma Hla", Phone = "09 111 222 333" });
        await Anonymous().PostAsJsonAsync($"/api/public/jobs/{second.PublicToken}/apply",
            new SubmitApplicationRequest { FullName = "Ma Hla", Phone = "+959111222333" });

        var a = await Internal(Roles.Recruiter)
            .GetFromJsonAsync<List<PipelineItemDto>>($"/api/jobpostings/{first.Id}/pipeline");
        var b = await Internal(Roles.Recruiter)
            .GetFromJsonAsync<List<PipelineItemDto>>($"/api/jobpostings/{second.Id}/pipeline");

        // Two applications, one person — which is what makes a 360° candidate history real.
        Assert.Equal(a!.Single().CandidateId, b!.Single().CandidateId);
    }

    [Fact]
    public async Task An_Application_With_No_Contact_Details_Is_Refused()
    {
        var posting = await LivePostingAsync("Sales Role C");

        var res = await Anonymous().PostAsJsonAsync($"/api/public/jobs/{posting.PublicToken}/apply",
            new SubmitApplicationRequest { FullName = "Anonymous Applicant" });

        // Without an email or phone nobody can ever be told the outcome, and duplicate
        // detection has nothing to key on.
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task A_Closed_Posting_Stops_New_Applications_But_Keeps_The_Old_Ones()
    {
        var posting = await LivePostingAsync("Sales Role D");

        await Anonymous().PostAsJsonAsync($"/api/public/jobs/{posting.PublicToken}/apply",
            new SubmitApplicationRequest { FullName = "Early Bird", Email = "early@example.com" });

        await Internal(Roles.Recruiter).PostAsync($"/api/jobpostings/{posting.Id}/close", null);

        var late = await Anonymous().PostAsJsonAsync($"/api/public/jobs/{posting.PublicToken}/apply",
            new SubmitApplicationRequest { FullName = "Late Comer", Email = "late@example.com" });
        Assert.Equal(HttpStatusCode.Conflict, late.StatusCode);

        // Closing the advert stops new candidates arriving; it does not reject the ones
        // who already did.
        var pipeline = await Internal(Roles.Recruiter)
            .GetFromJsonAsync<List<PipelineItemDto>>($"/api/jobpostings/{posting.Id}/pipeline");
        Assert.Single(pipeline!);
    }

    [Fact]
    public async Task Stage_History_Is_Written_From_The_First_Moment()
    {
        var posting = await LivePostingAsync("Sales Role E");
        await Anonymous().PostAsJsonAsync($"/api/public/jobs/{posting.PublicToken}/apply",
            new SubmitApplicationRequest { FullName = "Stage Walker", Email = "walker@example.com" });

        var pipeline = await Internal(Roles.Recruiter)
            .GetFromJsonAsync<List<PipelineItemDto>>($"/api/jobpostings/{posting.Id}/pipeline");
        var applicationId = pipeline!.Single().Id;

        await Internal(Roles.Recruiter, _factory.AdminUserId).PostAsJsonAsync(
            $"/api/applications/{applicationId}/stage",
            new MoveStageRequest { ToStatus = "Screening", Note = "CV looks relevant." });

        var history = await Internal(Roles.Recruiter)
            .GetFromJsonAsync<List<StageHistoryItemDto>>($"/api/applications/{applicationId}/history");

        // Module 5 measures time-in-stage from these rows, so the arrival itself has to be
        // recorded — not just the changes a recruiter makes afterwards.
        Assert.Equal(2, history!.Count);
        Assert.Null(history[0].FromStatus);
        Assert.Equal("Applied", history[0].ToStatus);
        Assert.Null(history[0].ChangedByName); // nobody was logged in
        Assert.Equal("Applied", history[1].FromStatus);
        Assert.Equal("Screening", history[1].ToStatus);
        Assert.Equal("CV looks relevant.", history[1].Note);
    }

    [Fact]
    public async Task A_Terminal_Application_Cannot_Be_Reopened()
    {
        var posting = await LivePostingAsync("Sales Role F");
        await Anonymous().PostAsJsonAsync($"/api/public/jobs/{posting.PublicToken}/apply",
            new SubmitApplicationRequest { FullName = "Final Answer", Email = "final@example.com" });

        var pipeline = await Internal(Roles.Recruiter)
            .GetFromJsonAsync<List<PipelineItemDto>>($"/api/jobpostings/{posting.Id}/pipeline");
        var applicationId = pipeline!.Single().Id;

        await Internal(Roles.Recruiter, _factory.AdminUserId).PostAsJsonAsync(
            $"/api/applications/{applicationId}/stage", new MoveStageRequest { ToStatus = "Rejected" });

        // Moving back out would silently corrupt time-to-hire and conversion figures,
        // which are computed from this history.
        var reopen = await Internal(Roles.Recruiter, _factory.AdminUserId).PostAsJsonAsync(
            $"/api/applications/{applicationId}/stage", new MoveStageRequest { ToStatus = "Screening" });

        Assert.Equal(HttpStatusCode.Conflict, reopen.StatusCode);
    }

    // ── Custom application fields (Module 2.2) ───────────────────────────────

    private const string SalaryAndShiftForm = """
        [
          { "key": "expected_salary", "label": "Expected salary", "type": "number", "required": true },
          { "key": "shift", "label": "Preferred shift", "type": "select", "required": false,
            "options": ["Day", "Night"] }
        ]
        """;

    [Fact]
    public async Task Custom_Answers_Reach_The_Pipeline_And_Are_Rebuilt_Not_Passed_Through()
    {
        var posting = await LivePostingAsync("Sales Role H", formFieldsJson: SalaryAndShiftForm);

        var job = await Anonymous().GetFromJsonAsync<PublicJobDto>($"/api/public/jobs/{posting.PublicToken}");
        // The public page needs the schema to render the questions at all.
        Assert.NotNull(job!.ApplicationFormFieldsJson);

        var res = await Anonymous().PostAsJsonAsync($"/api/public/jobs/{posting.PublicToken}/apply",
            new SubmitApplicationRequest
            {
                FullName = "Custom Answerer",
                Email = "custom@example.com",
                // "not_a_field" is the point: an anonymous caller must not be able to write
                // arbitrary JSON into the customer's database under cover of a custom field.
                CustomFieldsJson = """{ "expected_salary": "850000", "shift": "Night", "not_a_field": "smuggled" }""",
            });
        res.EnsureSuccessStatusCode();

        var pipeline = await Internal(Roles.Recruiter)
            .GetFromJsonAsync<List<PipelineItemDto>>($"/api/jobpostings/{posting.Id}/pipeline");
        var stored = pipeline!.Single().CustomFieldsJson;

        Assert.NotNull(stored);
        Assert.Contains("expected_salary", stored);
        Assert.Contains("Night", stored);
        Assert.DoesNotContain("not_a_field", stored);
        Assert.DoesNotContain("smuggled", stored);
    }

    [Fact]
    public async Task A_Missing_Required_Custom_Answer_Blocks_The_Application()
    {
        var posting = await LivePostingAsync("Sales Role I", formFieldsJson: SalaryAndShiftForm);

        var res = await Anonymous().PostAsJsonAsync($"/api/public/jobs/{posting.PublicToken}/apply",
            new SubmitApplicationRequest
            {
                FullName = "Incomplete",
                Email = "incomplete@example.com",
                CustomFieldsJson = """{ "shift": "Day" }""",
            });

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);

        // And nothing was half-written: the candidate row must not survive a refused application.
        var pipeline = await Internal(Roles.Recruiter)
            .GetFromJsonAsync<List<PipelineItemDto>>($"/api/jobpostings/{posting.Id}/pipeline");
        Assert.Empty(pipeline!);
    }

    [Fact]
    public async Task A_Broken_Form_Schema_Is_Refused_When_The_Recruiter_Saves_It()
    {
        var posting = await LivePostingAsync("Sales Role J");

        // Caught here rather than on the public page, where it would surface to a stranger
        // with nobody watching.
        var res = await Internal(Roles.Recruiter).PutAsJsonAsync(
            $"/api/jobpostings/{posting.Id}", new UpdateJobPostingRequest
            {
                Title = "Sales Role J",
                Description = "Copy.",
                EmploymentType = "FullTime",
                Headcount = 1,
                ApplicationFormFieldsJson = """[{ "key": "bad key", "label": "Nope", "type": "text" }]""",
            });

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task A_Hiring_Manager_Cannot_See_Another_Departments_Pipeline()
    {
        var posting = await LivePostingAsync("Sales Role G");

        // The seeded hiring manager owns Sales, so they SHOULD see this one...
        var mine = await Internal(Roles.HiringManager, _factory.HiringManagerUserId)
            .GetAsync($"/api/jobpostings/{posting.Id}/pipeline");
        mine.EnsureSuccessStatusCode();

        // ...and a manager with no departments at all must get 404, not an empty list:
        // "no such posting" and "a posting you may not see" have to look identical (ADR-0003).
        var theirs = await Internal(Roles.HiringManager, Guid.NewGuid())
            .GetAsync($"/api/jobpostings/{posting.Id}/pipeline");
        Assert.Equal(HttpStatusCode.NotFound, theirs.StatusCode);
    }
}
