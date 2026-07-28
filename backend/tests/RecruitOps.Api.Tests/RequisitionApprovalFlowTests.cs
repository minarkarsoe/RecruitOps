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

    [Fact]
    public async Task A_Later_Approver_Cannot_Jump_The_Queue()
    {
        var draft = await NewDraftAsync("Sales Analyst");
        await ClientFor(Roles.HiringManager, _factory.HiringManagerUserId)
            .PostAsync($"/api/requisitions/{draft.Id}/submit", null);

        // Finance is step 2; HR has not decided yet.
        var res = await ClientFor(Roles.Approver, _factory.FinanceApproverUserId)
            .PostAsJsonAsync($"/api/requisitions/{draft.Id}/decision",
                new ApprovalDecisionRequest { Approve = true });

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
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

        // Department access alone is not enough here: CanAccessAsync returns true for every
        // non-department-scoped role, Approver included. Without an ownership check an
        // approver could push a Draft into the chain and then decide on it themselves.
        var res = await ClientFor(Roles.Approver, _factory.FinanceApproverUserId)
            .PostAsync($"/api/requisitions/{draft.Id}/submit", null);

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Deciding_On_A_Requisition_That_Is_Not_Yours_Does_Not_Leak_Its_Status()
    {
        var draft = await NewDraftAsync("Sales Archivist");
        await ClientFor(Roles.HiringManager, _factory.HiringManagerUserId)
            .PostAsync($"/api/requisitions/{draft.Id}/submit", null);
        await ClientFor(Roles.Admin, _factory.AdminUserId)
            .PostAsJsonAsync($"/api/requisitions/{draft.Id}/decision", new ApprovalDecisionRequest { Approve = false });

        // Now Rejected. A stranger probing it must get the same 404 as for a GUID that does
        // not exist — a 409 naming the status would turn this endpoint into an oracle.
        var rejected = await ClientFor(Roles.Recruiter, Guid.NewGuid())
            .PostAsJsonAsync($"/api/requisitions/{draft.Id}/decision", new ApprovalDecisionRequest { Approve = true });
        var nonexistent = await ClientFor(Roles.Recruiter, Guid.NewGuid())
            .PostAsJsonAsync($"/api/requisitions/{Guid.NewGuid()}/decision", new ApprovalDecisionRequest { Approve = true });

        Assert.Equal(HttpStatusCode.NotFound, rejected.StatusCode);
        Assert.Equal(nonexistent.StatusCode, rejected.StatusCode);
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

        var res = await ClientFor(Roles.Approver, _factory.FinanceApproverUserId)
            .PutAsJsonAsync($"/api/requisitions/{draft.Id}", new UpdateRequisitionRequest
            {
                DepartmentId = _factory.SalesDepartmentId,
                Title = "Rewritten by someone else",
                JobDescription = "Not theirs to change.",
                Headcount = 1,
            });

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
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
        // requester (or a company-wide role). 404, not 403, per ADR-0003.
        var res = await ClientFor(Roles.Approver, _factory.FinanceApproverUserId)
            .PostAsync($"/api/requisitions/{draft.Id}/cancel", null);

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
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
    public async Task Inbox_Only_Shows_Requisitions_Waiting_On_You()
    {
        var draft = await NewDraftAsync("Sales Engineer");
        await ClientFor(Roles.HiringManager, _factory.HiringManagerUserId)
            .PostAsync($"/api/requisitions/{draft.Id}/submit", null);

        // Step 1 is HR (admin); Finance is step 2 and must not see it yet, otherwise
        // approvers would work the queue out of order.
        var adminInbox = await ClientFor(Roles.Admin, _factory.AdminUserId)
            .GetFromJsonAsync<List<RequisitionListItemDto>>("/api/requisitions/inbox");
        Assert.Contains(adminInbox!, r => r.Id == draft.Id);

        var financeInbox = await ClientFor(Roles.Approver, _factory.FinanceApproverUserId)
            .GetFromJsonAsync<List<RequisitionListItemDto>>("/api/requisitions/inbox");
        Assert.DoesNotContain(financeInbox!, r => r.Id == draft.Id);

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
