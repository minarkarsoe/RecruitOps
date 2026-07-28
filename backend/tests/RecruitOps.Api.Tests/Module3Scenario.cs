using System.Net.Http.Json;
using RecruitOps.Api.Auth;
using RecruitOps.Application.DTOs;

namespace RecruitOps.Api.Tests;

/// <summary>Drives the Module 1 + 2 loop far enough to have a real job application to
/// interview against, so the Module 3 tests can start where they actually mean to.
/// <para>Shared rather than copied into each test class: the chain is six requests long and
/// a divergent copy would silently test a different starting state.</para></summary>
internal sealed class Module3Scenario
{
    private readonly CustomWebAppFactory _factory;

    public Module3Scenario(CustomWebAppFactory factory) => _factory = factory;

    public HttpClient Client(string role, Guid? userId = null)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Tenant", _factory.TenantA.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Roles", role);
        if (userId is not null)
            client.DefaultRequestHeaders.Add("X-Test-UserId", userId.Value.ToString());
        return client;
    }

    public HttpClient Recruiter() => Client(Roles.Recruiter, _factory.AdminUserId);

    public HttpClient SalesManager() => Client(Roles.HiringManager, _factory.HiringManagerUserId);

    public HttpClient FinanceManager() => Client(Roles.HiringManager, _factory.FinanceManagerUserId);

    /// <summary>The Finance approver — a real user who signs off on requisitions and,
    /// per ADR-0018, reaches no candidate data at all unless put on a panel.</summary>
    public HttpClient FinanceApprover() => Client(Roles.Approver, _factory.FinanceApproverUserId);

    /// <summary>A live posting in the Sales department with one applicant in the pipeline.</summary>
    public async Task<(Guid PostingId, Guid ApplicationId)> ApplicationAsync(string title)
    {
        var create = await SalesManager().PostAsJsonAsync("/api/requisitions", new CreateRequisitionRequest
        {
            DepartmentId = _factory.SalesDepartmentId,
            Title = title,
            JobDescription = "Internal JD.",
            Headcount = 1,
            SalaryBudget = 800_000m,
        });
        create.EnsureSuccessStatusCode();
        var draft = (await create.Content.ReadFromJsonAsync<RequisitionDetailDto>())!;

        await SalesManager().PostAsync($"/api/requisitions/{draft.Id}/submit", null);
        await Client(Roles.Admin, _factory.AdminUserId)
            .PostAsJsonAsync($"/api/requisitions/{draft.Id}/decision",
                new ApprovalDecisionRequest { Approve = true });
        await Client(Roles.Approver, _factory.FinanceApproverUserId)
            .PostAsJsonAsync($"/api/requisitions/{draft.Id}/decision",
                new ApprovalDecisionRequest { Approve = true });

        var postingRes = await Recruiter()
            .PostAsJsonAsync("/api/jobpostings", new CreateJobPostingRequest { RequisitionId = draft.Id });
        postingRes.EnsureSuccessStatusCode();
        var posting = (await postingRes.Content.ReadFromJsonAsync<JobPostingDetailDto>())!;

        var publishRes = await Recruiter().PostAsync($"/api/jobpostings/{posting.Id}/publish", null);
        publishRes.EnsureSuccessStatusCode();
        var live = (await publishRes.Content.ReadFromJsonAsync<JobPostingDetailDto>())!;

        // Anonymous, like a real applicant — which is also what puts the first
        // ApplicationStageHistory row in place.
        var apply = await _factory.CreateClient()
            .PostAsJsonAsync($"/api/public/jobs/{live.PublicToken}/apply", new SubmitApplicationRequest
            {
                FullName = $"Applicant for {title}",
                Email = $"applicant.{Guid.NewGuid():N}@example.test",
            });
        apply.EnsureSuccessStatusCode();

        var pipeline = await Recruiter()
            .GetFromJsonAsync<List<PipelineItemDto>>($"/api/jobpostings/{posting.Id}/pipeline");

        return (posting.Id, pipeline!.Single().Id);
    }

    /// <summary>Ensures a company-wide scorecard template exists and returns it.
    /// <para>Idempotent: the service refuses a second active template for the same scope,
    /// and every test in a class shares one database.</para></summary>
    public async Task<ScorecardTemplateDto> EnsureCompanyTemplateAsync()
    {
        var existing = await Recruiter()
            .GetFromJsonAsync<List<ScorecardTemplateDto>>("/api/scorecardtemplates");

        var companyWide = existing!.FirstOrDefault(t => t.DepartmentId is null && t.JobPostingId is null);
        if (companyWide is not null) return companyWide;

        var res = await Recruiter().PostAsJsonAsync("/api/scorecardtemplates",
            new SaveScorecardTemplateRequest
            {
                Name = "Default interview scorecard",
                Criteria = new List<ScorecardCriterionInput>
                {
                    new() { Label = "Communication", Type = "Rating", IsRequired = true },
                    new() { Label = "Role knowledge", Type = "Rating", IsRequired = true },
                    new() { Label = "Evidence", Type = "Text", IsRequired = false },
                },
            });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<ScorecardTemplateDto>())!;
    }

    /// <summary>Schedules a round with both hiring managers on the panel — one of whom
    /// (Finance) has no departmental access to this application at all.</summary>
    public Task<InterviewDto> ScheduleAsync(Guid applicationId)
        => ScheduleWithAsync(
            applicationId, _factory.HiringManagerUserId, _factory.FinanceManagerUserId);

    /// <summary>Schedules a round with a chosen panel. The first id is the lead.
    /// <para>Used to put someone on a panel deliberately — participation is the only route
    /// by which a role excluded from candidate data reaches an application (ADR-0018).</para></summary>
    public async Task<InterviewDto> ScheduleWithAsync(
        Guid applicationId, params Guid[] participantUserIds)
    {
        var res = await Recruiter().PostAsJsonAsync(
            $"/api/applications/{applicationId}/interviews",
            new ScheduleInterviewRequest
            {
                ScheduledStart = new DateTimeOffset(2026, 8, 3, 9, 0, 0, TimeSpan.Zero),
                DurationMinutes = 60,
                Mode = "Video",
                Location = "https://meet.example.test/abc",
                ParticipantUserIds = participantUserIds,
                LeadUserId = participantUserIds[0],
            });

        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<InterviewDto>())!;
    }

    public static SaveScorecardRequest CompleteAnswers(
        ScorecardTemplateDto template, string recommendation, int rating = 4) =>
        new()
        {
            Recommendation = recommendation,
            SummaryComment = "Assessed against the standard criteria.",
            Answers = template.Criteria.Select(c => new ScorecardAnswerInput
            {
                ScorecardCriterionId = c.Id,
                Rating = c.Type == "Rating" ? rating : null,
                YesNo = c.Type == "YesNo" ? true : null,
                Comment = c.Type == "Text" ? "Worked through a real example." : null,
            }).ToList(),
        };
}
