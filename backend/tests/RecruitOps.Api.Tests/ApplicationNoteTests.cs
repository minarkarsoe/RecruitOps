using System.Net;
using System.Net.Http.Json;
using RecruitOps.Application.DTOs;
using Xunit;

namespace RecruitOps.Api.Tests;

/// <summary>Module 3.4 — collaborative notes and @mentions.
///
/// <para>Two things are being defended here. A mention must be derived from the text rather
/// than taken from the request, or a note can be forged to look addressed to a colleague.
/// And a mention must not reach someone who cannot see the application, or "@finance.approver
/// what do you think of this candidate" becomes a disclosure channel.</para>
/// </summary>
public class ApplicationNoteTests : IClassFixture<CustomWebAppFactory>
{
    private readonly Module3Scenario _scenario;

    public ApplicationNoteTests(CustomWebAppFactory factory)
        => _scenario = new Module3Scenario(factory);

    private async Task<NoteDto> PostAsync(HttpClient client, Guid applicationId, string body)
    {
        var res = await client.PostAsJsonAsync(
            $"/api/applications/{applicationId}/notes", new CreateNoteRequest { Body = body });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<NoteDto>())!;
    }

    [Fact]
    public async Task A_Mention_Is_Resolved_From_The_Text_And_Marked_Up()
    {
        var (_, applicationId) = await _scenario.ApplicationAsync("Note Role A");

        var note = await PostAsync(
            _scenario.Recruiter(), applicationId, "@sales.manager can you sit on this panel?");

        Assert.Single(note.Mentions);
        Assert.Equal("Sales Manager", note.Mentions[0].DisplayName);
        Assert.Contains("class=\"mention\"", note.BodyHtml);
        Assert.Contains(note.Mentions[0].UserId.ToString(), note.BodyHtml);
    }

    [Fact]
    public async Task Markup_In_The_Body_Is_Escaped_Not_Rendered()
    {
        var (_, applicationId) = await _scenario.ApplicationAsync("Note Role B");

        var note = await PostAsync(
            _scenario.Recruiter(), applicationId,
            "<script>alert('x')</script> strong on delivery, @sales.manager");

        // The raw body is preserved — mangling what someone typed is not a security control.
        Assert.Contains("<script>", note.Body);

        // What is rendered is escaped, and only the markup this server generated survives.
        Assert.DoesNotContain("<script>", note.BodyHtml);
        Assert.Contains("&lt;script&gt;", note.BodyHtml);
        Assert.Contains("class=\"mention\"", note.BodyHtml);
    }

    [Fact]
    public async Task A_Mention_Of_Someone_Who_Cannot_See_The_Application_Is_Not_Recorded()
    {
        var (_, applicationId) = await _scenario.ApplicationAsync("Note Role C");

        var note = await PostAsync(
            _scenario.Recruiter(), applicationId, "@finance.manager thoughts on this one?");

        // The Finance manager has no reach into a Sales application. Recording the mention
        // would put a candidate's name and a judgement in front of them — and, once Module 7
        // delivers notifications, mail it to them.
        Assert.Empty(note.Mentions);
        Assert.DoesNotContain("class=\"mention\"", note.BodyHtml);
    }

    [Fact]
    public async Task A_Mention_Of_An_Approver_Is_Not_Recorded()
    {
        var (_, applicationId) = await _scenario.ApplicationAsync("Note Role C2");

        // The regression this file's own summary described and did not test. The old check
        // read `role is UserRole.HiringManager` and returned true for everything else, so
        // this exact handle — the one named in the doc comment as the thing being prevented —
        // resolved. Module 7 would have mailed a candidate's name and a judgement to Finance.
        var note = await PostAsync(
            _scenario.Recruiter(), applicationId, "@finance.approver can we afford this one?");

        Assert.Empty(note.Mentions);
        Assert.DoesNotContain("class=\"mention\"", note.BodyHtml);
    }

    [Fact]
    public async Task Putting_Someone_On_The_Panel_Makes_Them_Mentionable()
    {
        var (_, applicationId) = await _scenario.ApplicationAsync("Note Role D");
        await _scenario.ScheduleAsync(applicationId);

        var note = await PostAsync(
            _scenario.Recruiter(), applicationId, "@finance.manager thoughts on this one?");

        // Participation is the grant (ADR-0017 §4) — and an interviewer from another
        // department is exactly the person you most want to be able to tag.
        Assert.Single(note.Mentions);
        Assert.Equal("Finance Manager", note.Mentions[0].DisplayName);
    }

    [Fact]
    public async Task A_Panel_Member_Can_Read_And_Join_The_Thread()
    {
        var (_, applicationId) = await _scenario.ApplicationAsync("Note Role E");

        var before = await _scenario.FinanceManager()
            .GetAsync($"/api/applications/{applicationId}/notes");
        Assert.Equal(HttpStatusCode.NotFound, before.StatusCode);

        await _scenario.ScheduleAsync(applicationId);
        await PostAsync(_scenario.Recruiter(), applicationId, "Panel confirmed for Monday.");

        var thread = await _scenario.FinanceManager()
            .GetFromJsonAsync<List<NoteDto>>($"/api/applications/{applicationId}/notes");
        Assert.Single(thread!);

        var mine = await PostAsync(
            _scenario.FinanceManager(), applicationId, "Happy to cover the technical half.");
        Assert.Equal("Finance Manager", mine.AuthorName);
    }

    [Fact]
    public async Task An_Unmatched_Handle_Stays_Plain_Text()
    {
        var (_, applicationId) = await _scenario.ApplicationAsync("Note Role F");

        var note = await PostAsync(
            _scenario.Recruiter(), applicationId, "Reached them at nobody@external.example.");

        // An unmatched "@" is far more often an email address than a mistake worth flagging.
        Assert.Empty(note.Mentions);
        Assert.DoesNotContain("class=\"mention\"", note.BodyHtml);
    }

    [Fact]
    public async Task A_Note_Cannot_Be_Pinned_To_Another_Applications_Interview()
    {
        var (_, applicationId) = await _scenario.ApplicationAsync("Note Role G");
        var (_, otherApplicationId) = await _scenario.ApplicationAsync("Note Role H");
        var elsewhere = await _scenario.ScheduleAsync(otherApplicationId);

        var res = await _scenario.Recruiter().PostAsJsonAsync(
            $"/api/applications/{applicationId}/notes",
            new CreateNoteRequest { Body = "Debrief.", InterviewId = elsewhere.Id });

        // Otherwise the interview id becomes a way to attach commentary to a candidate the
        // author has no access to.
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task Notes_On_An_Application_Out_Of_Reach_Are_A_404()
    {
        var (_, applicationId) = await _scenario.ApplicationAsync("Note Role I");

        var read = await _scenario.FinanceManager()
            .GetAsync($"/api/applications/{applicationId}/notes");
        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);

        var write = await _scenario.FinanceManager().PostAsJsonAsync(
            $"/api/applications/{applicationId}/notes", new CreateNoteRequest { Body = "Hello." });
        Assert.Equal(HttpStatusCode.NotFound, write.StatusCode);
    }

    [Fact]
    public async Task The_Thread_Comes_Back_Oldest_First()
    {
        var (_, applicationId) = await _scenario.ApplicationAsync("Note Role J");

        await PostAsync(_scenario.Recruiter(), applicationId, "First.");
        await PostAsync(_scenario.Recruiter(), applicationId, "Second.");

        var thread = await _scenario.Recruiter()
            .GetFromJsonAsync<List<NoteDto>>($"/api/applications/{applicationId}/notes");

        Assert.Equal(2, thread!.Count);
        Assert.Contains(thread, n => n.Body == "First.");
        Assert.Contains(thread, n => n.Body == "Second.");

        // Asserted as a monotonic sequence rather than by body: two writes inside one clock
        // tick would otherwise make this test fail for a reason that has nothing to do with
        // the ordering contract it is checking.
        Assert.True(thread[0].CreatedAt <= thread[1].CreatedAt);
    }
}
