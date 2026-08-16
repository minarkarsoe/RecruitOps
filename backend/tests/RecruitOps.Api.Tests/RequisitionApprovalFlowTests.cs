using System.Net;
using System.Net.Http.Json;
using RecruitOps.Api.Auth;
using RecruitOps.Application.DTOs;
using Xunit;

namespace RecruitOps.Api.Tests;

/// <summary>Module 1.3 — the approval chain, end to end. The seeded company-wide chain
/// has two steps (HR then Finance) so sequencing is actually exercised rather than assumed.</summary>
public class RequisitionApprovalFlowTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;

    public RequisitionApprovalFlowTests(CustomWebAppFactory factory) => _factory = factory;

    private HttpClient ClientFor(string role, Guid? userId = null)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Tenant", _factory.TenantA.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Roles", role);
        if (userId is not null)
            client.DefaultRequestHeaders.Add("X-Test-UserId", userId.Value.ToString());
        return client;
    }

    /// <summary>Creates a fresh Draft so each test is independent.</summary>
    private async Task<RequisitionDetailDto> NewDraftAsync(string title)
    {
        var res = await ClientFor(Roles.HiringManager, _factory.HiringManagerUserId)
            .PostAsJsonAsync("/api/requisitions", new CreateRequisitionRequest
            {
                DepartmentId = _factory.SalesDepartmentId,
                Title = title,
                JobDescription = "Because we need one.",
                Headcount = 1,
            });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<RequisitionDetailDto>())!;
    }

    [Fact]
    public async Task Full_Chain_Approval_Moves_Through_Both_Steps()
    {
        var draft = await NewDraftAsync("Head of Sales");
        Assert.Equal("Draft", draft.Status);

        // Submit → snapshots the chain, now waiting on step 1.
        var submitRes = await ClientFor(Roles.HiringManager, _factory.HiringManagerUserId)
            .PostAsync($"/api/requisitions/{draft.Id}/submit", null);
        submitRes.EnsureSuccessStatusCode();
        var submitted = await submitRes.Content.ReadFromJsonAsync<RequisitionDetailDto>();
        Assert.Equal("PendingApproval", submitted!.Status);
        Assert.Equal("HR", submitted.AwaitingApprovalFrom);

        // The chain is snapshotted on submit, so the timeline exists immediately.
        Assert.Equal(2, submitted.Approvals.Count);
        Assert.All(submitted.Approvals, a => Assert.Equal("Waiting", a.Decision));

        // Step 1 (HR / admin) approves → still pending, now waiting on Finance.
        var step1 = await ClientFor(Roles.Admin, _factory.AdminUserId)
            .PostAsJsonAsync($"/api/requisitions/{draft.Id}/decision",
                new ApprovalDecisionRequest { Approve = true, Comment = "Headcount agreed." });
        step1.EnsureSuccessStatusCode();
        var afterStep1 = await step1.Content.ReadFromJsonAsync<RequisitionDetailDto>();
        Assert.Equal("PendingApproval", afterStep1!.Status);
        Assert.Equal("Finance", afterStep1.AwaitingApprovalFrom);
        Assert.Equal("Approved", afterStep1.Approvals.Single(a => a.Sequence == 1).Decision);
        Assert.Equal("Headcount agreed.", afterStep1.Approvals.Single(a => a.Sequence == 1).Comment);

        // Step 2 (Finance) approves → fully approved.
        var step2 = await ClientFor(Roles.Approver, _factory.FinanceApproverUserId)
            .PostAsJsonAsync($"/api/requisitions/{draft.Id}/decision",
                new ApprovalDecisionRequest { Approve = true, Comment = "Budget available." });
        step2.EnsureSuccessStatusCode();
        var approved = await step2.Content.ReadFromJsonAsync<RequisitionDetailDto>();
        Assert.Equal("Approved", approved!.Status);
        Assert.Null(approved.AwaitingApprovalFrom);
        Assert.NotNull(approved.DecidedAt);
    }

    [Fact]
    public async Task Rejection_At_First_Step_Rejects_The_Requisition()
    {
        var draft = await NewDraftAsync("Sales Coordinator");
        await ClientFor(Roles.HiringManager, _factory.HiringManagerUserId)
            .PostAsync($"/api/requisitions/{draft.Id}/submit", null);

        var res = await ClientFor(Roles.Admin, _factory.AdminUserId)
            .PostAsJsonAsync($"/api/requisitions/{draft.Id}/decision",
                new ApprovalDecisionRequest { Approve = false, Comment = "Not this quarter." });
        res.EnsureSuccessStatusCode();

        var rejected = await res.Content.ReadFromJsonAsync<RequisitionDetailDto>();
        Assert.Equal("Rejected", rejected!.Status);

        // Step 2 is never reached, so it must stay Waiting rather than being auto-decided.
        Assert.Equal("Rejected", rejected.Approvals.Single(a => a.Sequence == 1).Decision);
        Assert.Equal("Waiting", rejected.Approvals.Single(a => a.Sequence == 2).Decision);
    }

    // This test used to assert the opposite — that a step-2 approver got 404 while step 1 was
    // undecided. ADR-0024 reverses that deliberately: a later step outranks an earlier one, so
    // Finance may close HR's step as well as its own. It is re-scoped rather than deleted,
    // because the pair below is what proves the *limits* of the new rule still hold.
    [Fact]
    public async Task A_Later_Approver_Can_Approve_Forward_And_The_Record_Names_Them()
    {
        var draft = await NewDraftAsync("Sales Analyst");
        await ClientFor(Roles.HiringManager, _factory.HiringManagerUserId)
            .PostAsync($"/api/requisitions/{draft.Id}/submit", null);

        // Finance is step 2; HR (step 1) has not decided yet.
        var res = await ClientFor(Roles.Approver, _factory.FinanceApproverUserId)
            .PostAsJsonAsync($"/api/requisitions/{draft.Id}/decision",
                new ApprovalDecisionRequest { Approve = true });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var detail = await res.Content.ReadFromJsonAsync<RequisitionDetailDto>();

        // Both steps close in the one action, and the requisition is fully approved.
        Assert.All(detail!.Approvals, a => Assert.Equal("Approved", a.Decision));
        Assert.Equal("Approved", detail.Status);

        // The audit requirement, in the product owner's words: "it must show the record of
        // what I did." HR's step must name Finance as the actual decider while still showing
        // HR as the assignee — overwriting ApproverUserId would erase who the step was for.
        var hrStep = detail.Approvals.Single(a => a.Sequence == 1);
        Assert.Equal(_factory.FinanceApproverUserId, hrStep.DecidedByUserId);
        Assert.NotEqual(_factory.FinanceApproverUserId, hrStep.ApproverUserId);

        // Finance's own step is not "on behalf of" anyone, so it stays null.
        var financeStep = detail.Approvals.Single(a => a.Sequence == 2);
        Assert.Null(financeStep.DecidedByUserId);
    }

    [Fact]
    public async Task A_Later_Approver_Cannot_Reject_On_A_Junior_Steps_Behalf()
    {
        var draft = await NewDraftAsync("Sales Coordinator");
        await ClientFor(Roles.HiringManager, _factory.HiringManagerUserId)
            .PostAsync($"/api/requisitions/{draft.Id}/submit", null);

        // Approving forward is allowed; rejecting forward is not, and the two are not
        // symmetric (ADR-0024). Approving removes HR's step but not their say — the
        // requisition proceeds either way. Rejecting would END it before HR ever saw it.
        var res = await ClientFor(Roles.Approver, _factory.FinanceApproverUserId)
            .PostAsJsonAsync($"/api/requisitions/{draft.Id}/decision",
                new ApprovalDecisionRequest { Approve = false, Comment = "Not this quarter." });

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);

        // And nothing was written: both steps must still be Waiting, and the requisition
        // must still be PendingApproval. A refused action that half-applied would be worse
        // than one that was allowed.
        var after = await ClientFor(Roles.Admin, _factory.AdminUserId)
            .GetFromJsonAsync<RequisitionDetailDto>($"/api/requisitions/{draft.Id}");
        Assert.Equal("PendingApproval", after!.Status);
        Assert.All(after.Approvals, a => Assert.Equal("Waiting", a.Decision));
    }

    [Fact]
    public async Task An_Earlier_Approver_Still_Cannot_Approve_A_Later_Step()
    {
        var draft = await NewDraftAsync("Sales Associate");
        await ClientFor(Roles.HiringManager, _factory.HiringManagerUserId)
            .PostAsync($"/api/requisitions/{draft.Id}/submit", null);

        // The rule reaches DOWN the chain only. HR (step 1) approving must not close
        // Finance (step 2) — otherwise "senior may skip ahead" would collapse into
        // "anyone may approve everything", which is the opposite of the intent.
        var res = await ClientFor(Roles.Admin, _factory.AdminUserId)
            .PostAsJsonAsync($"/api/requisitions/{draft.Id}/decision",
                new ApprovalDecisionRequest { Approve = true });
        res.EnsureSuccessStatusCode();

        var detail = await res.Content.ReadFromJsonAsync<RequisitionDetailDto>();
        Assert.Equal("PendingApproval", detail!.Status);
        Assert.Equal("Approved", detail.Approvals.Single(a => a.Sequence == 1).Decision);
        Assert.Equal("Waiting", detail.Approvals.Single(a => a.Sequence == 2).Decision);
    }

    [Fact]
    public async Task Submitting_Twice_Is_A_Conflict()
    {
        var draft = await NewDraftAsync("Sales Intern");
        var first = await ClientFor(Roles.HiringManager, _factory.HiringManagerUserId)
            .PostAsync($"/api/requisitions/{draft.Id}/submit", null);
        first.EnsureSuccessStatusCode();

        var second = await ClientFor(Roles.HiringManager, _factory.HiringManagerUserId)
            .PostAsync($"/api/requisitions/{draft.Id}/submit", null);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task An_Approver_Cannot_Submit_Someone_Elses_Draft()
    {
        var draft = await NewDraftAsync("Sales Coordinator II");

        // Pre-ADR-0022 this reached RequisitionService and was stopped by IsOwnerOrCompanyWide
        // (404): department access alone was not enough, since CanAccessAsync returns true
        // for every non-department-scoped role, Approver included, and without an ownership
        // check an approver could push a Draft into the chain and then decide on it themselves.
        // Post-ADR-0022 the Approver role (read, approve only) never reaches the service at
        // all — /submit is gated on requisitions:update, which Approver does not hold — so the
        // request is stopped at the policy layer instead. The underlying guarantee (an approver
        // cannot submit someone else's draft) still holds; it is enforced earlier and for every
        // requisition, not just this one.
        var res = await ClientFor(Roles.Approver, _factory.FinanceApproverUserId)
            .PostAsync($"/api/requisitions/{draft.Id}/submit", null);

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Deciding_On_A_Requisition_That_Is_Not_Yours_Does_Not_Leak_Its_Status()
    {
        var draft = await NewDraftAsync("Sales Archivist");
        await ClientFor(Roles.HiringManager, _factory.HiringManagerUserId)
            .PostAsync($"/api/requisitions/{draft.Id}/submit", null);
        await ClientFor(Roles.Admin, _factory.AdminUserId)
            .PostAsJsonAsync($"/api/requisitions/{draft.Id}/decision", new ApprovalDecisionRequest { Approve = false });

        // Now Rejected. A stranger probing it must not be able to tell it apart from a GUID
        // that does not exist — the no-oracle guarantee ADR-0003 exists for.
        //
        // Post-ADR-0022, Recruiter (read only, no approve) is stopped at the policy layer
        // before either request reaches RequisitionService, so both come back 403 rather than
        // the pre-ADR-0022 404 — the identical-response invariant this test asserts still
        // holds, just at a different, and now blanket rather than per-resource, status code.
        var rejected = await ClientFor(Roles.Recruiter, Guid.NewGuid())
            .PostAsJsonAsync($"/api/requisitions/{draft.Id}/decision", new ApprovalDecisionRequest { Approve = true });
        var nonexistent = await ClientFor(Roles.Recruiter, Guid.NewGuid())
            .PostAsJsonAsync($"/api/requisitions/{Guid.NewGuid()}/decision", new ApprovalDecisionRequest { Approve = true });

        Assert.Equal(HttpStatusCode.Forbidden, rejected.StatusCode);
        Assert.Equal(nonexistent.StatusCode, rejected.StatusCode);
    }

    [Fact]
    public async Task An_Approver_Who_Holds_The_Permission_Still_Learns_Nothing_From_A_Requisition_Not_Theirs()
    {
        // Gap raised by the security review of ADR-0024. The test above probes with a caller
        // who lacks `approve` and is therefore stopped at the policy layer with a blanket 403
        // — it never reaches DecideAsync, so it cannot exercise the guard ordering that the
        // no-oracle rule actually depends on. This one uses a caller who DOES hold `approve`,
        // reaches the service, and must still be told nothing.
        //
        // That ordering is what ADR-0024 had to preserve: the status guard throws a 409 that
        // names the status, so it must never run before the caller has proven they hold a
        // Waiting step here.
        var draft = await NewDraftAsync("Sales Archivist II");
        await ClientFor(Roles.HiringManager, _factory.HiringManagerUserId)
            .PostAsync($"/api/requisitions/{draft.Id}/submit", null);

        // Finance closes both steps, so the requisition is Approved and Finance no longer
        // holds a Waiting step on it — the "exists, but nothing here is yours" case.
        (await ClientFor(Roles.Approver, _factory.FinanceApproverUserId)
            .PostAsJsonAsync($"/api/requisitions/{draft.Id}/decision",
                new ApprovalDecisionRequest { Approve = true })).EnsureSuccessStatusCode();

        var settled = await ClientFor(Roles.Approver, _factory.FinanceApproverUserId)
            .PostAsJsonAsync($"/api/requisitions/{draft.Id}/decision",
                new ApprovalDecisionRequest { Approve = true });
        var nonexistent = await ClientFor(Roles.Approver, _factory.FinanceApproverUserId)
            .PostAsJsonAsync($"/api/requisitions/{Guid.NewGuid()}/decision",
                new ApprovalDecisionRequest { Approve = true });

        // 404, not the 409 that would name the status. If the status guard were ever hoisted
        // above the `mine is null` check, this becomes 409 and the two responses diverge.
        Assert.Equal(HttpStatusCode.NotFound, settled.StatusCode);
        Assert.Equal(nonexistent.StatusCode, settled.StatusCode);

        // The bodies are compared with `traceId` stripped: it is per-request and says nothing
        // about the requisition, so requiring byte-equality would assert a property the
        // framework does not hold rather than the one that matters.
        static string WithoutTraceId(string body) =>
            System.Text.RegularExpressions.Regex.Replace(body, "\"traceId\":\"[^\"]*\"", "");

        Assert.Equal(
            WithoutTraceId(await nonexistent.Content.ReadAsStringAsync()),
            WithoutTraceId(await settled.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task Requester_Can_Edit_A_Draft()
    {
        var draft = await NewDraftAsync("Sales Assocate"); // typo is the point

        var res = await ClientFor(Roles.HiringManager, _factory.HiringManagerUserId)
            .PutAsJsonAsync($"/api/requisitions/{draft.Id}", new UpdateRequisitionRequest
            {
                DepartmentId = _factory.SalesDepartmentId,
                Title = "Sales Associate",
                JobDescription = "Sell things, correctly spelled.",
                Headcount = 4,
                SalaryBudget = 1_500_000m,
            });
        res.EnsureSuccessStatusCode();

        var updated = await res.Content.ReadFromJsonAsync<RequisitionDetailDto>();
        Assert.Equal("Sales Associate", updated!.Title);
        Assert.Equal(4, updated.Headcount);
        Assert.Equal(1_500_000m, updated.SalaryBudget);
        Assert.Equal("Draft", updated.Status); // editing must not advance the workflow
    }

    [Fact]
    public async Task A_Submitted_Requisition_Cannot_Be_Edited()
    {
        var draft = await NewDraftAsync("Sales Planner");
        await ClientFor(Roles.HiringManager, _factory.HiringManagerUserId)
            .PostAsync($"/api/requisitions/{draft.Id}/submit", null);

        // Approvers are deciding on these contents; letting them change underneath would
        // make every recorded decision refer to a document that no longer exists.
        var res = await ClientFor(Roles.HiringManager, _factory.HiringManagerUserId)
            .PutAsJsonAsync($"/api/requisitions/{draft.Id}", new UpdateRequisitionRequest
            {
                DepartmentId = _factory.SalesDepartmentId,
                Title = "Sales Planner (10x the headcount)",
                JobDescription = "Sneaky.",
                Headcount = 50,
            });

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task A_Draft_Cannot_Be_Moved_Into_A_Department_You_Cannot_See()
    {
        var draft = await NewDraftAsync("Sales Admin");

        // The hiring manager owns Sales only. Moving it to Finance would push the
        // requisition somewhere they cannot see it — 404, not 403 (ADR-0003).
        var res = await ClientFor(Roles.HiringManager, _factory.HiringManagerUserId)
            .PutAsJsonAsync($"/api/requisitions/{draft.Id}", new UpdateRequisitionRequest
            {
                DepartmentId = _factory.FinanceDepartmentId,
                Title = "Sales Admin",
                JobDescription = "Elsewhere.",
                Headcount = 1,
            });

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task An_Approver_Cannot_Edit_Someone_Elses_Draft()
    {
        var draft = await NewDraftAsync("Sales Clerk");

        // Same shift as An_Approver_Cannot_Submit_Someone_Elses_Draft: PUT is gated on
        // requisitions:update (ADR-0022), which Approver does not hold, so this is now
        // stopped at the policy layer (403) rather than IsOwnerOrCompanyWide inside the
        // service (404).
        var res = await ClientFor(Roles.Approver, _factory.FinanceApproverUserId)
            .PutAsJsonAsync($"/api/requisitions/{draft.Id}", new UpdateRequisitionRequest
            {
                DepartmentId = _factory.SalesDepartmentId,
                Title = "Rewritten by someone else",
                JobDescription = "Not theirs to change.",
                Headcount = 1,
            });

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Requester_Can_Cancel_A_Draft()
    {
        var draft = await NewDraftAsync("Sales Trainee");

        var res = await ClientFor(Roles.HiringManager, _factory.HiringManagerUserId)
            .PostAsync($"/api/requisitions/{draft.Id}/cancel", null);
        res.EnsureSuccessStatusCode();

        var cancelled = await res.Content.ReadFromJsonAsync<RequisitionDetailDto>();
        Assert.Equal("Cancelled", cancelled!.Status);
        Assert.NotNull(cancelled.DecidedAt);
    }

    [Fact]
    public async Task Cancelling_Mid_Approval_Keeps_The_Trail_And_Clears_The_Inbox()
    {
        var draft = await NewDraftAsync("Sales Ops Lead");
        await ClientFor(Roles.HiringManager, _factory.HiringManagerUserId)
            .PostAsync($"/api/requisitions/{draft.Id}/submit", null);

        // It is HR's (admin's) turn, so it should be in their inbox first.
        var inboxBefore = await ClientFor(Roles.Admin, _factory.AdminUserId)
            .GetFromJsonAsync<List<RequisitionListItemDto>>("/api/requisitions/inbox");
        Assert.Contains(inboxBefore!, r => r.Id == draft.Id);

        var res = await ClientFor(Roles.HiringManager, _factory.HiringManagerUserId)
            .PostAsync($"/api/requisitions/{draft.Id}/cancel", null);
        res.EnsureSuccessStatusCode();

        var cancelled = await res.Content.ReadFromJsonAsync<RequisitionDetailDto>();
        Assert.Equal("Cancelled", cancelled!.Status);
        // The steps are deliberately left Waiting — "cancelled while waiting on HR" is a fact.
        Assert.All(cancelled.Approvals, a => Assert.Equal("Waiting", a.Decision));

        // ...but a cancelled requisition must not linger in anyone's queue.
        var inboxAfter = await ClientFor(Roles.Admin, _factory.AdminUserId)
            .GetFromJsonAsync<List<RequisitionListItemDto>>("/api/requisitions/inbox");
        Assert.DoesNotContain(inboxAfter!, r => r.Id == draft.Id);
    }

    [Fact]
    public async Task An_Approver_Cannot_Cancel_Someone_Elses_Requisition()
    {
        var draft = await NewDraftAsync("Sales Support");
        await ClientFor(Roles.HiringManager, _factory.HiringManagerUserId)
            .PostAsync($"/api/requisitions/{draft.Id}/submit", null);

        // Being asked to approve is not authority to withdraw — that belongs to the
        // requester (or a company-wide role). Pre-ADR-0022 this was 404, not 403, because
        // IsOwnerOrCompanyWide inside the service made the call. Post-ADR-0022, /cancel is
        // gated on requisitions:update (rationale: cancel is a withdrawal by the requester,
        // same authority as edit), which Approver does not hold, so the policy layer now
        // stops it first — 403, before the service's ownership rule is ever reached.
        var res = await ClientFor(Roles.Approver, _factory.FinanceApproverUserId)
            .PostAsync($"/api/requisitions/{draft.Id}/cancel", null);

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task An_Approved_Requisition_Cannot_Be_Cancelled()
    {
        var draft = await NewDraftAsync("Sales Director");
        await ClientFor(Roles.HiringManager, _factory.HiringManagerUserId)
            .PostAsync($"/api/requisitions/{draft.Id}/submit", null);
        await ClientFor(Roles.Admin, _factory.AdminUserId)
            .PostAsJsonAsync($"/api/requisitions/{draft.Id}/decision", new ApprovalDecisionRequest { Approve = true });
        await ClientFor(Roles.Approver, _factory.FinanceApproverUserId)
            .PostAsJsonAsync($"/api/requisitions/{draft.Id}/decision", new ApprovalDecisionRequest { Approve = true });

        // Reopening a decided requisition would rewrite history.
        var res = await ClientFor(Roles.HiringManager, _factory.HiringManagerUserId)
            .PostAsync($"/api/requisitions/{draft.Id}/cancel", null);

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task Inbox_Shows_A_Senior_A_Junior_Step_Marked_As_Not_Their_Turn()
    {
        var draft = await NewDraftAsync("Sales Engineer");
        await ClientFor(Roles.HiringManager, _factory.HiringManagerUserId)
            .PostAsync($"/api/requisitions/{draft.Id}/submit", null);

        // Step 1 is HR (admin); Finance is step 2. This test previously asserted Finance must
        // NOT see it. Under ADR-0024 Finance may approve ahead of HR, so hiding it would make
        // the feature undiscoverable — a senior would have to already know the requisition
        // existed. It is surfaced and *marked* instead.
        var adminInbox = await ClientFor(Roles.Admin, _factory.AdminUserId)
            .GetFromJsonAsync<List<RequisitionListItemDto>>("/api/requisitions/inbox");
        var adminRow = Assert.Single(adminInbox!, r => r.Id == draft.Id);
        // It IS admin's turn, so the two labels agree — this is what the UI keys "Your turn" off.
        Assert.Equal(adminRow.AwaitingApprovalFrom, adminRow.YourStepLabel);

        var financeInbox = await ClientFor(Roles.Approver, _factory.FinanceApproverUserId)
            .GetFromJsonAsync<List<RequisitionListItemDto>>("/api/requisitions/inbox");
        var financeRow = Assert.Single(financeInbox!, r => r.Id == draft.Id);
        // It is NOT Finance's turn, and the row has to say so rather than labelling Finance's
        // step with HR's name — the whole reason YourStepLabel exists alongside it.
        Assert.Equal("Finance", financeRow.YourStepLabel);
        Assert.Equal("HR", financeRow.AwaitingApprovalFrom);
        Assert.NotEqual(financeRow.AwaitingApprovalFrom, financeRow.YourStepLabel);

        // After HR approves, the hand-off is the other way round.
        await ClientFor(Roles.Admin, _factory.AdminUserId)
            .PostAsJsonAsync($"/api/requisitions/{draft.Id}/decision", new ApprovalDecisionRequest { Approve = true });

        var adminAfter = await ClientFor(Roles.Admin, _factory.AdminUserId)
            .GetFromJsonAsync<List<RequisitionListItemDto>>("/api/requisitions/inbox");
        Assert.DoesNotContain(adminAfter!, r => r.Id == draft.Id);

        var financeAfter = await ClientFor(Roles.Approver, _factory.FinanceApproverUserId)
            .GetFromJsonAsync<List<RequisitionListItemDto>>("/api/requisitions/inbox");
        Assert.Contains(financeAfter!, r => r.Id == draft.Id);
    }

    [Fact]
    public async Task Admin_Can_Create_A_Chain_But_Recruiter_Cannot()
    {
        var request = new CreateApprovalChainRequest
        {
            Name = "Finance department chain",
            DepartmentId = _factory.FinanceDepartmentId, // scoped to Finance so it can't affect the Sales submits above
            Steps = new[]
            {
                new CreateApprovalChainStepRequest { ApproverUserId = _factory.AdminUserId, Label = "HR" },
            },
        };

        var asAdmin = await ClientFor(Roles.Admin, _factory.AdminUserId)
            .PostAsJsonAsync("/api/approvalchains", request);
        Assert.Equal(HttpStatusCode.Created, asAdmin.StatusCode);

        var created = await asAdmin.Content.ReadFromJsonAsync<ApprovalChainDto>();
        Assert.Single(created!.Steps);
        Assert.Equal(1, created.Steps[0].Sequence); // sequence derived from list order

        // Editing a chain is equivalent to being able to approve — recruiters must not.
        var asRecruiter = await ClientFor(Roles.Recruiter).PostAsJsonAsync("/api/approvalchains", request);
        Assert.Equal(HttpStatusCode.Forbidden, asRecruiter.StatusCode);
    }

    [Fact]
    public async Task Chain_With_An_Unknown_Approver_Is_Rejected()
    {
        var res = await ClientFor(Roles.Admin, _factory.AdminUserId)
            .PostAsJsonAsync("/api/approvalchains", new CreateApprovalChainRequest
            {
                Name = "Broken chain",
                Steps = new[]
                {
                    new CreateApprovalChainStepRequest { ApproverUserId = Guid.NewGuid(), Label = "Ghost" },
                },
            });

        // Otherwise a requisition could be submitted into a chain nobody can action.
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }
}
