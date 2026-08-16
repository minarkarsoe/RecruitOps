using System.Net;
using System.Net.Http.Json;
using RecruitOps.Api.Auth;
using RecruitOps.Application.DTOs;
using Xunit;

namespace RecruitOps.Api.Tests;

/// <summary>
/// Requirement R4 — Comprehensive Multi-Module End-to-End API Integration Test Suite.
/// Tests complete connected user journey from Admin setup, requisition creation & sequential approval,
/// job posting & custom form publishing, public applicant submission & deduplication, pipeline stage advancement,
/// interview scheduling with panel assignment, blind scorecard evaluation & notes with @mentions,
/// through to complete stage history timeline verification.
/// </summary>
public class FullUserJourneyIntegrationTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;

    public FullUserJourneyIntegrationTests(CustomWebAppFactory factory)
    {
        _factory = factory;
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

    private HttpClient AnonymousClient() => _factory.CreateClient();

    [Fact]
    public async Task Full_User_Journey_E2E_Integration_Flow()
    {
        // =========================================================================
        // Step 1: Admin setup -> create department -> assign users
        // =========================================================================
        var adminClient = ClientFor(Roles.Admin, _factory.AdminUserId);

        var createDeptRes = await adminClient.PostAsJsonAsync("/api/departments", new CreateDepartmentRequest
        {
            Name = "Engineering E2E",
            Code = "ENG-E2E"
        });
        createDeptRes.EnsureSuccessStatusCode();
        var department = (await createDeptRes.Content.ReadFromJsonAsync<DepartmentDetailDto>())!;
        Assert.NotNull(department);
        Assert.Equal("Engineering E2E", department.Name);

        var setMembersRes = await adminClient.PutAsJsonAsync(
            $"/api/departments/{department.Id}/members",
            new SetDepartmentMembersRequest
            {
                UserIds = new[] { _factory.HiringManagerUserId, _factory.AdminUserId }
            });
        setMembersRes.EnsureSuccessStatusCode();

        var membersRes = await adminClient.GetFromJsonAsync<List<DepartmentMemberDto>>(
            $"/api/departments/{department.Id}/members");
        Assert.NotNull(membersRes);
        Assert.True(membersRes.Single(m => m.UserId == _factory.HiringManagerUserId).IsMember);

        // =========================================================================
        // Step 2: HiringManager -> create requisition -> submit for approval
        // =========================================================================
        var hmClient = ClientFor(Roles.HiringManager, _factory.HiringManagerUserId);

        var createReqRes = await hmClient.PostAsJsonAsync("/api/requisitions", new CreateRequisitionRequest
        {
            DepartmentId = department.Id,
            Title = "Lead Software Architect",
            JobDescription = "Design and build scalable platform architectures.",
            Headcount = 2,
            SalaryBudget = 1_800_000m
        });
        createReqRes.EnsureSuccessStatusCode();
        var requisitionDraft = (await createReqRes.Content.ReadFromJsonAsync<RequisitionDetailDto>())!;
        Assert.Equal("Draft", requisitionDraft.Status);
        Assert.Equal("Lead Software Architect", requisitionDraft.Title);

        var submitReqRes = await hmClient.PostAsync($"/api/requisitions/{requisitionDraft.Id}/submit", null);
        submitReqRes.EnsureSuccessStatusCode();
        var submittedReq = (await submitReqRes.Content.ReadFromJsonAsync<RequisitionDetailDto>())!;
        Assert.Equal("PendingApproval", submittedReq.Status);
        Assert.Equal("HR", submittedReq.AwaitingApprovalFrom);

        // =========================================================================
        // Step 3: Approver -> approve requisition (verify sequential approval logic)
        // =========================================================================
        var financeApproverClient = ClientFor(Roles.Approver, _factory.FinanceApproverUserId);

        // Step 3a: Verify the remaining queue rule. Since ADR-0024, step 2 approving early is
        // legitimate (it would close step 1 too), so the probe here is a forward *reject* —
        // still refused, and it leaves the chain untouched so the journey continues in order.
        var jumpQueueRes = await financeApproverClient.PostAsJsonAsync(
            $"/api/requisitions/{requisitionDraft.Id}/decision",
            new ApprovalDecisionRequest { Approve = false, Comment = "Trying to reject on HR's behalf." });
        Assert.Equal(HttpStatusCode.Conflict, jumpQueueRes.StatusCode);

        // Step 3b: Step 1 (HR / Admin) approves
        var step1ApproveRes = await adminClient.PostAsJsonAsync(
            $"/api/requisitions/{requisitionDraft.Id}/decision",
            new ApprovalDecisionRequest { Approve = true, Comment = "HR approval granted for headcount." });
        step1ApproveRes.EnsureSuccessStatusCode();
        var afterStep1 = (await step1ApproveRes.Content.ReadFromJsonAsync<RequisitionDetailDto>())!;
        Assert.Equal("PendingApproval", afterStep1.Status);
        Assert.Equal("Finance", afterStep1.AwaitingApprovalFrom);

        // Step 3c: Step 2 (Finance Approver) approves
        var step2ApproveRes = await financeApproverClient.PostAsJsonAsync(
            $"/api/requisitions/{requisitionDraft.Id}/decision",
            new ApprovalDecisionRequest { Approve = true, Comment = "Budget allocation approved." });
        step2ApproveRes.EnsureSuccessStatusCode();
        var approvedReq = (await step2ApproveRes.Content.ReadFromJsonAsync<RequisitionDetailDto>())!;
        Assert.Equal("Approved", approvedReq.Status);
        Assert.Null(approvedReq.AwaitingApprovalFrom);
        Assert.NotNull(approvedReq.DecidedAt);

        // =========================================================================
        // Step 4: Recruiter -> create job posting from approved requisition with custom application form schema -> publish posting
        // =========================================================================
        var recruiterClient = ClientFor(Roles.Recruiter, _factory.AdminUserId);

        // Ensure a company-wide scorecard template exists for interview evaluations
        var createTemplateRes = await recruiterClient.PostAsJsonAsync("/api/scorecardtemplates", new SaveScorecardTemplateRequest
        {
            Name = "E2E Assessment Template",
            Criteria = new List<ScorecardCriterionInput>
            {
                new() { Label = "Technical Competency", Type = "Rating", IsRequired = true },
                new() { Label = "Communication", Type = "Rating", IsRequired = true },
                new() { Label = "Written Remarks", Type = "Text", IsRequired = false }
            }
        });
        createTemplateRes.EnsureSuccessStatusCode();

        var createPostingRes = await recruiterClient.PostAsJsonAsync("/api/jobpostings", new CreateJobPostingRequest
        {
            RequisitionId = requisitionDraft.Id
        });
        createPostingRes.EnsureSuccessStatusCode();
        var draftPosting = (await createPostingRes.Content.ReadFromJsonAsync<JobPostingDetailDto>())!;
        Assert.Equal("Draft", draftPosting.Status);
        Assert.Equal(requisitionDraft.Id, draftPosting.RequisitionId);

        const string customFormFieldsJson = """
            [
              { "key": "github_url", "label": "GitHub Profile URL", "type": "text", "required": true },
              { "key": "years_exp", "label": "Years of Architecture Experience", "type": "number", "required": true }
            ]
            """;

        var updatePostingRes = await recruiterClient.PutAsJsonAsync(
            $"/api/jobpostings/{draftPosting.Id}",
            new UpdateJobPostingRequest
            {
                Title = "Lead Software Architect (Public)",
                Description = "Join our high-performing core platform team.",
                Location = "Yangon / Remote",
                EmploymentType = "FullTime",
                Headcount = 2,
                SalaryMin = 1_500_000m,
                SalaryMax = 2_000_000m,
                ShowSalary = true,
                ApplicationFormFieldsJson = customFormFieldsJson
            });
        updatePostingRes.EnsureSuccessStatusCode();

        var publishPostingRes = await recruiterClient.PostAsync($"/api/jobpostings/{draftPosting.Id}/publish", null);
        publishPostingRes.EnsureSuccessStatusCode();
        var livePosting = (await publishPostingRes.Content.ReadFromJsonAsync<JobPostingDetailDto>())!;
        Assert.Equal("Live", livePosting.Status);
        Assert.NotNull(livePosting.PostedAt);
        Assert.False(string.IsNullOrWhiteSpace(livePosting.PublicToken));

        // =========================================================================
        // Step 5: Anonymous applicant -> view public job page -> submit application with custom form answers
        // =========================================================================
        var anonClient = AnonymousClient();

        var getPublicJobRes = await anonClient.GetAsync($"/api/public/jobs/{livePosting.PublicToken}");
        getPublicJobRes.EnsureSuccessStatusCode();
        var publicJob = (await getPublicJobRes.Content.ReadFromJsonAsync<PublicJobDto>())!;
        Assert.Equal("Lead Software Architect (Public)", publicJob.Title);
        Assert.True(publicJob.IsOpen);
        Assert.NotNull(publicJob.ApplicationFormFieldsJson);
        Assert.Contains("github_url", publicJob.ApplicationFormFieldsJson);

        var submitApp1Res = await anonClient.PostAsJsonAsync(
            $"/api/public/jobs/{livePosting.PublicToken}/apply",
            new SubmitApplicationRequest
            {
                FullName = "Kyaw Kyaw",
                Email = "Kyaw.Kyaw@Example.com",
                Phone = "+95 9 123 456 789",
                CoverNote = "Passionate about enterprise system architecture.",
                CustomFieldsJson = """{ "github_url": "https://github.com/kyawkyaw", "years_exp": "8" }"""
            });
        submitApp1Res.EnsureSuccessStatusCode();
        var app1Response = (await submitApp1Res.Content.ReadFromJsonAsync<SubmitApplicationResponse>())!;
        Assert.NotNull(app1Response);

        // =========================================================================
        // Step 6: Candidate Deduplication -> submit application for candidate with matching email/phone in alternate phone format
        // =========================================================================
        var submitApp2Res = await anonClient.PostAsJsonAsync(
            $"/api/public/jobs/{livePosting.PublicToken}/apply",
            new SubmitApplicationRequest
            {
                FullName = "Kyaw Kyaw",
                Email = "kyaw.kyaw@example.com",
                Phone = "09123456789", // Alternate phone format: 09123456789 vs +95 9 123 456 789
                CoverNote = "Follow-up submission with updated contact information.",
                CustomFieldsJson = """{ "github_url": "https://github.com/kyawkyaw", "years_exp": "8" }"""
            });
        submitApp2Res.EnsureSuccessStatusCode();

        var pipelineRes = await recruiterClient.GetFromJsonAsync<List<PipelineItemDto>>(
            $"/api/jobpostings/{livePosting.Id}/pipeline");
        Assert.NotNull(pipelineRes);
        Assert.Equal(2, pipelineRes.Count);

        // Deduplication Assertion: Both applications belong to the same candidate entity
        var firstApp = pipelineRes[0];
        var secondApp = pipelineRes[1];
        Assert.Equal(firstApp.CandidateId, secondApp.CandidateId);
        Assert.Equal("kyaw.kyaw@example.com", firstApp.Email);
        Assert.Equal("09123456789", firstApp.Phone);

        // =========================================================================
        // Step 7: Recruiter -> view pipeline -> advance stage to Interview -> schedule interview round & assign panel members
        // =========================================================================
        var targetAppId = firstApp.Id;

        var moveStageRes = await recruiterClient.PostAsJsonAsync(
            $"/api/applications/{targetAppId}/stage",
            new MoveStageRequest
            {
                ToStatus = "Interview",
                Note = "Candidate selected for Round 1 Technical Assessment."
            });
        moveStageRes.EnsureSuccessStatusCode();
        var movedApp = (await moveStageRes.Content.ReadFromJsonAsync<PipelineItemDto>())!;
        Assert.Equal("Interview", movedApp.Status);

        var scheduleRes = await recruiterClient.PostAsJsonAsync(
            $"/api/applications/{targetAppId}/interviews",
            new ScheduleInterviewRequest
            {
                ScheduledStart = DateTimeOffset.UtcNow.AddDays(3),
                DurationMinutes = 60,
                Mode = "Video",
                Location = "https://meet.example.test/architect-interview",
                ParticipantUserIds = new[] { _factory.HiringManagerUserId, _factory.FinanceManagerUserId },
                LeadUserId = _factory.HiringManagerUserId
            });
        scheduleRes.EnsureSuccessStatusCode();
        var interview = (await scheduleRes.Content.ReadFromJsonAsync<InterviewDto>())!;
        Assert.Equal("Scheduled", interview.Status);
        Assert.Equal(1, interview.Round);
        Assert.Equal(2, interview.Participants.Count);
        Assert.NotNull(interview.ScorecardTemplateId);

        // =========================================================================
        // Step 8: Panel member -> submit scorecard under blind scoring -> verify blind state -> add notes with @mentions
        // =========================================================================
        var financeManagerClient = ClientFor(Roles.HiringManager, _factory.FinanceManagerUserId);
        var salesManagerClient = ClientFor(Roles.HiringManager, _factory.HiringManagerUserId);

        // Step 8a: Ensure template criteria are available
        var myScorecardTemplate = await salesManagerClient.GetFromJsonAsync<MyScorecardDto>(
            $"/api/interviews/{interview.Id}/scorecard");
        Assert.NotNull(myScorecardTemplate);
        Assert.NotEmpty(myScorecardTemplate.Criteria);

        // Step 8b: Finance Manager (Panel Member 1) submits scorecard
        var financeScorecardReq = new SaveScorecardRequest
        {
            Recommendation = "StrongYes",
            SummaryComment = "Exceptional system design knowledge and clean code practices.",
            Answers = myScorecardTemplate.Criteria.Select(c => new ScorecardAnswerInput
            {
                ScorecardCriterionId = c.Id,
                Rating = c.Type == "Rating" ? 5 : null,
                YesNo = c.Type == "YesNo" ? true : null,
                Comment = c.Type == "Text" ? "Outstanding architectural problem solving." : null
            }).ToList()
        };

        var submitFinanceRes = await financeManagerClient.PostAsJsonAsync(
            $"/api/interviews/{interview.Id}/scorecard/submit", financeScorecardReq);
        if (!submitFinanceRes.IsSuccessStatusCode)
        {
            var err = await submitFinanceRes.Content.ReadAsStringAsync();
            Assert.Fail($"submitFinanceRes failed with {submitFinanceRes.StatusCode}: {err}");
        }

        // Step 8c: Sales Manager (Panel Member 2) inspects panel scorecards BEFORE submitting own -> Verify Blind State
        var blindView = await salesManagerClient.GetFromJsonAsync<InterviewScorecardsDto>(
            $"/api/interviews/{interview.Id}/scorecards");
        Assert.NotNull(blindView);
        Assert.True(blindView.BlindedUntilYouSubmit, "Scorecards must be blinded until caller submits their own.");
        Assert.Empty(blindView.Visible);
        Assert.Equal(1, blindView.HiddenCount);

        // Step 8d: Sales Manager (Panel Member 2) submits own scorecard
        var salesScorecardReq = new SaveScorecardRequest
        {
            Recommendation = "Yes",
            SummaryComment = "Good technical fit for our team needs.",
            Answers = myScorecardTemplate.Criteria.Select(c => new ScorecardAnswerInput
            {
                ScorecardCriterionId = c.Id,
                Rating = c.Type == "Rating" ? 4 : null,
                YesNo = c.Type == "YesNo" ? true : null,
                Comment = c.Type == "Text" ? "Demonstrated solid experience." : null
            }).ToList()
        };

        var submitSalesRes = await salesManagerClient.PostAsJsonAsync(
            $"/api/interviews/{interview.Id}/scorecard/submit", salesScorecardReq);
        submitSalesRes.EnsureSuccessStatusCode();

        // Step 8e: Sales Manager inspects panel scorecards AFTER submitting -> Blind state unmasked
        var openView = await salesManagerClient.GetFromJsonAsync<InterviewScorecardsDto>(
            $"/api/interviews/{interview.Id}/scorecards");
        Assert.NotNull(openView);
        Assert.False(openView.BlindedUntilYouSubmit, "Scorecards must be unmasked after caller submits.");
        Assert.Equal(2, openView.Visible.Count);
        Assert.Equal(0, openView.HiddenCount);

        // Step 8f: Add notes with @mentions
        var addNoteRes = await recruiterClient.PostAsJsonAsync(
            $"/api/applications/{targetAppId}/notes",
            new CreateNoteRequest
            {
                Body = "@sales.manager Both panel members approved. Please review for final offer proposal.",
                InterviewId = interview.Id
            });
        addNoteRes.EnsureSuccessStatusCode();
        var createdNote = (await addNoteRes.Content.ReadFromJsonAsync<NoteDto>())!;
        Assert.NotNull(createdNote);
        Assert.Single(createdNote.Mentions);
        Assert.Equal("Sales Manager", createdNote.Mentions[0].DisplayName);
        Assert.Contains("class=\"mention\"", createdNote.BodyHtml);

        // =========================================================================
        // Step 9: Stage History Verification -> fetch application stage history -> verify complete timeline entries
        // =========================================================================
        var historyRes = await recruiterClient.GetFromJsonAsync<List<StageHistoryItemDto>>(
            $"/api/applications/{targetAppId}/history");
        Assert.NotNull(historyRes);
        Assert.True(historyRes.Count >= 2, "Expected at least 2 stage history entries (Applied and Interview).");

        // Verify transition timeline
        var firstEntry = historyRes[0];
        Assert.Null(firstEntry.FromStatus);
        Assert.Equal("Applied", firstEntry.ToStatus);
        Assert.Null(firstEntry.ChangedByName); // Public anonymous application submission

        var secondEntry = historyRes[1];
        Assert.Equal("Applied", secondEntry.FromStatus);
        Assert.Equal("Interview", secondEntry.ToStatus);
        Assert.Equal("Candidate selected for Round 1 Technical Assessment.", secondEntry.Note);
        Assert.Equal("Alpha Admin", secondEntry.ChangedByName);

        Assert.True(firstEntry.ChangedAt <= secondEntry.ChangedAt);
    }

    [Fact]
    public async Task Candidate_Deduplication_Normalizes_Phone_And_Email_Formats()
    {
        var hmClient = ClientFor(Roles.HiringManager, _factory.HiringManagerUserId);
        var adminClient = ClientFor(Roles.Admin, _factory.AdminUserId);
        var recruiterClient = ClientFor(Roles.Recruiter, _factory.AdminUserId);

        var createReq = await hmClient.PostAsJsonAsync("/api/requisitions", new CreateRequisitionRequest
        {
            DepartmentId = _factory.SalesDepartmentId,
            Title = "Deduplication Test Role",
            JobDescription = "Testing phone/email normalization.",
            Headcount = 1
        });
        createReq.EnsureSuccessStatusCode();
        var draft = (await createReq.Content.ReadFromJsonAsync<RequisitionDetailDto>())!;

        await hmClient.PostAsync($"/api/requisitions/{draft.Id}/submit", null);
        await adminClient.PostAsJsonAsync($"/api/requisitions/{draft.Id}/decision", new ApprovalDecisionRequest { Approve = true });
        await ClientFor(Roles.Approver, _factory.FinanceApproverUserId)
            .PostAsJsonAsync($"/api/requisitions/{draft.Id}/decision", new ApprovalDecisionRequest { Approve = true });

        var createPosting = await recruiterClient.PostAsJsonAsync("/api/jobpostings", new CreateJobPostingRequest { RequisitionId = draft.Id });
        var posting = (await createPosting.Content.ReadFromJsonAsync<JobPostingDetailDto>())!;
        var publish = await recruiterClient.PostAsync($"/api/jobpostings/{posting.Id}/publish", null);
        var live = (await publish.Content.ReadFromJsonAsync<JobPostingDetailDto>())!;

        var anon = AnonymousClient();

        // First application with international phone format
        var app1 = await anon.PostAsJsonAsync($"/api/public/jobs/{live.PublicToken}/apply", new SubmitApplicationRequest
        {
            FullName = "Testing Deduplication",
            Email = "Dedupe.Candidate@Domain.Com",
            Phone = "+95 9 888 777 666"
        });
        app1.EnsureSuccessStatusCode();

        // Second application with local formatted phone and uppercase email
        var app2 = await anon.PostAsJsonAsync($"/api/public/jobs/{live.PublicToken}/apply", new SubmitApplicationRequest
        {
            FullName = "Testing Deduplication",
            Email = "dedupe.candidate@domain.com",
            Phone = "09888777666"
        });
        app2.EnsureSuccessStatusCode();

        var pipeline = await recruiterClient.GetFromJsonAsync<List<PipelineItemDto>>($"/api/jobpostings/{live.Id}/pipeline");
        Assert.NotNull(pipeline);
        Assert.Equal(2, pipeline.Count);
        Assert.Equal(pipeline[0].CandidateId, pipeline[1].CandidateId);
        Assert.Equal("dedupe.candidate@domain.com", pipeline[0].Email);
        Assert.Equal("09888777666", pipeline[0].Phone);
    }

    [Fact]
    public async Task Sequential_Approval_Logic_Enforces_Step_Order()
    {
        var hmClient = ClientFor(Roles.HiringManager, _factory.HiringManagerUserId);
        var createReq = await hmClient.PostAsJsonAsync("/api/requisitions", new CreateRequisitionRequest
        {
            DepartmentId = _factory.SalesDepartmentId,
            Title = "Sequential Approval Test Role",
            JobDescription = "Testing approval step order.",
            Headcount = 1
        });
        var draft = (await createReq.Content.ReadFromJsonAsync<RequisitionDetailDto>())!;
        await hmClient.PostAsync($"/api/requisitions/{draft.Id}/submit", null);

        var financeClient = ClientFor(Roles.Approver, _factory.FinanceApproverUserId);
        var hrClient = ClientFor(Roles.Admin, _factory.AdminUserId);

        // Reject out of order (Finance is step 2, HR is step 1). Approving out of order is
        // now permitted — a later step outranks an earlier one — but rejecting is not, since
        // that would end the requisition before HR ever saw it (ADR-0024). The refusal must
        // also leave the chain intact, which the in-order run below then depends on.
        var outOfOrder = await financeClient.PostAsJsonAsync(
            $"/api/requisitions/{draft.Id}/decision", new ApprovalDecisionRequest { Approve = false });
        Assert.Equal(HttpStatusCode.Conflict, outOfOrder.StatusCode);

        // Step 1 approves
        var step1 = await hrClient.PostAsJsonAsync(
            $"/api/requisitions/{draft.Id}/decision", new ApprovalDecisionRequest { Approve = true });
        step1.EnsureSuccessStatusCode();

        // Step 2 approves
        var step2 = await financeClient.PostAsJsonAsync(
            $"/api/requisitions/{draft.Id}/decision", new ApprovalDecisionRequest { Approve = true });
        step2.EnsureSuccessStatusCode();

        var finalState = (await step2.Content.ReadFromJsonAsync<RequisitionDetailDto>())!;
        Assert.Equal("Approved", finalState.Status);
    }
}
