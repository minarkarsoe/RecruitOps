using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RecruitOps.Api.Auth;
using RecruitOps.Application.DTOs;
using RecruitOps.Domain.Enums;
using RecruitOps.Infrastructure.Persistence;
using Xunit;

namespace RecruitOps.Api.Tests;

/// <summary>Module 4 slice A (ADR-0030): drafting an offer from an application, the budget check,
/// internal approval through Module 1's rules and an offer chain, and who reaches what.
///
/// <para>Every client is a real user of its role: <c>Module3Scenario.Recruiter()</c> sends the
/// Recruiter role on the Admin's id, so the offer actions here use the real Recruiter.</para></summary>
public class OfferTests : IClassFixture<CustomWebAppFactory>
{
    /// <summary>The Sales offer chain's one step — distinct from every requisition chain label, so a
    /// test can tell which chain a round was taken from.</summary>
    internal const string OfferStepLabel = "Finance Controller (offers)";

    private readonly CustomWebAppFactory _factory;
    private readonly Module3Scenario _scenario;

    public OfferTests(CustomWebAppFactory factory)
    {
        _factory = factory;
        _scenario = new Module3Scenario(factory);
    }

    private HttpClient Recruiter() => _scenario.Client(Roles.Recruiter, _factory.RecruiterUserId);
    private HttpClient Admin() => _scenario.Client(Roles.Admin, _factory.AdminUserId);

    internal static async Task<Guid> OfferStageApplicationAsync(Module3Scenario scenario, HttpClient recruiter, string title)
    {
        var (_, applicationId) = await scenario.ApplicationAsync(title);
        (await recruiter.PostAsJsonAsync($"/api/applications/{applicationId}/stage",
            new MoveStageRequest { ToStatus = "Offer" })).EnsureSuccessStatusCode();
        return applicationId;
    }

    internal static async Task<OfferDetailDto> DraftAsync(Module3Scenario scenario, HttpClient recruiter, string title)
    {
        var applicationId = await OfferStageApplicationAsync(scenario, recruiter, title);
        var res = await recruiter.PostAsJsonAsync("/api/offers", new CreateOfferRequest { JobApplicationId = applicationId });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<OfferDetailDto>())!;
    }

    internal static async Task<OfferDetailDto> TermsAsync(HttpClient recruiter, OfferDetailDto offer, decimal baseSalary, decimal allowances = 0)
    {
        var res = await recruiter.PutAsJsonAsync($"/api/offers/{offer.Id}", new UpdateOfferRequest
        {
            BaseSalary = baseSalary,
            Allowances = allowances,
            JoiningDate = offer.JoiningDate,
            ProbationMonths = offer.ProbationMonths,
            ExpiresOn = offer.ExpiresOn,
            LetterBody = offer.LetterBody!,
        });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<OfferDetailDto>())!;
    }

    /// <summary>A Sales offer chain whose one step is the Finance approver. Idempotent: the fixture's
    /// database is shared by every test in the class.</summary>
    internal static async Task EnsureOfferChainAsync(HttpClient admin, Guid departmentId, Guid approverId)
    {
        var chains = await admin.GetFromJsonAsync<List<ApprovalChainDto>>("/api/approvalchains");
        if (chains!.Any(c => c.Purpose == "Offer" && c.DepartmentId == departmentId)) return;
        (await admin.PostAsJsonAsync("/api/approvalchains", new CreateApprovalChainRequest
        {
            Name = "Sales offers",
            DepartmentId = departmentId,
            Purpose = "Offer",
            Steps = new[] { new CreateApprovalChainStepRequest { ApproverUserId = approverId, Label = OfferStepLabel } },
        })).EnsureSuccessStatusCode();
    }

    private Task EnsureOfferChainAsync() => EnsureOfferChainAsync(Admin(), _factory.SalesDepartmentId, _factory.FinanceApproverUserId);

    private async Task<OfferDetailDto> PendingAsync(string title)
    {
        await EnsureOfferChainAsync();
        var draft = await DraftAsync(_scenario, Recruiter(), title);
        await TermsAsync(Recruiter(), draft, 900_000m); // the scenario's budget is 800,000
        var res = await Recruiter().PostAsync($"/api/offers/{draft.Id}/submit", null);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<OfferDetailDto>())!;
    }

    [Fact]
    public async Task A_Draft_Is_Prefilled_From_The_Application_And_Numbered()
    {
        var offer = await DraftAsync(_scenario, Recruiter(), "Offer Prefill");

        Assert.Equal("Draft", offer.Status);
        Assert.Matches(@"^OFR-\d{4}-\d{4}$", offer.Reference);
        Assert.Equal("Applicant for Offer Prefill", offer.CandidateName);
        Assert.Equal(_factory.SalesDepartmentId, offer.DepartmentId);
        Assert.Equal(800_000m, offer.BaseSalary);     // the requisition's budget is the starting point
        Assert.Equal(800_000m, offer.SalaryBudget);
        Assert.False(offer.OverBudget);
        Assert.False(offer.NeedsApproval);
        Assert.Contains("{{CandidateName}}", offer.LetterBody);

        var letter = await Recruiter().GetFromJsonAsync<OfferLetterDto>($"/api/offers/{offer.Id}/letter");
        Assert.Contains("Dear Applicant for Offer Prefill,", letter!.Text);
        Assert.Contains("MMK 800,000 a month", letter.Text);
    }

    [Fact]
    public async Task An_Offer_Needs_The_Offer_Stage_And_Only_One_Is_Open_At_A_Time()
    {
        var (_, early) = await _scenario.ApplicationAsync("Offer Too Early");
        var tooEarly = await Recruiter().PostAsJsonAsync("/api/offers", new CreateOfferRequest { JobApplicationId = early });
        Assert.Equal(HttpStatusCode.Conflict, tooEarly.StatusCode);

        var offer = await DraftAsync(_scenario, Recruiter(), "Offer Twice");
        var twice = await Recruiter().PostAsJsonAsync("/api/offers", new CreateOfferRequest { JobApplicationId = offer.JobApplicationId });
        Assert.Equal(HttpStatusCode.Conflict, twice.StatusCode);

        var eligible = await Recruiter().GetFromJsonAsync<List<OfferEligibleApplicationDto>>("/api/offers/eligible-applications");
        Assert.DoesNotContain(eligible!, e => e.JobApplicationId == offer.JobApplicationId);
    }

    [Fact]
    public async Task Within_Budget_Needs_No_Approval_And_Over_Budget_Says_So_At_Entry()
    {
        var offer = await DraftAsync(_scenario, Recruiter(), "Offer Budget");
        await EnsureOfferChainAsync();

        // The editor decides "needs approval" from the terms being typed, before they are saved — so
        // a Draft within budget still names the chain an unsaved raise would go to. Without it, the
        // editor told a recruiter there was no offer chain (seen in the browser, 2026-10-21).
        var draft = await Recruiter().GetFromJsonAsync<OfferDetailDto>($"/api/offers/{offer.Id}");
        Assert.False(draft!.NeedsApproval);
        Assert.Equal(new[] { OfferStepLabel }, draft.ApprovalPreview);

        var within = await Recruiter().PostAsync($"/api/offers/{offer.Id}/submit", null);
        Assert.Equal(HttpStatusCode.Conflict, within.StatusCode);

        // Base plus allowances is the package: 760,000 + 50,000 = 810,000 > 800,000.
        var over = await TermsAsync(Recruiter(), offer, 760_000m, 50_000m);
        Assert.True(over.OverBudget);
        Assert.True(over.NeedsApproval);

        // "Approval this will need", before anything is submitted.
        var preview = await Recruiter().GetFromJsonAsync<OfferDetailDto>($"/api/offers/{offer.Id}");
        Assert.Equal(new[] { OfferStepLabel }, preview!.ApprovalPreview);
    }

    [Fact]
    public async Task Over_Budget_Goes_Through_The_Offer_Chain_And_Approval_Clears_It()
    {
        var pending = await PendingAsync("Offer Approve");
        Assert.Equal("PendingApproval", pending.Status);
        var step = Assert.Single(pending.Approvals);
        Assert.Equal(OfferStepLabel, step.Label);
        Assert.Equal(_factory.FinanceApproverUserId, step.ApproverUserId);
        Assert.Null(pending.ApprovalPreview); // submitted: the rail itself is the answer now
        var row = (await Recruiter().GetFromJsonAsync<List<OfferListItemDto>>("/api/offers?group=awaiting"))!
            .Single(o => o.Id == pending.Id);
        Assert.Equal(OfferStepLabel, row.WaitingOn);

        // Only a Draft can be edited while it waits.
        var edit = await Recruiter().PutAsJsonAsync($"/api/offers/{pending.Id}", new UpdateOfferRequest
        {
            BaseSalary = 1m,
            JoiningDate = pending.JoiningDate,
            ExpiresOn = pending.ExpiresOn,
            LetterBody = "x",
        });
        Assert.Equal(HttpStatusCode.Conflict, edit.StatusCode);

        var decided = await _scenario.FinanceApprover().PostAsJsonAsync(
            $"/api/offers/{pending.Id}/decide", new ApprovalDecisionRequest { Approve = true, Round = 1 });
        decided.EnsureSuccessStatusCode();
        var approved = (await decided.Content.ReadFromJsonAsync<OfferDetailDto>())!;
        Assert.Equal("Approved", approved.Status);
        Assert.NotNull(approved.ApprovedAt);
    }

    [Fact]
    public async Task An_Approvers_Rejection_Returns_A_Draft_And_The_Round_Stays()
    {
        var pending = await PendingAsync("Offer Reject");

        var rejected = await _scenario.FinanceApprover().PostAsJsonAsync(
            $"/api/offers/{pending.Id}/decide", new ApprovalDecisionRequest { Approve = false, Comment = "Too high.", Round = 1 });
        rejected.EnsureSuccessStatusCode();
        var back = (await rejected.Content.ReadFromJsonAsync<OfferDetailDto>())!;

        // Never "Rejected" — that word is the candidate's (ADR-0030 §3).
        Assert.Equal("Draft", back.Status);
        Assert.Equal("Rejected", Assert.Single(back.Approvals).Decision);

        var resubmit = await Recruiter().PostAsync($"/api/offers/{pending.Id}/submit", null);
        resubmit.EnsureSuccessStatusCode();
        var again = (await resubmit.Content.ReadFromJsonAsync<OfferDetailDto>())!;
        Assert.Equal(new[] { 1, 2 }, again.Approvals.Select(a => a.Round).ToArray());
        Assert.Equal("Waiting", again.Approvals.Single(a => a.Round == 2).Decision);
    }

    [Fact]
    public async Task A_Requisition_Never_Takes_An_Offer_Chain()
    {
        // A Sales offer chain exists and, being department-specific, would be preferred if purpose
        // were ignored. A Sales requisition must still go through a requisition chain.
        await EnsureOfferChainAsync();
        var create = await _scenario.SalesManager().PostAsJsonAsync("/api/requisitions", new CreateRequisitionRequest
        {
            DepartmentId = _factory.SalesDepartmentId,
            Title = "Offer Chain Purpose",
            JobDescription = "JD.",
            Headcount = 1,
        });
        var draft = (await create.Content.ReadFromJsonAsync<RequisitionDetailDto>())!;
        var submitted = await _scenario.SalesManager().PostAsync($"/api/requisitions/{draft.Id}/submit", null);
        submitted.EnsureSuccessStatusCode();
        var requisition = (await submitted.Content.ReadFromJsonAsync<RequisitionDetailDto>())!;

        Assert.NotEmpty(requisition.Approvals);
        Assert.DoesNotContain(requisition.Approvals, a => a.Label == OfferStepLabel);
    }

    [Fact]
    public async Task A_Hiring_Manager_Reads_Their_Departments_Offers_Without_Salary()
    {
        var offer = await DraftAsync(_scenario, Recruiter(), "Offer Manager View");

        var asSales = await _scenario.SalesManager().GetFromJsonAsync<OfferDetailDto>($"/api/offers/{offer.Id}");
        Assert.False(asSales!.SalaryVisible);
        Assert.Null(asSales.BaseSalary);
        Assert.Null(asSales.Allowances);
        Assert.Null(asSales.SalaryBudget);
        Assert.Null(asSales.LetterBody); // it quotes the salary
        Assert.False(asSales.CanManage);
        Assert.Equal(HttpStatusCode.NotFound, (await _scenario.SalesManager().GetAsync($"/api/offers/{offer.Id}/letter")).StatusCode);

        var listed = (await _scenario.SalesManager().GetFromJsonAsync<List<OfferListItemDto>>("/api/offers"))!
            .Single(o => o.Id == offer.Id);
        Assert.Null(listed.BaseSalary);

        // Read-only: every write is recruitment staff's.
        Assert.Equal(HttpStatusCode.Forbidden, (await _scenario.SalesManager().PostAsync($"/api/offers/{offer.Id}/submit", null)).StatusCode);

        // Another department's manager does not reach it at all.
        Assert.Equal(HttpStatusCode.NotFound, (await _scenario.FinanceManager().GetAsync($"/api/offers/{offer.Id}")).StatusCode);
        Assert.DoesNotContain((await _scenario.FinanceManager().GetFromJsonAsync<List<OfferListItemDto>>("/api/offers"))!,
            o => o.Id == offer.Id);
    }

    [Fact]
    public async Task Being_Named_On_The_Approval_Is_What_Lets_An_Approver_In()
    {
        await EnsureOfferChainAsync();
        var draft = await DraftAsync(_scenario, Recruiter(), "Offer Named Approver");
        await TermsAsync(Recruiter(), draft, 900_000m);

        // Excluded from candidate data (ADR-0018) — until named on this offer's approval.
        Assert.Equal(HttpStatusCode.NotFound, (await _scenario.FinanceApprover().GetAsync($"/api/offers/{draft.Id}")).StatusCode);
        Assert.DoesNotContain((await _scenario.FinanceApprover().GetFromJsonAsync<List<OfferListItemDto>>("/api/offers"))!,
            o => o.Id == draft.Id);

        (await Recruiter().PostAsync($"/api/offers/{draft.Id}/submit", null)).EnsureSuccessStatusCode();

        var asApprover = await _scenario.FinanceApprover().GetFromJsonAsync<OfferDetailDto>($"/api/offers/{draft.Id}");
        Assert.True(asApprover!.SalaryVisible);           // deciding needs the terms
        Assert.Equal(900_000m, asApprover.BaseSalary);
        Assert.False(asApprover.CanManage);
        var mine = (await _scenario.FinanceApprover().GetFromJsonAsync<List<OfferListItemDto>>("/api/offers?group=awaiting"))!
            .Single(o => o.Id == draft.Id);
        Assert.True(mine.AwaitingMe);

        // The offer, not the candidate's file.
        Assert.Equal(HttpStatusCode.NotFound,
            (await _scenario.FinanceApprover().GetAsync($"/api/applications/{draft.JobApplicationId}/notes")).StatusCode);
    }

    [Fact]
    public async Task Someone_Not_Named_Cannot_Decide_And_Is_Told_Nothing()
    {
        var pending = await PendingAsync("Offer Outsider");

        // The Sales manager reaches the offer (read-only) but holds no step: a bare 404, not a 409.
        var outsider = await _scenario.SalesManager().PostAsJsonAsync(
            $"/api/offers/{pending.Id}/decide", new ApprovalDecisionRequest { Approve = true });
        Assert.Equal(HttpStatusCode.NotFound, outsider.StatusCode);

        var interviewer = _scenario.Client(Roles.Interviewer, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Forbidden, (await interviewer.GetAsync("/api/offers")).StatusCode);
    }

    [Fact]
    public async Task Revising_An_Approved_Offer_Voids_The_Approval()
    {
        var pending = await PendingAsync("Offer Revise");
        (await _scenario.FinanceApprover().PostAsJsonAsync($"/api/offers/{pending.Id}/decide",
            new ApprovalDecisionRequest { Approve = true, Round = 1 })).EnsureSuccessStatusCode();

        var revise = await Recruiter().PostAsync($"/api/offers/{pending.Id}/revise", null);
        revise.EnsureSuccessStatusCode();
        var draft = (await revise.Content.ReadFromJsonAsync<OfferDetailDto>())!;
        Assert.Equal("Draft", draft.Status);
        Assert.Null(draft.ApprovedAt);

        // The next submission is a fresh round; the approved one stays as the record.
        var resubmit = (await (await Recruiter().PostAsync($"/api/offers/{pending.Id}/submit", null))
            .Content.ReadFromJsonAsync<OfferDetailDto>())!;
        Assert.Equal("PendingApproval", resubmit.Status);
        Assert.Equal("Approved", resubmit.Approvals.Single(a => a.Round == 1).Decision);
        Assert.Equal("Waiting", resubmit.Approvals.Single(a => a.Round == 2).Decision);
    }

    [Fact]
    public async Task Only_A_Draft_Nobody_Has_Been_Asked_About_Can_Be_Deleted()
    {
        var fresh = await DraftAsync(_scenario, Recruiter(), "Offer Delete Fresh");
        Assert.Equal(HttpStatusCode.NoContent, (await Recruiter().DeleteAsync($"/api/offers/{fresh.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Recruiter().GetAsync($"/api/offers/{fresh.Id}")).StatusCode);

        var asked = await PendingAsync("Offer Delete Asked");
        (await _scenario.FinanceApprover().PostAsJsonAsync($"/api/offers/{asked.Id}/decide",
            new ApprovalDecisionRequest { Approve = false, Round = 1 })).EnsureSuccessStatusCode(); // back to Draft
        Assert.Equal(HttpStatusCode.Conflict, (await Recruiter().DeleteAsync($"/api/offers/{asked.Id}")).StatusCode);
    }

    [Fact]
    public async Task A_Change_Read_Before_An_Approval_Cannot_Overwrite_It()
    {
        // A recruiter revises (back to Draft) while the approver approves. Without the status token
        // both commit, and the offer is Approved after its approval was voided — or a Draft whose
        // approval round says Approved. The second to commit must fail instead.
        var pending = await PendingAsync("Offer Race");

        using var slow = _factory.Services.CreateScope();
        var slowDb = slow.ServiceProvider.GetRequiredService<AppDbContext>();
        var stale = await slowDb.Offers.IgnoreQueryFilters().SingleAsync(o => o.Id == pending.Id);

        (await _scenario.FinanceApprover().PostAsJsonAsync($"/api/offers/{pending.Id}/decide",
            new ApprovalDecisionRequest { Approve = true, Round = 1 })).EnsureSuccessStatusCode();

        stale.Status = OfferStatus.Draft; // what ReviseAsync writes, from a read taken before the approval
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => slowDb.SaveChangesAsync());
        Assert.Equal("Approved", (await Recruiter().GetFromJsonAsync<OfferDetailDto>($"/api/offers/{pending.Id}"))!.Status);
    }

    [Fact]
    public async Task A_Decision_On_A_Round_The_Approver_Did_Not_See_Is_Refused()
    {
        // The approver opened round 1. The recruiter revised and resubmitted: round 2, new terms. A click
        // on the stale page must not approve terms the approver never saw (security review, M3).
        var pending = await PendingAsync("Offer Stale Round");
        (await Recruiter().PostAsync($"/api/offers/{pending.Id}/revise", null)).EnsureSuccessStatusCode();
        (await Recruiter().PostAsync($"/api/offers/{pending.Id}/submit", null)).EnsureSuccessStatusCode();

        var stale = await _scenario.FinanceApprover().PostAsJsonAsync($"/api/offers/{pending.Id}/decide",
            new ApprovalDecisionRequest { Approve = true, Round = 1 });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var unbound = await _scenario.FinanceApprover().PostAsJsonAsync($"/api/offers/{pending.Id}/decide",
            new ApprovalDecisionRequest { Approve = true });
        Assert.Equal(HttpStatusCode.Conflict, unbound.StatusCode);

        var current = await _scenario.FinanceApprover().PostAsJsonAsync($"/api/offers/{pending.Id}/decide",
            new ApprovalDecisionRequest { Approve = true, Round = 2 });
        current.EnsureSuccessStatusCode(); // positive control
    }

    [Fact]
    public async Task An_Approvers_Comment_Follows_The_Salary_Rule()
    {
        // "Come down to 800,000" quotes the package. A Hiring Manager who may not see salaries must not
        // read it in a comment either (security review, M1).
        var pending = await PendingAsync("Offer Comment");
        (await _scenario.FinanceApprover().PostAsJsonAsync($"/api/offers/{pending.Id}/decide",
            new ApprovalDecisionRequest { Approve = false, Comment = "Come down to 800,000.", Round = 1 })).EnsureSuccessStatusCode();

        var asRecruiter = await Recruiter().GetFromJsonAsync<OfferDetailDto>($"/api/offers/{pending.Id}");
        Assert.Equal("Come down to 800,000.", Assert.Single(asRecruiter!.Approvals).Comment);

        var asManager = await _scenario.SalesManager().GetFromJsonAsync<OfferDetailDto>($"/api/offers/{pending.Id}");
        Assert.Null(Assert.Single(asManager!.Approvals).Comment);
        Assert.Equal("Rejected", asManager.Approvals[0].Decision); // the outcome is not hidden, only the words
    }

    [Fact]
    public async Task Only_The_Live_Round_Grants_An_Approver_Reach()
    {
        // Named on round 1; round 2 (the chain having changed) names someone else. The round-1 approver
        // no longer reads the offer — not "any round, for good" (security review, M2).
        var pending = await PendingAsync("Offer Old Round");
        Assert.Equal(HttpStatusCode.OK, (await _scenario.FinanceApprover().GetAsync($"/api/offers/{pending.Id}")).StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.OfferApprovals.Add(new RecruitOps.Domain.Entities.OfferApproval
            {
                TenantId = _factory.TenantA,
                OfferId = pending.Id,
                Round = 2,
                Sequence = 1,
                ApproverUserId = _factory.AdminUserId,
                Label = "Someone else",
            });
            await db.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.NotFound, (await _scenario.FinanceApprover().GetAsync($"/api/offers/{pending.Id}")).StatusCode);
        Assert.DoesNotContain((await _scenario.FinanceApprover().GetFromJsonAsync<List<OfferListItemDto>>("/api/offers"))!,
            o => o.Id == pending.Id);
    }
}
