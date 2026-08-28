using System.Net.Http.Json;
using RecruitOps.Application.DTOs;
using Xunit;

namespace RecruitOps.Api.Tests;

/// <summary>
/// Module 3 — <c>GET /api/interviews</c>, the interviews list (added 2026-08-28).
///
/// <para><b>The rule under test is "the list agrees with the detail screen".</b> Every kit screen
/// has carried an "Interviews" rail item since the design was drawn, and there was nothing behind
/// it; a round could only be reached by opening a posting and expanding a candidate. Adding the
/// list means restating an access rule, and a restated access rule is where the two drift: a list
/// that shows less than the detail screen will open produces "I can open it from the board but it
/// is not in my list", and one that shows more is a leak.</para>
///
/// <para>So reach here is deliberately <c>IApplicationAccess.ResolveAsync</c>'s two clauses, not
/// a fresh predicate: the candidate axis (ADR-0003 scoping bundled with the ADR-0018 exclusion),
/// <b>or</b> sitting on the panel (ADR-0017 §4). <c>InterviewService</c> comments say the same;
/// these tests are what stop the two from parting company.</para>
/// </summary>
public class InterviewListTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;
    private readonly Module3Scenario _scenario;

    public InterviewListTests(CustomWebAppFactory factory)
    {
        _factory = factory;
        _scenario = new Module3Scenario(factory);
    }

    private static async Task<List<InterviewListItemDto>> ListAsync(HttpClient client, string query = "")
    {
        var res = await client.GetAsync("/api/interviews" + query);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<List<InterviewListItemDto>>())!;
    }

    [Fact]
    public async Task A_Recruiter_Sees_A_Scheduled_Round_With_Its_Candidate_And_Department()
    {
        var (_, applicationId) = await _scenario.ApplicationAsync("List — Recruiter view");
        var interview = await _scenario.ScheduleAsync(applicationId);

        var row = Assert.Single(await ListAsync(_scenario.Recruiter()), i => i.Id == interview.Id);

        Assert.Equal("Scheduled", row.Status);
        Assert.Equal(1, row.Round);
        Assert.Equal(2, row.PanelSize);
        Assert.Equal(0, row.SubmittedCount);
        Assert.False(string.IsNullOrWhiteSpace(row.CandidateName));
        Assert.False(string.IsNullOrWhiteSpace(row.DepartmentName));
        Assert.False(string.IsNullOrWhiteSpace(row.JobPostingTitle));
    }

    [Fact]
    public async Task A_Panel_Member_From_Another_Department_Sees_It_In_Their_List()
    {
        // The Finance manager has no departmental reach into a Sales application. Being put on
        // the panel is the only thing that changes, and the list must honour it — otherwise the
        // interview they are expected to attend never appears anywhere they would look.
        var (_, applicationId) = await _scenario.ApplicationAsync("List — cross-department panel");

        var before = await ListAsync(_scenario.FinanceManager());
        var interview = await _scenario.ScheduleAsync(applicationId);
        var after = await ListAsync(_scenario.FinanceManager());

        Assert.DoesNotContain(before, i => i.JobPostingTitle == "List — cross-department panel");
        var row = Assert.Single(after, i => i.Id == interview.Id);
        Assert.True(row.IsOnPanel);
    }

    [Fact]
    public async Task A_Manager_Does_Not_See_Rounds_They_Are_Not_On_In_Another_Department()
    {
        // The counterpart to the test above, and the one that would catch a list built on a
        // looser predicate than the detail screen's.
        var (_, applicationId) = await _scenario.ApplicationAsync("List — no panel seat");
        var interview = await _scenario.ScheduleWithAsync(applicationId, _factory.HiringManagerUserId);

        var list = await ListAsync(_scenario.FinanceManager());

        Assert.DoesNotContain(list, i => i.Id == interview.Id);
    }

    [Fact]
    public async Task A_Role_Excluded_From_Candidate_Data_Sees_Only_Its_Own_Panels()
    {
        // ADR-0018: an Approver is company-wide on the requisition axis, which is exactly the
        // `true` that once handed them every candidate in the company. They get no standing reach
        // here — but participation still reaches one application (ADR-0017 §4), and the detail
        // endpoint honours that today, so the list must too.
        var (_, otherApplicationId) = await _scenario.ApplicationAsync("List — approver must not see");
        var invisible = await _scenario.ScheduleWithAsync(otherApplicationId, _factory.HiringManagerUserId);

        var (_, ownApplicationId) = await _scenario.ApplicationAsync("List — approver on panel");
        var visible = await _scenario.ScheduleWithAsync(ownApplicationId, _factory.FinanceApproverUserId);

        var list = await ListAsync(_scenario.FinanceApprover());

        Assert.DoesNotContain(list, i => i.Id == invisible.Id);
        var row = Assert.Single(list, i => i.Id == visible.Id);
        Assert.True(row.IsOnPanel);
    }

    [Fact]
    public async Task Panel_Reach_Extends_To_Every_Round_Of_The_Same_Application()
    {
        // Found by security review of the first cut, 2026-08-28. The list keyed panel reach on
        // the INTERVIEW; `IApplicationAccess.IsOnPanelForAsync` keys it on the APPLICATION, and
        // ADR-0017 §4 is explicit: "An InterviewParticipant row grants its user read access to
        // that one job application, ITS INTERVIEWS, its notes".
        //
        // So sitting on round 1 lets you open round 2's detail page, and the first version of
        // this list hid round 2 from you — "I can open it from the board but it is not in my
        // list", which is the exact failure this file's header says it exists to prevent. Under-
        // disclosure rather than a leak, but it breaks the rule the list was written to honour.
        var (_, applicationId) = await _scenario.ApplicationAsync("List — sibling rounds");

        var round1 = await _scenario.ScheduleWithAsync(applicationId, _factory.FinanceApproverUserId);
        var round2 = await _scenario.ScheduleWithAsync(applicationId, _factory.HiringManagerUserId);

        // The detail endpoint opens round 2 for the approver, via round 1's participation.
        var detail = await _scenario.FinanceApprover().GetAsync($"/api/interviews/{round2.Id}");
        Assert.Equal(System.Net.HttpStatusCode.OK, detail.StatusCode);

        // The list must agree with it.
        var list = await ListAsync(_scenario.FinanceApprover());

        Assert.Contains(list, i => i.Id == round1.Id);
        Assert.Contains(list, i => i.Id == round2.Id);

        // …and still say which one is actually theirs to score. Visibility is per application;
        // "am I on this panel" and "do I owe a scorecard" stay per interview.
        Assert.True(Assert.Single(list, i => i.Id == round1.Id).IsOnPanel);
        Assert.False(Assert.Single(list, i => i.Id == round2.Id).IsOnPanel);
        Assert.False(Assert.Single(list, i => i.Id == round2.Id).MyScorecardOutstanding);
    }

    [Fact]
    public async Task OnlyMine_Stays_Per_Interview_Even_Though_Visibility_Is_Per_Application()
    {
        // The control is labelled "Only mine". Widening it to the application would list rounds
        // the caller is not sitting on, which is not what the words say.
        var (_, applicationId) = await _scenario.ApplicationAsync("List — onlyMine sibling");

        var round1 = await _scenario.ScheduleWithAsync(applicationId, _factory.FinanceApproverUserId);
        var round2 = await _scenario.ScheduleWithAsync(applicationId, _factory.HiringManagerUserId);

        var mine = await ListAsync(_scenario.FinanceApprover(), "?onlyMine=true");

        Assert.Contains(mine, i => i.Id == round1.Id);
        Assert.DoesNotContain(mine, i => i.Id == round2.Id);
    }

    [Fact]
    public async Task Cancelled_Rounds_Are_Hidden_By_Default_And_Recoverable_By_Filter()
    {
        var (_, applicationId) = await _scenario.ApplicationAsync("List — cancelled");
        var interview = await _scenario.ScheduleAsync(applicationId);

        var cancel = await _scenario.Recruiter().PostAsJsonAsync(
            $"/api/interviews/{interview.Id}/cancel",
            new CancelInterviewRequest { Reason = "Candidate withdrew" });
        cancel.EnsureSuccessStatusCode();

        Assert.DoesNotContain(await ListAsync(_scenario.Recruiter()), i => i.Id == interview.Id);

        // Kept, not deleted: it is the reason a candidate was asked to move twice.
        var withCancelled = await ListAsync(_scenario.Recruiter(), "?status=Cancelled");
        Assert.Contains(withCancelled, i => i.Id == interview.Id);
    }

    [Fact]
    public async Task The_Default_View_Is_Everything_Except_Cancelled()
    {
        // Pins the exclusion, not a hand-written pair. `NoShow` exists and must not vanish from
        // every list because the default was written as "Scheduled or Completed" — the same
        // reasoning as the nav rail storing which groups are shut rather than which are open.
        var (_, applicationId) = await _scenario.ApplicationAsync("List — default statuses");
        var interview = await _scenario.ScheduleAsync(applicationId);

        var complete = await _scenario.Recruiter().PostAsJsonAsync(
            $"/api/interviews/{interview.Id}/complete", new CompleteInterviewRequest());
        complete.EnsureSuccessStatusCode();

        Assert.Contains(await ListAsync(_scenario.Recruiter()), i => i.Id == interview.Id);
    }

    [Fact]
    public async Task OnlyMine_Restricts_To_Rounds_The_Caller_Sits_On()
    {
        var (_, applicationId) = await _scenario.ApplicationAsync("List — onlyMine");
        // The recruiter schedules but is not on the panel.
        var interview = await _scenario.ScheduleWithAsync(applicationId, _factory.HiringManagerUserId);

        Assert.Contains(await ListAsync(_scenario.Recruiter()), i => i.Id == interview.Id);
        Assert.DoesNotContain(
            await ListAsync(_scenario.Recruiter(), "?onlyMine=true"), i => i.Id == interview.Id);
        Assert.Contains(
            await ListAsync(_scenario.SalesManager(), "?onlyMine=true"), i => i.Id == interview.Id);
    }

    [Fact]
    public async Task An_Unrecognised_Status_Matches_Nothing_Rather_Than_Erroring()
    {
        var (_, applicationId) = await _scenario.ApplicationAsync("List — bad status");
        await _scenario.ScheduleAsync(applicationId);

        // A stale bookmark should not become a broken screen, and an unknown status genuinely
        // matches no interview — so empty is the truthful answer, not a 400.
        Assert.Empty(await ListAsync(_scenario.Recruiter(), "?status=Postponed"));

        // `Enum.TryParse` accepts ANY numeric string, so this one parses to an InterviewStatus
        // naming no member. `Enum.IsDefined` is what stops it counting as a real status.
        Assert.Empty(await ListAsync(_scenario.Recruiter(), "?status=999"));
    }

    [Fact]
    public async Task Submitted_Count_Rises_And_The_Submitter_Is_No_Longer_Outstanding()
    {
        var (_, applicationId) = await _scenario.ApplicationAsync("List — submitted count");
        var template = await _scenario.EnsureCompanyTemplateAsync();
        var interview = await _scenario.ScheduleAsync(applicationId);

        var before = Assert.Single(
            await ListAsync(_scenario.SalesManager()), i => i.Id == interview.Id);
        Assert.Equal(0, before.SubmittedCount);
        Assert.True(before.MyScorecardOutstanding);

        var submit = await _scenario.SalesManager().PostAsJsonAsync(
            $"/api/interviews/{interview.Id}/scorecard/submit",
            Module3Scenario.CompleteAnswers(template, "Yes"));
        submit.EnsureSuccessStatusCode();

        var after = Assert.Single(
            await ListAsync(_scenario.SalesManager()), i => i.Id == interview.Id);
        Assert.Equal(1, after.SubmittedCount);
        Assert.False(after.MyScorecardOutstanding);

        // The other panel member has not submitted, and the count says so without saying what
        // was written — that distinction is the blind rule, and the next test pins it.
        var theirs = Assert.Single(
            await ListAsync(_scenario.FinanceManager()), i => i.Id == interview.Id);
        Assert.Equal(1, theirs.SubmittedCount);
        Assert.True(theirs.MyScorecardOutstanding);
    }

    [Fact]
    public async Task The_List_Carries_No_Evaluation_Content_At_All()
    {
        // The blind rule (ADR-0017 §3) lives in GET /interviews/{id}/scorecards. This list has no
        // such rule and must never need one — so it must not carry a rating, a recommendation or
        // a summary comment for anybody, submitted or not. Asserted against the raw JSON rather
        // than the DTO, because a leak added as an extra property would still deserialise fine.
        var (_, applicationId) = await _scenario.ApplicationAsync("List — no evaluation leak");
        var template = await _scenario.EnsureCompanyTemplateAsync();
        var interview = await _scenario.ScheduleAsync(applicationId);

        var submit = await _scenario.SalesManager().PostAsJsonAsync(
            $"/api/interviews/{interview.Id}/scorecard/submit",
            Module3Scenario.CompleteAnswers(template, "StrongYes"));
        submit.EnsureSuccessStatusCode();

        var json = await _scenario.FinanceManager().GetStringAsync("/api/interviews");

        Assert.DoesNotContain("StrongYes", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("recommendation", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Assessed against the standard criteria", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("summaryComment", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task An_Unauthenticated_Caller_Gets_401()
    {
        var res = await _factory.CreateClient().GetAsync("/api/interviews");
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, res.StatusCode);
    }
}
