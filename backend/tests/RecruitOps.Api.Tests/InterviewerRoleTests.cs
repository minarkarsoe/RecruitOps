using System.Net;
using System.Net.Http.Json;
using RecruitOps.Api.Auth;
using RecruitOps.Application.DTOs;
using Xunit;

namespace RecruitOps.Api.Tests;

/// <summary>The Interviewer role sees and scores its own panels, and nothing else (ADR-0021
/// amendment, 2026-10-19; product owner's decision 2026-10-17).
///
/// <para>Found by walking every menu as every role on a running install: the role's only menu
/// item, Interviews, answered 403 — no role policy admitted the <c>Interviewer</c> enum, which
/// is also what a deactivated custom role's holders carry. A user given the role could not see or
/// score the panel they had been put on.</para>
///
/// <para>Every client is a real Interviewer, created through the API: the services read the
/// panel from the database by user id, and a header on someone else's id would be someone
/// else.</para>
/// </summary>
public class InterviewerRoleTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;
    private readonly Module3Scenario _scenario;

    public InterviewerRoleTests(CustomWebAppFactory factory)
    {
        _factory = factory;
        _scenario = new Module3Scenario(factory);
    }

    private async Task<(HttpClient Client, Guid UserId)> InterviewerAsync()
    {
        var res = await _scenario.Client(Roles.Admin, _factory.AdminUserId).PostAsJsonAsync("/api/users",
            new CreateUserRequest($"ivw.{Guid.NewGuid():N}@alpha.test", "Ivy Interviewer", "Correct-Horse-9",
                Role: "Interviewer"));
        res.EnsureSuccessStatusCode();
        var id = (await res.Content.ReadFromJsonAsync<UserDetailDto>())!.Id;
        return (_scenario.Client(Roles.Interviewer, id), id);
    }

    [Fact]
    public async Task An_Interviewer_Sees_The_Rounds_They_Sit_On_And_No_Others()
    {
        var (ivw, ivwId) = await InterviewerAsync();
        var (_, mineApp) = await _scenario.ApplicationAsync("Interviewer Panel A");
        var (_, otherApp) = await _scenario.ApplicationAsync("Interviewer Panel B");
        var mine = await _scenario.ScheduleWithAsync(mineApp, _factory.HiringManagerUserId, ivwId);
        var other = await _scenario.ScheduleAsync(otherApp);

        var list = await ivw.GetFromJsonAsync<List<InterviewListItemDto>>("/api/interviews");
        Assert.Contains(list!, i => i.Id == mine.Id);
        Assert.DoesNotContain(list!, i => i.Id == other.Id);

        Assert.Equal(HttpStatusCode.OK, (await ivw.GetAsync($"/api/interviews/{mine.Id}")).StatusCode);
        // Not theirs: indistinguishable from not existing, like everywhere else in Module 3.
        Assert.Equal(HttpStatusCode.NotFound, (await ivw.GetAsync($"/api/interviews/{other.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await ivw.GetAsync($"/api/applications/{otherApp}/interviews")).StatusCode);
    }

    [Fact]
    public async Task An_Interviewer_Scores_Their_Round_Under_The_Blind_Rule()
    {
        var template = await _scenario.EnsureCompanyTemplateAsync();
        var (ivw, ivwId) = await InterviewerAsync();
        var (_, applicationId) = await _scenario.ApplicationAsync("Interviewer Scoring");
        var round = await _scenario.ScheduleWithAsync(applicationId, _factory.HiringManagerUserId, ivwId);

        // A colleague on the same panel submits first…
        (await _scenario.SalesManager().PostAsJsonAsync($"/api/interviews/{round.Id}/scorecard/submit",
            Module3Scenario.CompleteAnswers(template, recommendation: "Yes"))).EnsureSuccessStatusCode();

        // …and the Interviewer, not yet submitted, cannot read it (ADR-0017 §3).
        var before = await ivw.GetFromJsonAsync<InterviewScorecardsDto>($"/api/interviews/{round.Id}/scorecards");
        Assert.True(before!.BlindedUntilYouSubmit);
        Assert.Empty(before.Visible);

        Assert.Equal(HttpStatusCode.OK, (await ivw.GetAsync($"/api/interviews/{round.Id}/scorecard")).StatusCode);
        var submit = await ivw.PostAsJsonAsync($"/api/interviews/{round.Id}/scorecard/submit",
            Module3Scenario.CompleteAnswers(template, recommendation: "No", rating: 2));
        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);

        var after = await ivw.GetFromJsonAsync<InterviewScorecardsDto>($"/api/interviews/{round.Id}/scorecards");
        Assert.False(after!.BlindedUntilYouSubmit);
        Assert.Equal(2, after.Visible.Count);
    }

    [Fact]
    public async Task A_Later_Round_Of_The_Same_Candidate_Is_Readable_But_Not_Theirs_To_Score()
    {
        // ADR-0017 §4 grants a panel member that one APPLICATION and its rounds, read-only — the
        // same rule every other panel member has had since Module 3. Scoring stays per round:
        // writing an evaluation for a conversation you were not in would be fabricating evidence.
        var template = await _scenario.EnsureCompanyTemplateAsync();
        var (ivw, ivwId) = await InterviewerAsync();
        var (_, applicationId) = await _scenario.ApplicationAsync("Interviewer Second Round");
        await _scenario.ScheduleWithAsync(applicationId, _factory.HiringManagerUserId, ivwId);
        var second = await _scenario.ScheduleAsync(applicationId);

        Assert.Equal(HttpStatusCode.OK, (await ivw.GetAsync($"/api/interviews/{second.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ivw.GetAsync($"/api/interviews/{second.Id}/scorecard")).StatusCode);
        var write = await ivw.PutAsJsonAsync($"/api/interviews/{second.Id}/scorecard",
            Module3Scenario.CompleteAnswers(template, recommendation: "Yes"));
        Assert.Equal(HttpStatusCode.NotFound, write.StatusCode);
    }

    [Fact]
    public async Task An_Interviewer_Reads_And_Writes_The_Notes_Of_Their_Panels_Only()
    {
        // The interview screen embeds the application's notes; the panel talks in the same thread.
        var (ivw, ivwId) = await InterviewerAsync();
        var (_, mineApp) = await _scenario.ApplicationAsync("Interviewer Notes A");
        var (_, otherApp) = await _scenario.ApplicationAsync("Interviewer Notes B");
        await _scenario.ScheduleWithAsync(mineApp, _factory.HiringManagerUserId, ivwId);

        Assert.Equal(HttpStatusCode.OK, (await ivw.GetAsync($"/api/applications/{mineApp}/notes")).StatusCode);
        var post = await ivw.PostAsJsonAsync($"/api/applications/{mineApp}/notes",
            new CreateNoteRequest { Body = "Strong on the system design question." });
        Assert.Equal(HttpStatusCode.OK, post.StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await ivw.GetAsync($"/api/applications/{otherApp}/notes")).StatusCode);
        var stray = await ivw.PostAsJsonAsync($"/api/applications/{otherApp}/notes",
            new CreateNoteRequest { Body = "Should not land." });
        Assert.Equal(HttpStatusCode.NotFound, stray.StatusCode);
    }

    [Fact]
    public async Task An_Interviewer_Cannot_Run_The_Round_Itself()
    {
        var (ivw, ivwId) = await InterviewerAsync();
        var (_, applicationId) = await _scenario.ApplicationAsync("Interviewer Writes");
        var round = await _scenario.ScheduleWithAsync(applicationId, _factory.HiringManagerUserId, ivwId);

        var schedule = await ivw.PostAsJsonAsync($"/api/applications/{applicationId}/interviews",
            new ScheduleInterviewRequest
            {
                ScheduledStart = DateTimeOffset.UtcNow.AddDays(3),
                Mode = "Video",
                ParticipantUserIds = [ivwId],
            });
        var panel = await ivw.PutAsJsonAsync($"/api/interviews/{round.Id}/panel",
            new SetPanelRequest { ParticipantUserIds = [ivwId] });
        var cancel = await ivw.PostAsJsonAsync($"/api/interviews/{round.Id}/cancel", new CancelInterviewRequest());
        var reschedule = await ivw.PutAsJsonAsync($"/api/interviews/{round.Id}",
            new RescheduleInterviewRequest { ScheduledStart = DateTimeOffset.UtcNow.AddDays(5), Mode = "Phone" });
        var complete = await ivw.PostAsJsonAsync($"/api/interviews/{round.Id}/complete", new CompleteInterviewRequest());

        Assert.Equal(HttpStatusCode.Forbidden, complete.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, schedule.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, panel.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, cancel.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, reschedule.StatusCode);
    }

    [Theory]
    [InlineData("/api/departments")]
    [InlineData("/api/jobpostings")]
    [InlineData("/api/search?q=Interviewer")]
    [InlineData("/api/analytics/kpis")]
    [InlineData("/api/delivery")]
    [InlineData("/api/scorecardtemplates")]
    [InlineData("/api/jdtemplates")]
    [InlineData("/api/users/selectable")]
    public async Task Nothing_Else_That_Is_InternalUser_Opens_To_An_Interviewer(string path)
    {
        // The policy that admits them is PanelAccess, on the panel's own endpoints only. Widening
        // InternalUser instead would have opened every one of these.
        var (ivw, _) = await InterviewerAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await ivw.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task An_Interviewer_Reaches_No_Application_Through_The_Pipeline()
    {
        var (ivw, ivwId) = await InterviewerAsync();
        var (_, applicationId) = await _scenario.ApplicationAsync("Interviewer Pipeline");
        await _scenario.ScheduleWithAsync(applicationId, _factory.HiringManagerUserId, ivwId);

        // On the panel, but the pipeline is InternalUser and stays closed: the interview screen is
        // the whole of what this role reaches — the CV included.
        Assert.Equal(HttpStatusCode.Forbidden,
            (await ivw.GetAsync($"/api/applications/{applicationId}/history")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await ivw.GetAsync($"/api/applications/{applicationId}/resume")).StatusCode);
    }

    [Fact]
    public async Task A_Cancelled_Round_Grants_Nothing()
    {
        // A cancelled round's panel can no longer be edited, so a seat on one used to keep the
        // candidate's whole file open for good — someone seated by mistake, then the round called
        // off, read it forever (third security review, 2026-10-19, M1).
        var (ivw, ivwId) = await InterviewerAsync();
        var (_, applicationId) = await _scenario.ApplicationAsync("Interviewer Cancelled");
        var round = await _scenario.ScheduleWithAsync(applicationId, _factory.HiringManagerUserId, ivwId);
        Assert.Equal(HttpStatusCode.OK, (await ivw.GetAsync($"/api/interviews/{round.Id}")).StatusCode);

        (await _scenario.Recruiter().PostAsJsonAsync($"/api/interviews/{round.Id}/cancel",
            new CancelInterviewRequest { Reason = "Wrong interviewer seated." })).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.NotFound, (await ivw.GetAsync($"/api/interviews/{round.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ivw.GetAsync($"/api/applications/{applicationId}/notes")).StatusCode);
        foreach (var query in new[] { "?status=Cancelled", "?status=Cancelled&onlyMine=true" })
        {
            var list = await ivw.GetFromJsonAsync<List<InterviewListItemDto>>($"/api/interviews{query}");
            Assert.DoesNotContain(list!, i => i.Id == round.Id);
        }
    }

    [Fact]
    public async Task A_Recommendation_That_Names_Nothing_Is_Refused()
    {
        // Enum.TryParse accepts "99"; a submitted scorecard can never change, so it would carry a
        // recommendation that is no recommendation for good (third security review, L4).
        var template = await _scenario.EnsureCompanyTemplateAsync();
        var (ivw, ivwId) = await InterviewerAsync();
        var (_, applicationId) = await _scenario.ApplicationAsync("Interviewer Bad Recommendation");
        var round = await _scenario.ScheduleWithAsync(applicationId, _factory.HiringManagerUserId, ivwId);

        var submit = await ivw.PostAsJsonAsync($"/api/interviews/{round.Id}/scorecard/submit",
            Module3Scenario.CompleteAnswers(template, recommendation: "99"));

        Assert.Equal(HttpStatusCode.Conflict, submit.StatusCode);
    }

    [Fact]
    public async Task A_Panel_Member_Of_An_Earlier_Round_Reads_A_Later_Rounds_Submitted_Evaluations()
    {
        // Pins TODAY's rule, not a decision: ADR-0017 §4 opens the whole application to a panel
        // member, and the blind rule (§3) is per round, so a later round's submitted evaluations are
        // readable. Raised with the product owner 2026-10-19 (third security review, L1); if the
        // answer is "own rounds only", this test flips.
        var template = await _scenario.EnsureCompanyTemplateAsync();
        var (ivw, ivwId) = await InterviewerAsync();
        var (_, applicationId) = await _scenario.ApplicationAsync("Interviewer Later Evaluations");
        await _scenario.ScheduleWithAsync(applicationId, _factory.HiringManagerUserId, ivwId);
        var second = await _scenario.ScheduleWithAsync(applicationId, _factory.HiringManagerUserId);
        (await _scenario.SalesManager().PostAsJsonAsync($"/api/interviews/{second.Id}/scorecard/submit",
            Module3Scenario.CompleteAnswers(template, recommendation: "Yes"))).EnsureSuccessStatusCode();

        var seen = await ivw.GetFromJsonAsync<InterviewScorecardsDto>($"/api/interviews/{second.Id}/scorecards");

        Assert.False(seen!.BlindedUntilYouSubmit);
        Assert.Single(seen.Visible);
    }
}
