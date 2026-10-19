using System.Net;
using System.Net.Http.Json;
using RecruitOps.Api.Auth;
using RecruitOps.Application.DTOs;
using Xunit;

namespace RecruitOps.Api.Tests;

/// <summary>The Interviewer role sees and scores the rounds it sits on, and nothing else (ADR-0021
/// amendment, 2026-10-19; product owner's decisions 2026-10-17 and 2026-10-19 — "only their own
/// rounds": not the candidate's other rounds, their evaluations, or the rest of the thread).
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
        var (client, id, _) = await InterviewerWithHandleAsync();
        return (client, id);
    }

    /// <summary>Also returns the user's @mention handle — the email local-part, unique per call.</summary>
    private async Task<(HttpClient Client, Guid UserId, string Handle)> InterviewerWithHandleAsync()
    {
        var handle = $"ivw.{Guid.NewGuid():N}";
        var res = await _scenario.Client(Roles.Admin, _factory.AdminUserId).PostAsJsonAsync("/api/users",
            new CreateUserRequest($"{handle}@alpha.test", "Ivy Interviewer", "Correct-Horse-9",
                Role: "Interviewer"));
        res.EnsureSuccessStatusCode();
        var id = (await res.Content.ReadFromJsonAsync<UserDetailDto>())!.Id;
        return (_scenario.Client(Roles.Interviewer, id), id, handle);
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
    public async Task Another_Round_Of_The_Same_Candidate_Is_Not_Theirs_At_All()
    {
        // Product owner, 2026-10-19: an Interviewer sees only the rounds it sits on. ADR-0017 §4
        // opens the whole application to every other panel member; for this role a seat is the
        // round — not its siblings, not their evaluations (which the per-round blind rule would
        // not hide), not their place in the list.
        var template = await _scenario.EnsureCompanyTemplateAsync();
        var (ivw, ivwId) = await InterviewerAsync();
        var (_, applicationId) = await _scenario.ApplicationAsync("Interviewer Second Round");
        var mine = await _scenario.ScheduleWithAsync(applicationId, _factory.HiringManagerUserId, ivwId);
        var second = await _scenario.ScheduleWithAsync(applicationId, _factory.HiringManagerUserId);
        (await _scenario.SalesManager().PostAsJsonAsync($"/api/interviews/{second.Id}/scorecard/submit",
            Module3Scenario.CompleteAnswers(template, recommendation: "Yes"))).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.NotFound, (await ivw.GetAsync($"/api/interviews/{second.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ivw.GetAsync($"/api/interviews/{second.Id}/scorecards")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ivw.GetAsync($"/api/interviews/{second.Id}/scorecard")).StatusCode);
        var write = await ivw.PutAsJsonAsync($"/api/interviews/{second.Id}/scorecard",
            Module3Scenario.CompleteAnswers(template, recommendation: "Yes"));
        Assert.Equal(HttpStatusCode.NotFound, write.StatusCode);

        foreach (var query in new[] { "", "?status=Scheduled&status=Completed" })
        {
            var list = await ivw.GetFromJsonAsync<List<InterviewListItemDto>>($"/api/interviews{query}");
            Assert.Contains(list!, i => i.Id == mine.Id);
            Assert.DoesNotContain(list!, i => i.Id == second.Id);
        }
        var rounds = await ivw.GetFromJsonAsync<List<InterviewDto>>($"/api/applications/{applicationId}/interviews");
        Assert.Equal(new[] { mine.Id }, rounds!.Select(r => r.Id));
    }

    [Fact]
    public async Task Other_Roles_Keep_The_Application_Wide_Seat()
    {
        // The narrowing is the Interviewer's alone: a Hiring Manager from another department, on
        // round 1's panel, still reads round 2 (ADR-0017 §4, unchanged).
        var (_, applicationId) = await _scenario.ApplicationAsync("Interviewer Narrowing Is Role Specific");
        await _scenario.ScheduleAsync(applicationId); // Finance's manager sits on round 1
        var second = await _scenario.ScheduleWithAsync(applicationId, _factory.HiringManagerUserId);

        Assert.Equal(HttpStatusCode.OK, (await _scenario.FinanceManager().GetAsync($"/api/interviews/{second.Id}")).StatusCode);
    }

    [Fact]
    public async Task An_Interviewer_Reads_And_Writes_Only_The_Notes_Of_Their_Round()
    {
        // The interview screen shows the notes pinned to its round. It always filtered in the page;
        // the response used to carry the candidate's whole thread. Now the API sends only those.
        var (ivw, ivwId) = await InterviewerAsync();
        var (_, mineApp) = await _scenario.ApplicationAsync("Interviewer Notes A");
        var (_, otherApp) = await _scenario.ApplicationAsync("Interviewer Notes B");
        var mine = await _scenario.ScheduleWithAsync(mineApp, _factory.HiringManagerUserId, ivwId);
        var sibling = await _scenario.ScheduleWithAsync(mineApp, _factory.HiringManagerUserId);

        // Recruiter notes: one on the thread, one on the sibling round, one on the Interviewer's round.
        foreach (var (body, round) in new (string, Guid?)[]
                 { ("Thread-wide note.", null), ("About round two.", sibling.Id), ("About your round.", mine.Id) })
            (await _scenario.Recruiter().PostAsJsonAsync($"/api/applications/{mineApp}/notes",
                new CreateNoteRequest { Body = body, InterviewId = round })).EnsureSuccessStatusCode();

        var seen = await ivw.GetFromJsonAsync<List<NoteDto>>($"/api/applications/{mineApp}/notes");
        Assert.Equal(new[] { "About your round." }, seen!.Select(n => n.Body));

        var post = await ivw.PostAsJsonAsync($"/api/applications/{mineApp}/notes",
            new CreateNoteRequest { Body = "Strong on the system design question.", InterviewId = mine.Id });
        Assert.Equal(HttpStatusCode.OK, post.StatusCode);
        // Unpinned would land on the thread it cannot read; another round is not theirs.
        var unpinned = await ivw.PostAsJsonAsync($"/api/applications/{mineApp}/notes",
            new CreateNoteRequest { Body = "To everyone." });
        var onSibling = await ivw.PostAsJsonAsync($"/api/applications/{mineApp}/notes",
            new CreateNoteRequest { Body = "About round two.", InterviewId = sibling.Id });
        Assert.Equal(HttpStatusCode.Conflict, unpinned.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, onSibling.StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await ivw.GetAsync($"/api/applications/{otherApp}/notes")).StatusCode);
        var stray = await ivw.PostAsJsonAsync($"/api/applications/{otherApp}/notes",
            new CreateNoteRequest { Body = "Should not land.", InterviewId = mine.Id });
        Assert.Equal(HttpStatusCode.NotFound, stray.StatusCode); // no reach answers first: nothing to learn
    }

    [Fact]
    public async Task A_Mention_Reaches_An_Interviewer_Only_On_A_Note_They_Can_Open()
    {
        var (_, ivwId, handle) = await InterviewerWithHandleAsync();
        var (_, applicationId) = await _scenario.ApplicationAsync("Interviewer Mentions");
        var mine = await _scenario.ScheduleWithAsync(applicationId, _factory.HiringManagerUserId, ivwId);
        var sibling = await _scenario.ScheduleWithAsync(applicationId, _factory.HiringManagerUserId);

        async Task<NoteDto> PostAsync(Guid? round)
        {
            var res = await _scenario.Recruiter().PostAsJsonAsync($"/api/applications/{applicationId}/notes",
                new CreateNoteRequest { Body = $"@{handle} what did you make of this?", InterviewId = round });
            res.EnsureSuccessStatusCode();
            return (await res.Content.ReadFromJsonAsync<NoteDto>())!;
        }

        Assert.Contains((await PostAsync(mine.Id)).Mentions, m => m.UserId == ivwId);
        Assert.DoesNotContain((await PostAsync(sibling.Id)).Mentions, m => m.UserId == ivwId);
        Assert.DoesNotContain((await PostAsync(null)).Mentions, m => m.UserId == ivwId);
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

}
