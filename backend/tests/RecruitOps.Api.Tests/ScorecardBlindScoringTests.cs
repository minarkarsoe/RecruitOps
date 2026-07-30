using System.Net;
using System.Net.Http.Json;
using RecruitOps.Application.DTOs;
using Xunit;

namespace RecruitOps.Api.Tests;

/// <summary>Module 3.3 — evaluations, and the rule that makes a panel worth having.
///
/// <para>Blind scoring (ADR-0017 §3) is an authorization rule, not a UI affordance: a hidden
/// element in the SPA is a decoration, and this API is directly reachable. Everything here
/// is asserted over HTTP for that reason.</para>
/// </summary>
public class ScorecardBlindScoringTests : IClassFixture<CustomWebAppFactory>
{
    private readonly Module3Scenario _scenario;

    public ScorecardBlindScoringTests(CustomWebAppFactory factory)
        => _scenario = new Module3Scenario(factory);

    private async Task<(ScorecardTemplateDto Template, InterviewDto Interview)> ReadyAsync(string title)
    {
        var template = await _scenario.EnsureCompanyTemplateAsync();
        var (_, applicationId) = await _scenario.ApplicationAsync(title);
        var interview = await _scenario.ScheduleAsync(applicationId);
        return (template, interview);
    }

    [Fact]
    public async Task An_Interview_Picks_Up_The_Resolved_Template_And_Offers_Its_Criteria()
    {
        var (template, interview) = await ReadyAsync("Scorecard Role A");

        Assert.Equal(template.Id, interview.ScorecardTemplateId);

        var mine = await _scenario.SalesManager()
            .GetFromJsonAsync<MyScorecardDto>($"/api/interviews/{interview.Id}/scorecard");

        Assert.Equal(template.Criteria.Count, mine!.Criteria.Count);
        // Nothing started yet — the form renders, the evaluation does not exist.
        Assert.Null(mine.Scorecard);
    }

    [Fact]
    public async Task A_Panel_Member_Cannot_See_A_Colleagues_Submitted_Score_Until_They_Submit_Their_Own()
    {
        var (template, interview) = await ReadyAsync("Scorecard Role B");

        var submitted = await _scenario.FinanceManager().PostAsJsonAsync(
            $"/api/interviews/{interview.Id}/scorecard/submit",
            Module3Scenario.CompleteAnswers(template, recommendation: "StrongYes", rating: 5));
        submitted.EnsureSuccessStatusCode();

        // The Sales manager has written nothing yet. Letting them read "Strong Yes, 5/5"
        // first is how four independent observations collapse into one repeated opinion.
        var blind = await _scenario.SalesManager()
            .GetFromJsonAsync<InterviewScorecardsDto>($"/api/interviews/{interview.Id}/scorecards");

        Assert.True(blind!.BlindedUntilYouSubmit);
        Assert.Empty(blind.Visible);
        Assert.Equal(1, blind.HiddenCount);

        var mine = await _scenario.SalesManager().PostAsJsonAsync(
            $"/api/interviews/{interview.Id}/scorecard/submit",
            Module3Scenario.CompleteAnswers(template, recommendation: "No", rating: 2));
        mine.EnsureSuccessStatusCode();

        var open = await _scenario.SalesManager()
            .GetFromJsonAsync<InterviewScorecardsDto>($"/api/interviews/{interview.Id}/scorecards");

        Assert.False(open!.BlindedUntilYouSubmit);
        Assert.Equal(2, open.Visible.Count);
        Assert.Equal(0, open.HiddenCount);

        // And the disagreement survives, which is the entire value of collecting them apart.
        Assert.Contains(open.Visible, s => s.Recommendation == "StrongYes");
        Assert.Contains(open.Visible, s => s.Recommendation == "No");
    }

    [Fact]
    public async Task A_Recruiter_Who_Is_Not_On_The_Panel_Sees_Submitted_Scores_Immediately()
    {
        var (template, interview) = await ReadyAsync("Scorecard Role C");

        await _scenario.FinanceManager().PostAsJsonAsync(
            $"/api/interviews/{interview.Id}/scorecard/submit",
            Module3Scenario.CompleteAnswers(template, recommendation: "Yes"));

        var view = await _scenario.Recruiter()
            .GetFromJsonAsync<InterviewScorecardsDto>($"/api/interviews/{interview.Id}/scorecards");

        // The rule keys on participation, not on reach. A recruiter isn't writing an
        // assessment, so there is nothing to anchor — and blinding them would lock them out
        // of their own pipeline for no benefit.
        Assert.False(view!.BlindedUntilYouSubmit);
        Assert.Single(view.Visible);
    }

    [Fact]
    public async Task A_Draft_Is_Visible_To_Its_Author_And_To_Nobody_Else()
    {
        var (template, interview) = await ReadyAsync("Scorecard Role D");

        var draft = await _scenario.FinanceManager().PutAsJsonAsync(
            $"/api/interviews/{interview.Id}/scorecard",
            Module3Scenario.CompleteAnswers(template, recommendation: "Yes"));
        draft.EnsureSuccessStatusCode();

        var author = await _scenario.FinanceManager()
            .GetFromJsonAsync<MyScorecardDto>($"/api/interviews/{interview.Id}/scorecard");
        Assert.Equal("Draft", author!.Scorecard!.Status);

        // An unfinished evaluation is not an opinion yet — not even for a company-wide role.
        var recruiter = await _scenario.Recruiter()
            .GetFromJsonAsync<InterviewScorecardsDto>($"/api/interviews/{interview.Id}/scorecards");
        Assert.Empty(recruiter!.Visible);
        Assert.Equal(0, recruiter.HiddenCount);
    }

    [Fact]
    public async Task A_Submitted_Scorecard_Cannot_Be_Revised()
    {
        var (template, interview) = await ReadyAsync("Scorecard Role E");

        await _scenario.SalesManager().PostAsJsonAsync(
            $"/api/interviews/{interview.Id}/scorecard/submit",
            Module3Scenario.CompleteAnswers(template, recommendation: "Yes"));

        var again = await _scenario.SalesManager().PutAsJsonAsync(
            $"/api/interviews/{interview.Id}/scorecard",
            Module3Scenario.CompleteAnswers(template, recommendation: "StrongNo", rating: 1));

        // An evaluation that can be rewritten after reading the panel's is not blind; it
        // just delays the anchoring by one request.
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Submitting_Requires_Every_Required_Criterion_And_A_Recommendation()
    {
        var (template, interview) = await ReadyAsync("Scorecard Role F");

        var incomplete = await _scenario.SalesManager().PostAsJsonAsync(
            $"/api/interviews/{interview.Id}/scorecard/submit", new SaveScorecardRequest
            {
                Recommendation = "Yes",
                Answers = new List<ScorecardAnswerInput>
                {
                    new() { ScorecardCriterionId = template.Criteria[0].Id, Rating = 4 },
                },
            });
        Assert.Equal(HttpStatusCode.Conflict, incomplete.StatusCode);

        var noRecommendation = await _scenario.SalesManager().PostAsJsonAsync(
            $"/api/interviews/{interview.Id}/scorecard/submit",
            Module3Scenario.CompleteAnswers(template, recommendation: string.Empty));
        Assert.Equal(HttpStatusCode.Conflict, noRecommendation.StatusCode);

        // Neither attempt may leave a half-submitted row behind.
        var mine = await _scenario.SalesManager()
            .GetFromJsonAsync<MyScorecardDto>($"/api/interviews/{interview.Id}/scorecard");
        Assert.True(mine!.Scorecard is null || mine.Scorecard.Status == "Draft");
    }

    [Fact]
    public async Task A_Draft_Can_Be_Incomplete()
    {
        var (template, interview) = await ReadyAsync("Scorecard Role G");

        var res = await _scenario.SalesManager().PutAsJsonAsync(
            $"/api/interviews/{interview.Id}/scorecard", new SaveScorecardRequest
            {
                Answers = new List<ScorecardAnswerInput>
                {
                    new() { ScorecardCriterionId = template.Criteria[0].Id, Rating = 3 },
                },
            });

        // Half-finished is the normal state of a scorecard during the interview itself.
        res.EnsureSuccessStatusCode();
        var mine = (await res.Content.ReadFromJsonAsync<MyScorecardDto>())!;
        Assert.Equal("Draft", mine.Scorecard!.Status);
        Assert.Single(mine.Scorecard.Responses);
    }

    [Fact]
    public async Task Someone_Who_Was_Not_In_The_Room_Cannot_Write_A_Scorecard()
    {
        var (template, interview) = await ReadyAsync("Scorecard Role H");

        var res = await _scenario.Recruiter().PutAsJsonAsync(
            $"/api/interviews/{interview.Id}/scorecard",
            Module3Scenario.CompleteAnswers(template, recommendation: "Yes"));

        // The recruiter can read this interview. Writing an assessment of a conversation
        // they were not in would be fabricating evidence, so reach is not enough.
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task An_Answer_To_A_Criterion_That_Is_Not_On_The_Template_Is_Dropped()
    {
        var (template, interview) = await ReadyAsync("Scorecard Role I");

        var request = Module3Scenario.CompleteAnswers(template, recommendation: "Yes");
        var withStray = request with
        {
            Answers = request.Answers
                .Append(new ScorecardAnswerInput { ScorecardCriterionId = Guid.NewGuid(), Rating = 5 })
                .ToList(),
        };

        var res = await _scenario.SalesManager().PostAsJsonAsync(
            $"/api/interviews/{interview.Id}/scorecard/submit", withStray);
        res.EnsureSuccessStatusCode();

        var mine = (await res.Content.ReadFromJsonAsync<MyScorecardDto>())!;

        // Answers are rebuilt from the template rather than stored as sent — the same
        // defence ApplicationFormSchema applies to anonymous applicants' answers.
        Assert.Equal(template.Criteria.Count, mine.Scorecard!.Responses.Count);
    }

    [Fact]
    public async Task A_Rating_Outside_One_To_Five_Is_Refused()
    {
        var (template, interview) = await ReadyAsync("Scorecard Role J");

        var res = await _scenario.SalesManager().PutAsJsonAsync(
            $"/api/interviews/{interview.Id}/scorecard", new SaveScorecardRequest
            {
                Answers = new List<ScorecardAnswerInput>
                {
                    new() { ScorecardCriterionId = template.Criteria[0].Id, Rating = 9 },
                },
            });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task A_Submitted_Answer_Keeps_The_Wording_It_Was_Answered_Against()
    {
        var (template, interview) = await ReadyAsync("Scorecard Role K");

        var submit = await _scenario.SalesManager().PostAsJsonAsync(
            $"/api/interviews/{interview.Id}/scorecard/submit",
            Module3Scenario.CompleteAnswers(template, recommendation: "Yes"));
        submit.EnsureSuccessStatusCode();

        var mine = (await submit.Content.ReadFromJsonAsync<MyScorecardDto>())!;

        // The snapshot (ADR-0017 §2). Renaming the criterion later must not retroactively
        // change what this person was asked — otherwise old scores quietly stop meaning
        // what they meant.
        Assert.Contains(mine.Scorecard!.Responses, r => r.CriterionLabel == "Communication");
    }

    [Fact]
    public async Task A_Cancelled_Round_Cannot_Be_Scored()
    {
        var (template, interview) = await ReadyAsync("Scorecard Role L");

        await _scenario.Recruiter().PostAsJsonAsync(
            $"/api/interviews/{interview.Id}/cancel",
            new CancelInterviewRequest { Reason = "Called off." });

        var res = await _scenario.SalesManager().PutAsJsonAsync(
            $"/api/interviews/{interview.Id}/scorecard",
            Module3Scenario.CompleteAnswers(template, recommendation: "Yes"));

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }
}
