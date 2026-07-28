using System.Net;
using System.Net.Http.Json;
using RecruitOps.Api.Auth;
using RecruitOps.Application.DTOs;
using Xunit;

namespace RecruitOps.Api.Tests;

/// <summary>Module 3 — scheduling, panels, and the access those panels grant.
///
/// <para>The two rules worth breaking the build over are here: scheduling must leave a
/// stage-history row behind (ADR-0017 §5), and a panel member from another department must
/// reach this application <em>and nothing else</em> (§4).</para>
/// </summary>
public class InterviewFlowTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;
    private readonly Module3Scenario _scenario;

    public InterviewFlowTests(CustomWebAppFactory factory)
    {
        _factory = factory;
        _scenario = new Module3Scenario(factory);
    }

    [Fact]
    public async Task Scheduling_Moves_The_Application_To_Interview_And_Records_The_Move()
    {
        var (_, applicationId) = await _scenario.ApplicationAsync("Sales Engineer");

        var interview = await _scenario.ScheduleAsync(applicationId);

        Assert.Equal("Scheduled", interview.Status);
        Assert.Equal(1, interview.Round);
        Assert.Equal("Video", interview.Mode);
        Assert.Equal(2, interview.Participants.Count);

        // The point of §5: the stage move rides in the same transaction, so it is impossible
        // to have an interview against an application still sitting at Applied. Module 5
        // computes time-to-interview from exactly this row and cannot reconstruct it later.
        var history = await _scenario.Recruiter()
            .GetFromJsonAsync<List<StageHistoryItemDto>>($"/api/applications/{applicationId}/history");

        Assert.Contains(history!, h => h.ToStatus == "Interview");
        Assert.Equal("Interview", history!.Last().ToStatus);
    }

    [Fact]
    public async Task Scheduling_A_Second_Round_Numbers_It_And_Does_Not_Duplicate_History()
    {
        var (_, applicationId) = await _scenario.ApplicationAsync("Sales Engineer II");

        await _scenario.ScheduleAsync(applicationId);
        var second = await _scenario.ScheduleAsync(applicationId);

        Assert.Equal(2, second.Round);

        // Already at Interview, so the second round must NOT write another history row —
        // a no-op transition would be counted by Module 5 as a real stage change.
        var history = await _scenario.Recruiter()
            .GetFromJsonAsync<List<StageHistoryItemDto>>($"/api/applications/{applicationId}/history");

        Assert.Single(history!, h => h.ToStatus == "Interview");
    }

    [Fact]
    public async Task A_Panel_Member_From_Another_Department_Reaches_The_Application_Only_Once_On_The_Panel()
    {
        var (_, applicationId) = await _scenario.ApplicationAsync("Sales Engineer III");

        // Before: the Finance manager has no business seeing a Sales application, and the
        // answer is 404 rather than 403 so its existence isn't leaked.
        var before = await _scenario.FinanceManager()
            .GetAsync($"/api/applications/{applicationId}/interviews");
        Assert.Equal(HttpStatusCode.NotFound, before.StatusCode);

        await _scenario.ScheduleAsync(applicationId);

        // After: participation is the grant (ADR-0017 §4). Without this, the most ordinary
        // panel there is — HR plus a lead from another team — cannot be run at all.
        var after = await _scenario.FinanceManager()
            .GetFromJsonAsync<List<InterviewDto>>($"/api/applications/{applicationId}/interviews");

        Assert.Single(after!);
    }

    [Fact]
    public async Task Participation_Grants_Nothing_Beyond_That_One_Application()
    {
        var (_, panelApplicationId) = await _scenario.ApplicationAsync("Sales Engineer IV");
        var (_, otherApplicationId) = await _scenario.ApplicationAsync("Sales Engineer V");

        await _scenario.ScheduleAsync(panelApplicationId);

        // On one panel, so that application is readable...
        var reachable = await _scenario.FinanceManager()
            .GetAsync($"/api/applications/{panelApplicationId}/interviews");
        Assert.Equal(HttpStatusCode.OK, reachable.StatusCode);

        // ...and the neighbouring Sales application is still none of their business. This is
        // the line between "participation grants access" and "participation grants the
        // department", and it is the whole reason the grant is scoped to a single row.
        var unreachable = await _scenario.FinanceManager()
            .GetAsync($"/api/applications/{otherApplicationId}/interviews");
        Assert.Equal(HttpStatusCode.NotFound, unreachable.StatusCode);
    }

    [Fact]
    public async Task A_Panel_Member_Cannot_Reschedule_Or_Cancel()
    {
        var (_, applicationId) = await _scenario.ApplicationAsync("Sales Engineer VI");
        var interview = await _scenario.ScheduleAsync(applicationId);

        // Reading the round and running it are different rights. Even the Sales manager,
        // who owns this department, does not reschedule — that is a recruiter's job.
        var reschedule = await _scenario.SalesManager()
            .PutAsJsonAsync($"/api/interviews/{interview.Id}", new RescheduleInterviewRequest
            {
                ScheduledStart = new DateTimeOffset(2026, 8, 4, 9, 0, 0, TimeSpan.Zero),
                Mode = "Video",
            });
        Assert.Equal(HttpStatusCode.Forbidden, reschedule.StatusCode);

        var cancel = await _scenario.FinanceManager()
            .PostAsJsonAsync($"/api/interviews/{interview.Id}/cancel", new CancelInterviewRequest());
        Assert.Equal(HttpStatusCode.Forbidden, cancel.StatusCode);
    }

    [Fact]
    public async Task An_Interview_Needs_A_Panel_And_A_Lead_Who_Is_On_It()
    {
        var (_, applicationId) = await _scenario.ApplicationAsync("Sales Engineer VII");

        var noPanel = await _scenario.Recruiter().PostAsJsonAsync(
            $"/api/applications/{applicationId}/interviews", new ScheduleInterviewRequest
            {
                ScheduledStart = DateTimeOffset.UtcNow.AddDays(3),
                Mode = "Phone",
                ParticipantUserIds = Array.Empty<Guid>(),
            });

        // Model validation catches the empty list before the service does; either way this
        // must not produce an interview nobody can score.
        Assert.True(noPanel.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict);

        var strayLead = await _scenario.Recruiter().PostAsJsonAsync(
            $"/api/applications/{applicationId}/interviews", new ScheduleInterviewRequest
            {
                ScheduledStart = DateTimeOffset.UtcNow.AddDays(3),
                Mode = "Phone",
                ParticipantUserIds = new[] { _factory.HiringManagerUserId },
                LeadUserId = _factory.FinanceManagerUserId,
            });

        Assert.Equal(HttpStatusCode.Conflict, strayLead.StatusCode);
    }

    [Fact]
    public async Task An_Unknown_Interviewer_Is_A_Conflict_Not_A_Silent_Skip()
    {
        var (_, applicationId) = await _scenario.ApplicationAsync("Sales Engineer VIII");

        var res = await _scenario.Recruiter().PostAsJsonAsync(
            $"/api/applications/{applicationId}/interviews", new ScheduleInterviewRequest
            {
                ScheduledStart = DateTimeOffset.UtcNow.AddDays(3),
                Mode = "OnSite",
                Location = "Room 2",
                ParticipantUserIds = new[] { _factory.HiringManagerUserId, Guid.NewGuid() },
            });

        // Dropping the unknown id quietly would be discovered on the day, by an empty chair.
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task A_Rejected_Application_Cannot_Be_Scheduled_Against()
    {
        var (_, applicationId) = await _scenario.ApplicationAsync("Sales Engineer IX");

        await _scenario.Recruiter().PostAsJsonAsync($"/api/applications/{applicationId}/stage",
            new MoveStageRequest { ToStatus = "Rejected" });

        var res = await _scenario.Recruiter().PostAsJsonAsync(
            $"/api/applications/{applicationId}/interviews", new ScheduleInterviewRequest
            {
                ScheduledStart = DateTimeOffset.UtcNow.AddDays(3),
                Mode = "Phone",
                ParticipantUserIds = new[] { _factory.HiringManagerUserId },
            });

        // Terminal is terminal everywhere, not only in PipelineService. Otherwise scheduling
        // becomes a side door that drags a closed application back into the pipeline.
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task A_Cancelled_Round_Cannot_Be_Rescheduled_And_Leaves_The_Stage_Alone()
    {
        var (_, applicationId) = await _scenario.ApplicationAsync("Sales Engineer X");
        var interview = await _scenario.ScheduleAsync(applicationId);

        var cancelled = await _scenario.Recruiter().PostAsJsonAsync(
            $"/api/interviews/{interview.Id}/cancel",
            new CancelInterviewRequest { Reason = "Candidate withdrew for now." });
        cancelled.EnsureSuccessStatusCode();

        var dto = (await cancelled.Content.ReadFromJsonAsync<InterviewDto>())!;
        Assert.Equal("Cancelled", dto.Status);

        var again = await _scenario.Recruiter().PutAsJsonAsync(
            $"/api/interviews/{interview.Id}", new RescheduleInterviewRequest
            {
                ScheduledStart = DateTimeOffset.UtcNow.AddDays(5),
                Mode = "Phone",
            });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);

        // The application stays at Interview: calling off a round does not undo the decision
        // to interview, and rewinding would claim the candidate moved backwards.
        var history = await _scenario.Recruiter()
            .GetFromJsonAsync<List<StageHistoryItemDto>>($"/api/applications/{applicationId}/history");
        Assert.Equal("Interview", history!.Last().ToStatus);
    }

    [Fact]
    public async Task An_Interviewer_Who_Has_Started_Scoring_Cannot_Be_Dropped_From_The_Panel()
    {
        var template = await _scenario.EnsureCompanyTemplateAsync();
        var (_, applicationId) = await _scenario.ApplicationAsync("Sales Engineer XI");
        var interview = await _scenario.ScheduleAsync(applicationId);

        var draft = await _scenario.FinanceManager().PutAsJsonAsync(
            $"/api/interviews/{interview.Id}/scorecard",
            Module3Scenario.CompleteAnswers(template, recommendation: "Yes"));
        draft.EnsureSuccessStatusCode();

        var res = await _scenario.Recruiter().PutAsJsonAsync(
            $"/api/interviews/{interview.Id}/panel", new SetPanelRequest
            {
                ParticipantUserIds = new[] { _factory.HiringManagerUserId },
                LeadUserId = _factory.HiringManagerUserId,
            });

        // Removing them would delete their assessment along with the access that produced
        // it. An audit trail you can quietly discard is not one.
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task An_Interview_On_Someone_Elses_Application_Is_A_404_Not_A_403()
    {
        var (_, applicationId) = await _scenario.ApplicationAsync("Sales Engineer XII");

        var res = await _scenario.FinanceManager()
            .GetAsync($"/api/applications/{applicationId}/interviews");

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task An_Unauthenticated_Caller_Gets_401()
    {
        var (_, applicationId) = await _scenario.ApplicationAsync("Sales Engineer XIII");

        var res = await _factory.CreateClient()
            .GetAsync($"/api/applications/{applicationId}/interviews");

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Completing_A_Round_Can_Record_A_No_Show_Distinctly()
    {
        var (_, applicationId) = await _scenario.ApplicationAsync("Sales Engineer XIV");
        var interview = await _scenario.ScheduleAsync(applicationId);

        var res = await _scenario.Recruiter().PostAsJsonAsync(
            $"/api/interviews/{interview.Id}/complete",
            new CompleteInterviewRequest { NoShow = true });
        res.EnsureSuccessStatusCode();

        var dto = (await res.Content.ReadFromJsonAsync<InterviewDto>())!;

        // "Nobody turned up" and "it happened" are different facts, and Module 5 will want
        // to tell them apart. Collapsing both into Completed loses that permanently.
        Assert.Equal("NoShow", dto.Status);
    }
}
