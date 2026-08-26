using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using RecruitOps.Application.DTOs;
using Xunit;

namespace RecruitOps.Api.Tests;

/// <summary>ADR-0018 — an Approver reaches requisitions, not candidates.
///
/// <para>These exist because the Module 3 security review found the opposite shipped. An
/// Approver is not department-scoped (ADR-0003, for a good reason about approval chains
/// crossing departments), and every candidate-facing service asked that one question — so
/// <c>CanAccessAsync</c> said yes for every department and an approver could read any
/// candidate's debrief, scorecards and stage history in the company. Nothing was broken;
/// each layer did what it said, and the total was a privilege nobody granted.</para>
///
/// <para>Every assertion below is <b>404, not 403</b>: out-of-reach and non-existent must be
/// indistinguishable, or the status code itself confirms the candidate exists.</para>
/// </summary>
public class ApproverReachTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;
    private readonly Module3Scenario _scenario;

    public ApproverReachTests(CustomWebAppFactory factory)
    {
        _factory = factory;
        _scenario = new Module3Scenario(factory);
    }

    [Fact]
    public async Task An_Approver_Cannot_Read_The_Note_Thread()
    {
        var (_, applicationId) = await _scenario.ApplicationAsync("Approver Reach A");
        await PostNoteAsync(applicationId, "Strong on the technical side.");

        var res = await _scenario.FinanceApprover()
            .GetAsync($"/api/applications/{applicationId}/notes");

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task An_Approver_Cannot_Post_To_The_Note_Thread()
    {
        var (_, applicationId) = await _scenario.ApplicationAsync("Approver Reach B");

        var res = await _scenario.FinanceApprover().PostAsJsonAsync(
            $"/api/applications/{applicationId}/notes",
            new CreateNoteRequest { Body = "What is the budget impact here?" });

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task An_Approver_Cannot_See_The_Panels_Scorecards()
    {
        var (_, applicationId) = await _scenario.ApplicationAsync("Approver Reach C");
        var interview = await _scenario.ScheduleAsync(applicationId);

        // The sharpest case: an approver is not a participant, so the blind rule does not
        // apply to them — before ADR-0018 they saw every submitted evaluation immediately.
        var res = await _scenario.FinanceApprover()
            .GetAsync($"/api/interviews/{interview.Id}/scorecards");

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task An_Approver_Cannot_Reach_The_Interview_Or_Its_Round_List()
    {
        var (_, applicationId) = await _scenario.ApplicationAsync("Approver Reach D");
        var interview = await _scenario.ScheduleAsync(applicationId);

        var single = await _scenario.FinanceApprover().GetAsync($"/api/interviews/{interview.Id}");
        var list = await _scenario.FinanceApprover()
            .GetAsync($"/api/applications/{applicationId}/interviews");

        Assert.Equal(HttpStatusCode.NotFound, single.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, list.StatusCode);
    }

    [Fact]
    public async Task An_Approver_Cannot_Reach_The_Pipeline_Or_The_Stage_History()
    {
        var (postingId, applicationId) = await _scenario.ApplicationAsync("Approver Reach E");

        // The same hole one module earlier: PipelineService guarded on CanAccessAsync alone.
        var pipeline = await _scenario.FinanceApprover()
            .GetAsync($"/api/jobpostings/{postingId}/pipeline");
        var history = await _scenario.FinanceApprover()
            .GetAsync($"/api/applications/{applicationId}/history");

        Assert.Equal(HttpStatusCode.NotFound, pipeline.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, history.StatusCode);
    }

    [Fact]
    public async Task An_Approver_Cannot_Bulk_Upload_CVs_Into_A_Posting()
    {
        // ⚠️ This shipped broken and was found by the 2026-08-26 security review. `POST
        // /api/jobpostings/{id}/resumes/bulk` as the Finance approver, against a Sales posting,
        // returned **200 OK** with a batch id — the third instance of the same mistake
        // (`CanAccessAsync` alone), in a class written after ADR-0018 was already documented.
        //
        // The write half is the serious one: creating Candidate and JobApplication rows in a
        // department the caller has no relationship with is not a read leak, it is data injected
        // into somebody else's pipeline by a role whose whole remit is signing off headcount.
        var (postingId, _) = await _scenario.ApplicationAsync("Approver Reach F");

        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes("%PDF-1.4 not a real CV"));
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(file, "files", "Candidate.pdf");

        var upload = await _scenario.FinanceApprover()
            .PostAsync($"/api/jobpostings/{postingId}/resumes/bulk", content);

        Assert.Equal(HttpStatusCode.NotFound, upload.StatusCode);
    }

    [Fact]
    public async Task An_Approver_Cannot_Read_A_Bulk_Upload_Batch()
    {
        // The read half of the same hole. The batch summary carries CV filenames — which are
        // very often the candidate's name — plus the candidate and application ids each file
        // produced, so it is a candidate list by another route.
        var (postingId, _) = await _scenario.ApplicationAsync("Approver Reach G");

        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes("%PDF-1.4 not a real CV"));
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(file, "files", "Candidate.pdf");

        var upload = await _scenario.Recruiter()
            .PostAsync($"/api/jobpostings/{postingId}/resumes/bulk", content);
        upload.EnsureSuccessStatusCode();
        var batch = (await upload.Content.ReadFromJsonAsync<BulkUploadBatchResponseDto>())!;

        var read = await _scenario.FinanceApprover()
            .GetAsync($"/api/jobpostings/{postingId}/resumes/bulk/{batch.BatchId}");

        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
    }

    [Fact]
    public async Task A_Panel_Seat_Is_The_Way_In_And_It_Reaches_Only_That_Application()
    {
        var (_, onPanel) = await _scenario.ApplicationAsync("Approver Reach F");
        var (_, other) = await _scenario.ApplicationAsync("Approver Reach G");

        await _scenario.ScheduleWithAsync(
            onPanel, _factory.HiringManagerUserId, _factory.FinanceApproverUserId);
        await _scenario.ScheduleAsync(other);

        await PostNoteAsync(onPanel, "Panel confirmed for Monday.");

        // Exclusion is not a permanent lock-out: participation is the deliberate, visible,
        // expiring grant (ADR-0017 §4), and it is exactly as narrow for an approver as it is
        // for a hiring manager from another department.
        var thread = await _scenario.FinanceApprover()
            .GetFromJsonAsync<List<NoteDto>>($"/api/applications/{onPanel}/notes");
        Assert.Single(thread!);

        var elsewhere = await _scenario.FinanceApprover()
            .GetAsync($"/api/applications/{other}/notes");
        Assert.Equal(HttpStatusCode.NotFound, elsewhere.StatusCode);
    }

    [Fact]
    public async Task A_Panel_Seat_Still_Does_Not_Let_An_Approver_Move_The_Process()
    {
        var (_, applicationId) = await _scenario.ApplicationAsync("Approver Reach H");
        var interview = await _scenario.ScheduleWithAsync(
            applicationId, _factory.HiringManagerUserId, _factory.FinanceApproverUserId);

        var res = await _scenario.FinanceApprover().PostAsJsonAsync(
            $"/api/interviews/{interview.Id}/cancel",
            new CancelInterviewRequest { Reason = "Budget on hold." });

        // 403 here, not 404: RecruitmentStaff stops this at the policy layer before the
        // service is reached. Both guards are wanted — see InterviewsController.
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task An_Approver_Still_Decides_On_A_Requisition_From_Another_Department()
    {
        // The day job, unchanged. ADR-0003's argument — an approval chain crosses departments,
        // so an approver must not be department-scoped — is why the fix is a second axis
        // rather than one more role added to IsDepartmentScoped.
        var create = await _scenario.SalesManager().PostAsJsonAsync(
            "/api/requisitions",
            new CreateRequisitionRequest
            {
                DepartmentId = _factory.SalesDepartmentId,
                Title = "Approver Reach I",
                JobDescription = "Internal JD.",
                Headcount = 1,
                SalaryBudget = 800_000m,
            });
        create.EnsureSuccessStatusCode();
        var draft = (await create.Content.ReadFromJsonAsync<RequisitionDetailDto>())!;

        await _scenario.SalesManager().PostAsync($"/api/requisitions/{draft.Id}/submit", null);
        await _scenario.Client(RecruitOps.Api.Auth.Roles.Admin, _factory.AdminUserId)
            .PostAsJsonAsync($"/api/requisitions/{draft.Id}/decision",
                new ApprovalDecisionRequest { Approve = true });

        var decision = await _scenario.FinanceApprover().PostAsJsonAsync(
            $"/api/requisitions/{draft.Id}/decision", new ApprovalDecisionRequest { Approve = true });

        Assert.True(
            decision.IsSuccessStatusCode,
            $"A Finance approver must still decide on a Sales requisition; got {decision.StatusCode}.");

        var read = await _scenario.FinanceApprover().GetAsync($"/api/requisitions/{draft.Id}");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
    }

    private async Task PostNoteAsync(Guid applicationId, string body)
    {
        var res = await _scenario.Recruiter().PostAsJsonAsync(
            $"/api/applications/{applicationId}/notes", new CreateNoteRequest { Body = body });
        res.EnsureSuccessStatusCode();
    }
}
