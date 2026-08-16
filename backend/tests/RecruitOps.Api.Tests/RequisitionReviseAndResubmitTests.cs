using System.Net;
using System.Net.Http.Json;
using RecruitOps.Api.Auth;
using RecruitOps.Application.DTOs;
using Xunit;

namespace RecruitOps.Api.Tests;

/// <summary>ADR-0023 — a rejected requisition can be revised and resubmitted, and each
/// submission is a round. Before this, `Rejected` was terminal and enforced by two `if`
/// statements with no test behind either of them.</summary>
public class RequisitionReviseAndResubmitTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;

    public RequisitionReviseAndResubmitTests(CustomWebAppFactory factory) => _factory = factory;

    private HttpClient ClientFor(string role, Guid? userId = null)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Tenant", _factory.TenantA.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Roles", role);
        if (userId is not null)
            client.DefaultRequestHeaders.Add("X-Test-UserId", userId.Value.ToString());
        return client;
    }

    private HttpClient Requester => ClientFor(Roles.HiringManager, _factory.HiringManagerUserId);
    private HttpClient Hr => ClientFor(Roles.Admin, _factory.AdminUserId);

    private async Task<RequisitionDetailDto> NewDraftAsync(string title)
    {
        var res = await Requester.PostAsJsonAsync("/api/requisitions", new CreateRequisitionRequest
        {
            DepartmentId = _factory.SalesDepartmentId,
            Title = title,
            JobDescription = "Because we need one.",
            Headcount = 1,
        });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<RequisitionDetailDto>())!;
    }

    /// <summary>Draft → submit → HR rejects at step 1.</summary>
    private async Task<RequisitionDetailDto> RejectedRequisitionAsync(string title, string reason)
    {
        var draft = await NewDraftAsync(title);
        (await Requester.PostAsync($"/api/requisitions/{draft.Id}/submit", null))
            .EnsureSuccessStatusCode();

        var res = await Hr.PostAsJsonAsync($"/api/requisitions/{draft.Id}/decision",
            new ApprovalDecisionRequest { Approve = false, Comment = reason });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<RequisitionDetailDto>())!;
    }

    [Fact]
    public async Task Reject_Revise_Resubmit_Opens_Round_Two_And_Keeps_Round_One_Readable()
    {
        const string reason = "Headcount of 1 is not justified against the current plan.";
        var rejected = await RejectedRequisitionAsync("Sales Ops Lead", reason);
        Assert.Equal("Rejected", rejected.Status);

        // Back to Draft — no new status value, because every rule a revision needs is one
        // Draft already carries (ADR-0023).
        var reviseRes = await Requester.PostAsync($"/api/requisitions/{rejected.Id}/revise", null);
        reviseRes.EnsureSuccessStatusCode();
        var revising = (await reviseRes.Content.ReadFromJsonAsync<RequisitionDetailDto>())!;
        Assert.Equal("Draft", revising.Status);

        // A Draft is editable, so the correction the rejection asked for can actually be made.
        var editRes = await Requester.PutAsJsonAsync($"/api/requisitions/{rejected.Id}",
            new UpdateRequisitionRequest
            {
                DepartmentId = _factory.SalesDepartmentId,
                Title = "Sales Ops Lead",
                JobDescription = "Now with the headcount justification attached.",
                Headcount = 1,
            });
        editRes.EnsureSuccessStatusCode();

        var resubmitRes = await Requester.PostAsync($"/api/requisitions/{rejected.Id}/submit", null);
        resubmitRes.EnsureSuccessStatusCode();
        var round2 = (await resubmitRes.Content.ReadFromJsonAsync<RequisitionDetailDto>())!;

        Assert.Equal("PendingApproval", round2.Status);

        // Round 1's decision timestamp must not survive onto a requisition that is pending
        // again — every screen rendering "Decided" would otherwise state something false.
        Assert.Null(round2.DecidedAt);

        // Round 1 survives verbatim. This is the whole point: the reviewer's reasoning is the
        // most useful sentence on the record, and resetting the rows would erase it.
        var roundOne = round2.Approvals.Where(a => a.Round == 1).ToList();
        Assert.Equal("Rejected", roundOne.Single(a => a.Sequence == 1).Decision);
        Assert.Equal(reason, roundOne.Single(a => a.Sequence == 1).Comment);

        // Round 2 is decided afresh from step 1 — an earlier Approved was granted to a
        // different document, so nothing carries forward.
        var roundTwo = round2.Approvals.Where(a => a.Round == 2).ToList();
        Assert.Equal(roundOne.Count, roundTwo.Count);
        Assert.All(roundTwo, a => Assert.Equal("Waiting", a.Decision));

        // And the chain is waiting on round 2's step 1, not on a leftover from round 1.
        Assert.Equal(roundTwo.OrderBy(a => a.Sequence).First().Label, round2.AwaitingApprovalFrom);
    }

    [Fact]
    public async Task A_Second_Round_Can_Be_Approved_All_The_Way_Through()
    {
        // The quietest possible breakage, and the reason round scoping is not optional: the
        // completion check used to ask whether EVERY approval row was Approved. With a
        // rejected round preserved that can never be true, so a fully-approved round 2 would
        // sit in PendingApproval with no Waiting step — invisible in every inbox, no error.
        var rejected = await RejectedRequisitionAsync("Sales Ops Analyst", "Try again later.");
        (await Requester.PostAsync($"/api/requisitions/{rejected.Id}/revise", null))
            .EnsureSuccessStatusCode();
        (await Requester.PostAsync($"/api/requisitions/{rejected.Id}/submit", null))
            .EnsureSuccessStatusCode();

        // Finance is step 2 and may close both steps in one action (ADR-0024).
        var res = await ClientFor(Roles.Approver, _factory.FinanceApproverUserId)
            .PostAsJsonAsync($"/api/requisitions/{rejected.Id}/decision",
                new ApprovalDecisionRequest { Approve = true });
        res.EnsureSuccessStatusCode();

        var final = (await res.Content.ReadFromJsonAsync<RequisitionDetailDto>())!;
        Assert.Equal("Approved", final.Status);
        Assert.All(final.Approvals.Where(a => a.Round == 2), a => Assert.Equal("Approved", a.Decision));
        // Round 1's rejection is untouched by round 2 succeeding.
        Assert.Equal("Rejected", final.Approvals.Single(a => a.Round == 1 && a.Sequence == 1).Decision);
    }

    [Fact]
    public async Task A_Stale_Round_Does_Not_Put_The_Requisition_Back_In_An_Inbox()
    {
        // Rejecting at step 1 leaves step 2 Waiting forever — deliberately, so the record
        // reads "rejected while Finance had not yet been reached". Those dead rows must not
        // resurface as work once the requisition has moved on.
        var rejected = await RejectedRequisitionAsync("Sales Ops Coordinator", "No.");
        var finance = ClientFor(Roles.Approver, _factory.FinanceApproverUserId);

        // Rejected, so it is nobody's work.
        var whileRejected = await finance
            .GetFromJsonAsync<List<RequisitionListItemDto>>("/api/requisitions/inbox");
        Assert.DoesNotContain(whileRejected!, r => r.Id == rejected.Id);

        // Draft, so still nobody's work — the round-1 Waiting row for Finance is still there.
        (await Requester.PostAsync($"/api/requisitions/{rejected.Id}/revise", null))
            .EnsureSuccessStatusCode();
        var whileDraft = await finance
            .GetFromJsonAsync<List<RequisitionListItemDto>>("/api/requisitions/inbox");
        Assert.DoesNotContain(whileDraft!, r => r.Id == rejected.Id);

        // Resubmitted: it reappears, and the row must describe ROUND 2 — "HR", the live step —
        // not round 1's leftover. Getting this wrong is silent, which is why it is asserted.
        (await Requester.PostAsync($"/api/requisitions/{rejected.Id}/submit", null))
            .EnsureSuccessStatusCode();
        var afterResubmit = await finance
            .GetFromJsonAsync<List<RequisitionListItemDto>>("/api/requisitions/inbox");
        var row = Assert.Single(afterResubmit!, r => r.Id == rejected.Id);
        Assert.Equal("HR", row.AwaitingApprovalFrom);
        Assert.Equal("Finance", row.YourStepLabel);
    }

    [Fact]
    public async Task An_Approver_Dropped_From_The_Chain_Between_Rounds_Loses_The_Inbox_Item()
    {
        // The case the inbox's round filter actually exists for, and the only one that
        // distinguishes it: an approver who held a step in round 1, was removed from the
        // chain before round 2, and therefore has a Waiting row that is pure history while
        // the requisition is live again. Filtering on "has a Waiting step" alone would hand
        // them work that is no longer theirs.
        //
        // Found by mutation: with the round predicate deleted the whole suite stayed green,
        // because every other test's chain is identical across rounds. This test is what
        // makes that mutation fail.
        var admin = Hr;

        // Raised in Finance so the department-specific chain created below cannot disturb
        // the Sales-based tests in this class.
        var createRes = await admin.PostAsJsonAsync("/api/requisitions", new CreateRequisitionRequest
        {
            DepartmentId = _factory.FinanceDepartmentId,
            Title = "Chain Changed Between Rounds",
            JobDescription = "Because we need one.",
            Headcount = 1,
        });
        createRes.EnsureSuccessStatusCode();
        var req = (await createRes.Content.ReadFromJsonAsync<RequisitionDetailDto>())!;

        // Round 1 runs on the company-wide default: HR then Finance.
        (await admin.PostAsync($"/api/requisitions/{req.Id}/submit", null)).EnsureSuccessStatusCode();
        (await admin.PostAsJsonAsync($"/api/requisitions/{req.Id}/decision",
            new ApprovalDecisionRequest { Approve = false, Comment = "Rework this." }))
            .EnsureSuccessStatusCode();

        // The chain is re-resolved at submit time, so shortening it now applies to round 2
        // only — round 1 keeps the steps that were actually decided.
        (await admin.PostAsJsonAsync("/api/approvalchains", new CreateApprovalChainRequest
        {
            Name = "Finance dept — HR only",
            DepartmentId = _factory.FinanceDepartmentId,
            Steps = new[]
            {
                new CreateApprovalChainStepRequest { ApproverUserId = _factory.AdminUserId, Label = "HR" },
            },
        })).EnsureSuccessStatusCode();

        (await admin.PostAsync($"/api/requisitions/{req.Id}/revise", null)).EnsureSuccessStatusCode();
        var resubmit = await admin.PostAsync($"/api/requisitions/{req.Id}/submit", null);
        resubmit.EnsureSuccessStatusCode();
        var round2 = (await resubmit.Content.ReadFromJsonAsync<RequisitionDetailDto>())!;

        // Round 2 has no Finance step at all...
        Assert.Equal("PendingApproval", round2.Status);
        Assert.DoesNotContain(round2.Approvals.Where(a => a.Round == 2), a => a.Label == "Finance");

        // ...but Finance's round-1 row is still Waiting, exactly as the audit trail requires.
        Assert.Equal("Waiting",
            round2.Approvals.Single(a => a.Round == 1 && a.Label == "Finance").Decision);

        // So the live requisition must NOT appear in Finance's inbox.
        var financeInbox = await ClientFor(Roles.Approver, _factory.FinanceApproverUserId)
            .GetFromJsonAsync<List<RequisitionListItemDto>>("/api/requisitions/inbox");
        Assert.DoesNotContain(financeInbox!, r => r.Id == req.Id);

        // And it does appear for HR, who genuinely holds round 2's step.
        var hrInbox = await admin.GetFromJsonAsync<List<RequisitionListItemDto>>("/api/requisitions/inbox");
        Assert.Contains(hrInbox!, r => r.Id == req.Id);
    }

    [Fact]
    public async Task Approved_And_Cancelled_Stay_Terminal()
    {
        // ADR-0023 reopens exactly one status. Approved work must not be reopened silently,
        // and a withdrawn request stays withdrawn.
        var cancelled = await NewDraftAsync("Sales Ops Terminal A");
        (await Requester.PostAsync($"/api/requisitions/{cancelled.Id}/cancel", null))
            .EnsureSuccessStatusCode();
        var reviseCancelled = await Requester.PostAsync($"/api/requisitions/{cancelled.Id}/revise", null);
        Assert.Equal(HttpStatusCode.Conflict, reviseCancelled.StatusCode);

        var approved = await NewDraftAsync("Sales Ops Terminal B");
        (await Requester.PostAsync($"/api/requisitions/{approved.Id}/submit", null))
            .EnsureSuccessStatusCode();
        (await ClientFor(Roles.Approver, _factory.FinanceApproverUserId)
            .PostAsJsonAsync($"/api/requisitions/{approved.Id}/decision",
                new ApprovalDecisionRequest { Approve = true })).EnsureSuccessStatusCode();

        var reviseApproved = await Requester.PostAsync($"/api/requisitions/{approved.Id}/revise", null);
        Assert.Equal(HttpStatusCode.Conflict, reviseApproved.StatusCode);
    }

    [Fact]
    public async Task Only_The_Requester_Can_Revise_And_The_Two_Guards_Refuse_Differently()
    {
        // Mirrors cancellation: an approver's tool is Reject, which records a decision. Being
        // asked to approve something is not authority to rewrite it and re-queue it.
        //
        // Two separate guards refuse this, and they give different answers on purpose:
        var rejected = await RejectedRequisitionAsync("Sales Ops Guarded", "Not now.");

        // 1. The permission gate. Approver does not hold requisitions:update, so it is
        //    stopped before the service is ever reached — a blanket 403 that says nothing
        //    about this particular requisition. This is the second-order consequence
        //    ADR-0022 recorded for submit/edit/cancel; revise inherits it by construction.
        var approver = await ClientFor(Roles.Approver, _factory.FinanceApproverUserId)
            .PostAsync($"/api/requisitions/{rejected.Id}/revise", null);
        Assert.Equal(HttpStatusCode.Forbidden, approver.StatusCode);

        // 2. The ownership guard. A Recruiter DOES hold requisitions:update (ADR-0022), so
        //    they clear the policy and are stopped by IsOwnerOrCompanyWide instead — 404, not
        //    403, so a caller cannot use this endpoint to discover that the requisition
        //    exists (ADR-0003). Without this case the ownership rule would be untested, since
        //    the 403 above never reaches it.
        var recruiter = await ClientFor(Roles.Recruiter, _factory.RecruiterUserId)
            .PostAsync($"/api/requisitions/{rejected.Id}/revise", null);
        Assert.Equal(HttpStatusCode.NotFound, recruiter.StatusCode);

        // Still Rejected — neither refusal half-applied.
        var after = await Hr.GetFromJsonAsync<RequisitionDetailDto>($"/api/requisitions/{rejected.Id}");
        Assert.Equal("Rejected", after!.Status);
    }
}
