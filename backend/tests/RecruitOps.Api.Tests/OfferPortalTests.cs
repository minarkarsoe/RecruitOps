using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RecruitOps.Api.Auth;
using RecruitOps.Api.Controllers;
using RecruitOps.Application.DTOs;
using RecruitOps.Application.Interfaces;
using RecruitOps.Domain.Entities;
using RecruitOps.Domain.Enums;
using RecruitOps.Infrastructure.Persistence;
using RecruitOps.Infrastructure.Services.Delivery;
using Xunit;

namespace RecruitOps.Api.Tests;

/// <summary>The test host for slice B: a clock the tests move, and a transport that keeps what it is
/// handed — so "the code stops working after 10 minutes" and "this is the email the candidate gets" are
/// both things a test can observe.</summary>
public class OfferPortalWebAppFactory : CustomWebAppFactory
{
    public sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    public sealed class CapturingEmailSender : IEmailSender
    {
        public ConcurrentQueue<EmailMessage> Sent { get; } = new();

        public Task SendAsync(EmailMessage message, CancellationToken ct = default)
        {
            Sent.Enqueue(message);
            return Task.CompletedTask;
        }
    }

    public TestClock Clock { get; } = new();
    public CapturingEmailSender Email { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Smtp:FromAddress"] = "recruitment@alpha.test",
        }));
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Email);
        });
    }
}

/// <summary>Module 4 slice B — sending, the candidate's link, the code, signing, declining, reminders and
/// expiry (ADR-0030 §5a). Every rule the ADR states with a number is pinned here with that number.</summary>
public class OfferPortalTests : IClassFixture<OfferPortalWebAppFactory>
{
    private readonly OfferPortalWebAppFactory _factory;
    private readonly Module3Scenario _scenario;

    public OfferPortalTests(OfferPortalWebAppFactory factory)
    {
        _factory = factory;
        _scenario = new Module3Scenario(factory);
    }

    private HttpClient Recruiter() => _scenario.Client(Roles.Recruiter, _factory.RecruiterUserId);
    private HttpClient Candidate() => _factory.CreateClient();

    private async Task<T> InDbAsync<T>(Func<AppDbContext, Task<T>> read)
    {
        using var scope = _factory.Services.CreateScope();
        return await read(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    /// <summary>A within-budget draft, sent. Returns it with its link's token.</summary>
    private async Task<(OfferDetailDto Offer, string Token)> SentAsync(string title)
    {
        var draft = await OfferTests.DraftAsync(_scenario, Recruiter(), title);
        var res = await Recruiter().PostAsync($"/api/offers/{draft.Id}/send", null);
        res.EnsureSuccessStatusCode();
        var sent = (await res.Content.ReadFromJsonAsync<OfferDetailDto>())!;
        var token = await InDbAsync(db => db.Offers.IgnoreQueryFilters().Where(o => o.Id == draft.Id)
            .Select(o => o.PortalToken!).SingleAsync());
        return (sent, token);
    }

    private Task<string> LatestCodeAsync(Guid offerId) => InDbAsync(async db =>
    {
        var row = await db.OutboundMessages.IgnoreQueryFilters()
            .Where(m => m.Kind == OutboundMessageKind.OfferAccessCode && m.SubjectId == offerId)
            .OrderByDescending(m => m.CreatedAt).FirstAsync();
        return JsonSerializer.Deserialize<OfferAccessCodePayload>(row.PayloadJson)!.Code;
    });

    private static string Wrong(string code) => code == "000000" ? "111111" : "000000";

    /// <summary>Runs the delivery worker until the outbox is empty — one pass claims a batch, and other
    /// tests' messages share the queue.</summary>
    private async Task DrainOutboxAsync()
    {
        var worker = _factory.Services.GetRequiredService<OutboundMessageWorker>();
        for (var pass = 0; pass < 50 && await worker.RunOnceAsync() > 0; pass++) { }
    }

    /// <summary>A candidate's browser that has asked for a code and entered it.</summary>
    private async Task<HttpClient> UnlockedAsync(Guid offerId, string token) => (await UnlockAsync(offerId, token)).Browser;

    /// <summary>As <see cref="UnlockedAsync"/>, with the session cookie's value as the API set it.</summary>
    private async Task<(HttpClient Browser, string Session)> UnlockAsync(Guid offerId, string token)
    {
        // A tick between codes, as on a real clock: "the most recent code" must be one code.
        _factory.Clock.Now += TimeSpan.FromSeconds(1);
        var candidate = Candidate();
        var asked = await candidate.PostAsync($"/api/public/offers/{token}/code", null);
        Assert.True(asked.StatusCode == HttpStatusCode.Accepted, await asked.Content.ReadAsStringAsync());
        var verify = await candidate.PostAsJsonAsync($"/api/public/offers/{token}/verify",
            new VerifyOfferCodeRequest { Code = await LatestCodeAsync(offerId) });
        Assert.Equal(HttpStatusCode.NoContent, verify.StatusCode);
        var session = verify.Headers.GetValues("Set-Cookie").Single().Split(';')[0].Split('=', 2)[1];
        return (candidate, session);
    }

    private static async Task<string> DetailOf(HttpResponseMessage res) =>
        (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString()!;

    // ── Sending ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_Draft_Within_Budget_Is_Sent_And_Its_Link_Is_Emailed_But_Never_Shown_To_Staff()
    {
        var (offer, token) = await SentAsync("Portal Send");
        Assert.Equal("Sent", offer.Status);
        Assert.NotNull(offer.SentAt);
        Assert.Equal(43, token.Length);

        var json = await (await Recruiter().GetAsync($"/api/offers/{offer.Id}")).Content.ReadAsStringAsync();
        Assert.DoesNotContain(token, json);

        await DrainOutboxAsync();
        var email = _factory.Email.Sent.Single(m => m.Body().Contains(token));
        Assert.Contains($"https://careers.test/offer/{token}", email.PlainTextBody);
        Assert.StartsWith("applicant.", email.To);
        Assert.Contains("Please reply by", email.PlainTextBody);
    }

    [Fact]
    public async Task An_Offer_That_Needs_Approval_Is_Not_Sent_Until_Approved()
    {
        await OfferTests.EnsureOfferChainAsync(_scenario.Client(Roles.Admin, _factory.AdminUserId), _factory.SalesDepartmentId, _factory.FinanceApproverUserId);
        var draft = await OfferTests.DraftAsync(_scenario, Recruiter(), "Portal Needs Approval");
        await OfferTests.TermsAsync(Recruiter(), draft, 900_000m);

        var early = await Recruiter().PostAsync($"/api/offers/{draft.Id}/send", null);
        Assert.Equal(HttpStatusCode.Conflict, early.StatusCode);
        Assert.Contains("needs internal approval", await DetailOf(early));

        (await Recruiter().PostAsync($"/api/offers/{draft.Id}/submit", null)).EnsureSuccessStatusCode();
        (await _scenario.FinanceApprover().PostAsJsonAsync($"/api/offers/{draft.Id}/decide",
            new ApprovalDecisionRequest { Approve = true, Round = 1 })).EnsureSuccessStatusCode();

        var sent = await Recruiter().PostAsync($"/api/offers/{draft.Id}/send", null);
        sent.EnsureSuccessStatusCode();
        Assert.Equal("Sent", (await sent.Content.ReadFromJsonAsync<OfferDetailDto>())!.Status);
    }

    [Fact]
    public async Task Sending_Is_Refused_When_The_Address_Changed_Since_The_Recruiter_Saw_It()
    {
        var draft = await OfferTests.DraftAsync(_scenario, Recruiter(), "Portal Expected Email");
        var shown = (await Recruiter().GetFromJsonAsync<OfferDetailDto>($"/api/offers/{draft.Id}"))!.CandidateEmail!;

        var stale = await Recruiter().PostAsJsonAsync($"/api/offers/{draft.Id}/send", new SendOfferRequest { ExpectedEmail = "old.address@example.test" });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Contains(shown, await DetailOf(stale));

        var res = await Recruiter().PostAsJsonAsync($"/api/offers/{draft.Id}/send", new SendOfferRequest { ExpectedEmail = shown.ToUpperInvariant() });
        res.EnsureSuccessStatusCode();
        Assert.Equal(shown, (await res.Content.ReadFromJsonAsync<OfferDetailDto>())!.CandidateEmail);
    }

    [Fact]
    public async Task Only_Recruitment_Staff_Send_And_Remind()
    {
        var draft = await OfferTests.DraftAsync(_scenario, Recruiter(), "Portal Staff Only");
        Assert.Equal(HttpStatusCode.Forbidden, (await _scenario.SalesManager().PostAsync($"/api/offers/{draft.Id}/send", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _scenario.FinanceApprover().PostAsync($"/api/offers/{draft.Id}/remind", null)).StatusCode);
    }

    // ── The link alone ──────────────────────────────────────────────────────

    [Fact]
    public async Task The_Link_Alone_Shows_The_Company_And_A_Masked_Address_And_Nothing_Of_The_Offer()
    {
        var (offer, token) = await SentAsync("Portal Link Only Title");
        var res = await Candidate().GetAsync($"/api/public/offers/{token}");
        res.EnsureSuccessStatusCode();
        var raw = await res.Content.ReadAsStringAsync();
        var link = JsonSerializer.Deserialize<PortalOfferLinkDto>(raw, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        Assert.Equal("Open", link.State);
        Assert.Matches("^a•••••@example\\.test$", link.MaskedEmail!);
        Assert.Equal("recruitment@alpha.test", link.ContactEmail);
        Assert.DoesNotContain("Portal Link Only Title", raw);
        Assert.DoesNotContain("800", raw);
        Assert.DoesNotContain(offer.CandidateName, raw);

        // Without a code nothing else answers.
        Assert.Equal(HttpStatusCode.Unauthorized, (await Candidate().GetAsync($"/api/public/offers/{token}/offer")).StatusCode);
    }

    [Theory]
    [InlineData("not-a-token")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public async Task An_Unknown_Or_Malformed_Token_Is_One_404_Everywhere(string token)
    {
        var c = Candidate();
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/api/public/offers/{token}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.PostAsync($"/api/public/offers/{token}/code", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.PostAsJsonAsync($"/api/public/offers/{token}/verify", new VerifyOfferCodeRequest { Code = "123456" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/api/public/offers/{token}/offer")).StatusCode);
    }

    // ── The code ────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_Correct_Code_Opens_The_Offer_Marks_It_Viewed_And_Sets_A_Locked_Down_Cookie()
    {
        var (offer, token) = await SentAsync("Portal Unlock");
        var candidate = Candidate();
        (await candidate.PostAsync($"/api/public/offers/{token}/code", null)).EnsureSuccessStatusCode();

        // The code went by email — and not in the subject line, which shows on lock screens.
        var code = await LatestCodeAsync(offer.Id);
        await DrainOutboxAsync();
        var mail = _factory.Email.Sent.Single(m => m.PlainTextBody.Contains(code) && m.Subject.Contains("code"));
        Assert.DoesNotContain(code, mail.Subject);

        var verify = await candidate.PostAsJsonAsync($"/api/public/offers/{token}/verify", new VerifyOfferCodeRequest { Code = code });
        Assert.Equal(HttpStatusCode.NoContent, verify.StatusCode);
        var cookie = verify.Headers.GetValues("Set-Cookie").Single().ToLowerInvariant();
        Assert.Contains("httponly", cookie);
        Assert.Contains("samesite=strict", cookie);
        Assert.Contains($"path=/api/public/offers/{token.ToLowerInvariant()}", cookie);

        var unlocked = await candidate.GetFromJsonAsync<PortalOfferDto>($"/api/public/offers/{token}/offer");
        Assert.Equal("Portal Unlock", unlocked!.Position);
        Assert.Equal(800_000m, unlocked.BaseSalary);
        Assert.Contains(offer.CandidateName, unlocked.LetterText);
        Assert.Equal("Open", unlocked.State);

        var seen = await Recruiter().GetFromJsonAsync<OfferDetailDto>($"/api/offers/{offer.Id}");
        Assert.Equal("Viewed", seen!.Status);
        Assert.NotNull(seen.ViewedAt);
    }

    [Fact]
    public async Task A_Code_Typed_In_Burmese_Digits_Works()
    {
        var (offer, token) = await SentAsync("Portal Burmese Digits");
        var candidate = Candidate();
        await candidate.PostAsync($"/api/public/offers/{token}/code", null);
        var burmese = string.Concat((await LatestCodeAsync(offer.Id)).Select(d => (char)('၀' + (d - '0'))));
        var verify = await candidate.PostAsJsonAsync($"/api/public/offers/{token}/verify", new VerifyOfferCodeRequest { Code = burmese });
        Assert.Equal(HttpStatusCode.NoContent, verify.StatusCode);
    }

    [Fact]
    public async Task Five_Wrong_Tries_End_A_Code_Even_For_The_Right_One()
    {
        var (offer, token) = await SentAsync("Portal Five Tries");
        var candidate = Candidate();
        await candidate.PostAsync($"/api/public/offers/{token}/code", null);
        var code = await LatestCodeAsync(offer.Id);

        for (var i = 1; i <= 5; i++)
        {
            var wrong = await candidate.PostAsJsonAsync($"/api/public/offers/{token}/verify", new VerifyOfferCodeRequest { Code = Wrong(code) });
            Assert.Equal(HttpStatusCode.Conflict, wrong.StatusCode);
            var detail = await DetailOf(wrong);
            if (i < 5) Assert.Contains($"{5 - i} tr", detail);
            else Assert.Contains("stopped working", detail);
        }

        var late = await candidate.PostAsJsonAsync($"/api/public/offers/{token}/verify", new VerifyOfferCodeRequest { Code = code });
        Assert.Equal(HttpStatusCode.Conflict, late.StatusCode);
        Assert.Contains("stopped working", await DetailOf(late));
    }

    [Fact]
    public async Task Parallel_Guesses_Are_Each_Counted_And_A_Spent_Code_Says_The_Same_Whatever_Was_Sent()
    {
        // The flaw both reviews found (2026-10-23): a counter read and written by parallel guesses let all
        // but one go uncounted, and a right guess that lost the race got a different message.
        var (offer, token) = await SentAsync("Portal Parallel Guesses");
        var candidate = Candidate();
        await candidate.PostAsync($"/api/public/offers/{token}/code", null);
        var code = await LatestCodeAsync(offer.Id);

        var guesses = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ =>
            Candidate().PostAsJsonAsync($"/api/public/offers/{token}/verify", new VerifyOfferCodeRequest { Code = Wrong(code) })));
        Assert.All(guesses, g => Assert.Equal(HttpStatusCode.Conflict, g.StatusCode));

        var right = await candidate.PostAsJsonAsync($"/api/public/offers/{token}/verify", new VerifyOfferCodeRequest { Code = code });
        Assert.Equal(HttpStatusCode.Conflict, right.StatusCode);
        var wrong = await candidate.PostAsJsonAsync($"/api/public/offers/{token}/verify", new VerifyOfferCodeRequest { Code = Wrong(code) });
        Assert.Equal(await DetailOf(right), await DetailOf(wrong));
        // Every try was recorded — 20 in parallel, then the right code, then one more.
        var accessId = await InDbAsync(db => db.OfferPortalAccesses.IgnoreQueryFilters()
            .Where(a => a.OfferId == offer.Id).Select(a => a.Id).SingleAsync());
        Assert.Equal(22, await InDbAsync(db => db.OfferPortalAttempts.IgnoreQueryFilters()
            .CountAsync(t => t.OfferPortalAccessId == accessId)));
    }

    [Fact]
    public async Task Parallel_Code_Requests_Still_Send_At_Most_Five()
    {
        var (offer, token) = await SentAsync("Portal Parallel Codes");
        var results = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => Candidate().PostAsync($"/api/public/offers/{token}/code", null)));
        Assert.True(results.Count(r => r.StatusCode == HttpStatusCode.Accepted) <= 5);
        var codes = await InDbAsync(db => db.OutboundMessages.IgnoreQueryFilters()
            .CountAsync(m => m.Kind == OutboundMessageKind.OfferAccessCode && m.SubjectId == offer.Id));
        Assert.True(codes <= 5, $"{codes} codes were queued");
    }

    [Fact]
    public async Task The_Code_Goes_To_The_Address_The_Offer_Was_Sent_To_And_Is_Wiped_Once_Sent()
    {
        var (offer, token) = await SentAsync("Portal Pinned Address");
        var sentTo = (await Recruiter().GetFromJsonAsync<OfferDetailDto>($"/api/offers/{offer.Id}"))!.CandidateEmail!;

        // The candidate record's address changes after sending — by a merge, or a stranger's application.
        await InDbAsync(async db =>
        {
            var candidate = await db.Candidates.IgnoreQueryFilters()
                .SingleAsync(c => db.JobApplications.IgnoreQueryFilters().Any(a => a.Id == offer.JobApplicationId && a.CandidateId == c.Id));
            candidate.Email = "someone.else@attacker.test";
            return await db.SaveChangesAsync();
        });

        await Candidate().PostAsync($"/api/public/offers/{token}/code", null);
        var row = await InDbAsync(db => db.OutboundMessages.IgnoreQueryFilters()
            .Where(m => m.Kind == OutboundMessageKind.OfferAccessCode && m.SubjectId == offer.Id).SingleAsync());
        Assert.Equal(sentTo, row.Recipient);

        await DrainOutboxAsync();
        var after = await InDbAsync(db => db.OutboundMessages.IgnoreQueryFilters().SingleAsync(m => m.Id == row.Id));
        Assert.Equal(OutboundMessageStatus.Sent, after.Status);
        Assert.Equal("{}", after.PayloadJson);
    }

    [Fact]
    public async Task A_New_Code_Replaces_The_Old_And_A_Code_Works_For_Ten_Minutes()
    {
        var (offer, token) = await SentAsync("Portal Code Lifetime");
        var candidate = Candidate();
        await candidate.PostAsync($"/api/public/offers/{token}/code", null);
        var first = await LatestCodeAsync(offer.Id);
        _factory.Clock.Now += TimeSpan.FromSeconds(1);
        await candidate.PostAsync($"/api/public/offers/{token}/code", null);
        var second = await LatestCodeAsync(offer.Id);

        if (first != second)
        {
            var old = await candidate.PostAsJsonAsync($"/api/public/offers/{token}/verify", new VerifyOfferCodeRequest { Code = first });
            Assert.Equal(HttpStatusCode.Conflict, old.StatusCode);
        }

        _factory.Clock.Now += TimeSpan.FromMinutes(10);
        var expired = await candidate.PostAsJsonAsync($"/api/public/offers/{token}/verify", new VerifyOfferCodeRequest { Code = second });
        Assert.Equal(HttpStatusCode.Conflict, expired.StatusCode);
        Assert.Contains("expired", await DetailOf(expired));
    }

    [Fact]
    public async Task At_Most_Five_Codes_A_Day()
    {
        var (_, token) = await SentAsync("Portal Daily Codes");
        var candidate = Candidate();
        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.Accepted, (await candidate.PostAsync($"/api/public/offers/{token}/code", null)).StatusCode);

        var sixth = await candidate.PostAsync($"/api/public/offers/{token}/code", null);
        Assert.Equal(HttpStatusCode.Conflict, sixth.StatusCode);
        Assert.Contains("5 codes in the last 24 hours", await DetailOf(sixth));

        _factory.Clock.Now += TimeSpan.FromHours(24) + TimeSpan.FromMinutes(1);
        Assert.Equal(HttpStatusCode.Accepted, (await candidate.PostAsync($"/api/public/offers/{token}/code", null)).StatusCode);
    }

    [Fact]
    public async Task A_Session_Opens_Only_Its_Own_Offer_And_Lasts_Thirty_Minutes()
    {
        var (a, tokenA) = await SentAsync("Portal Session A");
        var (_, tokenB) = await SentAsync("Portal Session B");
        var (candidate, session) = await UnlockAsync(a.Id, tokenA);

        // The cookie is scoped to A's path, so a browser would not send it to B; replaying it by hand
        // must not work either.
        var stolen = Candidate();
        stolen.DefaultRequestHeaders.Add("Cookie", $"{PublicOffersController.SessionCookie}={session}");
        Assert.Equal(HttpStatusCode.Unauthorized, (await stolen.GetAsync($"/api/public/offers/{tokenB}/offer")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await stolen.GetAsync($"/api/public/offers/{tokenA}/offer")).StatusCode);

        // A browser-session cookie: no expiry of its own; the server ends it.
        Assert.Equal(HttpStatusCode.OK, (await candidate.GetAsync($"/api/public/offers/{tokenA}/offer")).StatusCode);
        _factory.Clock.Now += TimeSpan.FromMinutes(31);
        Assert.Equal(HttpStatusCode.Unauthorized, (await candidate.GetAsync($"/api/public/offers/{tokenA}/offer")).StatusCode);
    }

    [Fact]
    public async Task A_New_Code_Ends_The_Earlier_Session_And_An_Answer_Ends_Them_All()
    {
        var (offer, token) = await SentAsync("Portal Sessions End");
        var first = await UnlockedAsync(offer.Id, token);
        var second = await UnlockedAsync(offer.Id, token);
        Assert.Equal(HttpStatusCode.Unauthorized, (await first.GetAsync($"/api/public/offers/{token}/offer")).StatusCode);

        var declined = await second.PostAsJsonAsync($"/api/public/offers/{token}/decline", new DeclineOfferRequest());
        declined.EnsureSuccessStatusCode();
        // Answered on a shared computer, the offer does not stay open to the next person.
        Assert.Equal(HttpStatusCode.Unauthorized, (await second.GetAsync($"/api/public/offers/{token}/offer")).StatusCode);
    }

    [Fact]
    public async Task The_Session_Cookie_Has_No_Expiry_Of_Its_Own()
    {
        var (offer, token) = await SentAsync("Portal Browser Session");
        var candidate = Candidate();
        await candidate.PostAsync($"/api/public/offers/{token}/code", null);
        var verify = await candidate.PostAsJsonAsync($"/api/public/offers/{token}/verify",
            new VerifyOfferCodeRequest { Code = await LatestCodeAsync(offer.Id) });
        var cookie = verify.Headers.GetValues("Set-Cookie").Single().ToLowerInvariant();
        Assert.DoesNotContain("expires=", cookie);
        Assert.DoesNotContain("max-age=", cookie);
    }

    // ── Accepting ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Accepting_Records_What_Was_Read_And_Who_Typed_It_And_Tells_Both_Sides()
    {
        var (offer, token) = await SentAsync("Portal Accept");
        var candidate = await UnlockedAsync(offer.Id, token);
        candidate.DefaultRequestHeaders.UserAgent.ParseAdd("TestPhone/1.0");
        var shown = (await candidate.GetFromJsonAsync<PortalOfferDto>($"/api/public/offers/{token}/offer"))!;

        // Consent is not optional, the name is not blank, and the letter must be the one read.
        var noConsent = await candidate.PostAsJsonAsync($"/api/public/offers/{token}/accept",
            new AcceptOfferRequest { TypedName = "Aung Kyaw", Consent = false, LetterSha256 = shown.LetterSha256 });
        Assert.Equal(HttpStatusCode.Conflict, noConsent.StatusCode);
        var blank = await candidate.PostAsJsonAsync($"/api/public/offers/{token}/accept",
            new AcceptOfferRequest { TypedName = " \n ", Consent = true, LetterSha256 = shown.LetterSha256 });
        Assert.Equal(HttpStatusCode.BadRequest, blank.StatusCode);
        // Not whitespace to the validator, nothing at all once on one line: the service's own check.
        var controlOnly = await candidate.PostAsJsonAsync($"/api/public/offers/{token}/accept",
            new AcceptOfferRequest { TypedName = "\u0007\u0008", Consent = true, LetterSha256 = shown.LetterSha256 });
        Assert.Equal(HttpStatusCode.Conflict, controlOnly.StatusCode);
        var otherLetter = await candidate.PostAsJsonAsync($"/api/public/offers/{token}/accept",
            new AcceptOfferRequest { TypedName = "Aung Kyaw", Consent = true, LetterSha256 = new string('0', 64) });
        Assert.Equal(HttpStatusCode.Conflict, otherLetter.StatusCode);
        Assert.Contains("changed since you opened it", await DetailOf(otherLetter));

        var accept = await candidate.PostAsJsonAsync($"/api/public/offers/{token}/accept",
            new AcceptOfferRequest { TypedName = "  Aung\nKyaw  Moe ", Consent = true, LetterSha256 = shown.LetterSha256 });
        accept.EnsureSuccessStatusCode();
        var answered = (await accept.Content.ReadFromJsonAsync<PortalOfferDto>())!;
        Assert.Equal("Accepted", answered.State);
        Assert.Equal("Aung Kyaw Moe", answered.SignedName); // one line, as signed

        var signature = await InDbAsync(db => db.OfferSignatures.IgnoreQueryFilters().SingleAsync(s => s.OfferId == offer.Id));
        Assert.Equal(shown.LetterText, signature.LetterText);
        Assert.Equal(shown.LetterSha256, signature.LetterSha256);
        Assert.Equal("TestPhone/1.0", signature.UserAgent);

        // Staff see the evidence; a Hiring Manager sees only that it was accepted.
        var staff = (await Recruiter().GetFromJsonAsync<OfferDetailDto>($"/api/offers/{offer.Id}"))!;
        Assert.Equal("Accepted", staff.Status);
        Assert.Equal("Aung Kyaw Moe", staff.Signature!.TypedName);
        var manager = (await _scenario.SalesManager().GetFromJsonAsync<OfferDetailDto>($"/api/offers/{offer.Id}"))!;
        Assert.Equal("Accepted", manager.Status);
        Assert.Null(manager.Signature);

        // Accepting keeps the session — the documents follow at once (ADR-0030 §5b) — and the answer is
        // final from the link, in this session or a new one.
        var after = (await candidate.GetFromJsonAsync<PortalOfferDto>($"/api/public/offers/{token}/offer"))!;
        Assert.Equal("Accepted", after.State);
        Assert.True(after.DocumentsOpen);
        var reopened = await UnlockedAsync(offer.Id, token);
        Assert.Equal("Accepted", (await reopened.GetFromJsonAsync<PortalOfferDto>($"/api/public/offers/{token}/offer"))!.State);
        var again = await reopened.PostAsJsonAsync($"/api/public/offers/{token}/decline", new DeclineOfferRequest());
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Contains("already accepted", await DetailOf(again));

        // The candidate's copy carries the letter as signed; the recruiter is told.
        await DrainOutboxAsync();
        var copy = _factory.Email.Sent.Single(m => m.Subject.StartsWith("Your signed offer") && m.PlainTextBody.Contains(offer.CandidateName) && m.PlainTextBody.Contains(signature.LetterSha256));
        Assert.Contains(signature.LetterText, copy.PlainTextBody);
        Assert.Contains(_factory.Email.Sent, m => m.Subject == $"{offer.CandidateName} accepted offer {offer.Reference}");

        // The recruiter's preview of an accepted offer is the letter as signed, even after a name changes.
        await InDbAsync(async db =>
        {
            var c = await db.Candidates.IgnoreQueryFilters()
                .SingleAsync(x => db.JobApplications.IgnoreQueryFilters().Any(a => a.Id == offer.JobApplicationId && a.CandidateId == x.Id));
            c.FullName += " (renamed)";
            return await db.SaveChangesAsync();
        });
        Assert.Equal(signature.LetterText, (await Recruiter().GetFromJsonAsync<OfferLetterDto>($"/api/offers/{offer.Id}/letter"))!.Text);
    }

    // ── Declining ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Declining_Is_The_Candidates_Rejected_And_Its_Reason_Follows_The_Salary_Rule()
    {
        var (offer, token) = await SentAsync("Portal Decline");
        var candidate = await UnlockedAsync(offer.Id, token);

        var res = await candidate.PostAsJsonAsync($"/api/public/offers/{token}/decline",
            new DeclineOfferRequest { Reason = "The salary is below what I earn now." });
        res.EnsureSuccessStatusCode();
        Assert.Equal("Declined", (await res.Content.ReadFromJsonAsync<PortalOfferDto>())!.State);
        // Declining ends the session: a declined offer on a shared computer does not stay open.
        Assert.Equal(HttpStatusCode.Unauthorized, (await candidate.GetAsync($"/api/public/offers/{token}/offer")).StatusCode);

        var staff = (await Recruiter().GetFromJsonAsync<OfferDetailDto>($"/api/offers/{offer.Id}"))!;
        Assert.Equal("Rejected", staff.Status);
        Assert.Equal("The salary is below what I earn now.", staff.DeclineReason);
        Assert.NotNull(staff.RespondedAt);

        // Salaries hidden from Hiring Managers by default — and so is a reason that quotes one.
        var manager = (await _scenario.SalesManager().GetFromJsonAsync<OfferDetailDto>($"/api/offers/{offer.Id}"))!;
        Assert.Equal("Rejected", manager.Status);
        Assert.Null(manager.DeclineReason);

        var reopened = await UnlockedAsync(offer.Id, token);
        var accept = await reopened.PostAsJsonAsync($"/api/public/offers/{token}/accept",
            new AcceptOfferRequest { TypedName = "Changed Mind", Consent = true, LetterSha256 = new string('a', 64) });
        Assert.Equal(HttpStatusCode.Conflict, accept.StatusCode);
        Assert.Contains("declined this offer", await DetailOf(accept));
    }

    // ── Reminders and expiry ────────────────────────────────────────────────

    [Fact]
    public async Task A_Reminder_Repeats_The_Same_Link_At_Most_Once_A_Day()
    {
        var (offer, token) = await SentAsync("Portal Remind");
        var first = await Recruiter().PostAsync($"/api/offers/{offer.Id}/remind", null);
        first.EnsureSuccessStatusCode();
        Assert.NotNull((await first.Content.ReadFromJsonAsync<OfferDetailDto>())!.LastRemindedAt);

        var second = await Recruiter().PostAsync($"/api/offers/{offer.Id}/remind", null);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        _factory.Clock.Now += TimeSpan.FromHours(24);
        (await Recruiter().PostAsync($"/api/offers/{offer.Id}/remind", null)).EnsureSuccessStatusCode();

        await DrainOutboxAsync();
        var reminders = _factory.Email.Sent.Where(m => m.Subject.StartsWith("Reminder:") && m.PlainTextBody.Contains(token)).ToList();
        Assert.Equal(2, reminders.Count);
    }

    [Fact]
    public async Task Past_Its_Expiry_Date_An_Offer_Expires_On_Read_And_Its_Link_Stops()
    {
        var (offer, token) = await SentAsync("Portal Expiry");

        // One minute before the end of the expiry date — in the company's time zone, Yangon (UTC+6:30),
        // not UTC — the candidate unlocks it; two minutes later, with that session still live, it has
        // expired. Measured in UTC this would still be the afternoon of the expiry date.
        var yangon = TimeZoneInfo.FindSystemTimeZoneById("Asia/Yangon");
        var local = offer.ExpiresOn.ToDateTime(new TimeOnly(23, 59));
        var lastMinute = new DateTimeOffset(local, yangon.GetUtcOffset(local)).ToUniversalTime();
        _factory.Clock.Now = lastMinute;
        var candidate = await UnlockedAsync(offer.Id, token);
        var shown = (await candidate.GetFromJsonAsync<PortalOfferDto>($"/api/public/offers/{token}/offer"))!;
        _factory.Clock.Now = lastMinute + TimeSpan.FromMinutes(2);

        var accept = await candidate.PostAsJsonAsync($"/api/public/offers/{token}/accept",
            new AcceptOfferRequest { TypedName = "Too Late", Consent = true, LetterSha256 = shown.LetterSha256 });
        Assert.Equal(HttpStatusCode.Conflict, accept.StatusCode);
        Assert.Contains("expired", await DetailOf(accept));

        var link = await Candidate().GetFromJsonAsync<PortalOfferLinkDto>($"/api/public/offers/{token}");
        Assert.Equal("Expired", link!.State);
        Assert.Equal(HttpStatusCode.Conflict, (await Candidate().PostAsync($"/api/public/offers/{token}/code", null)).StatusCode);

        var staff = await Recruiter().GetFromJsonAsync<OfferDetailDto>($"/api/offers/{offer.Id}");
        Assert.Equal("Expired", staff!.Status);
        Assert.Equal(HttpStatusCode.Conflict, (await Recruiter().PostAsync($"/api/offers/{offer.Id}/remind", null)).StatusCode);

        // An ANSWERED offer's link closes on the same date (security review M1): it no longer resolves.
        var (answered, answeredToken) = await SentAsync("Portal Answered Closes");
        var unlocked = await UnlockedAsync(answered.Id, answeredToken);
        (await unlocked.PostAsJsonAsync($"/api/public/offers/{answeredToken}/decline", new DeclineOfferRequest())).EnsureSuccessStatusCode();
        var answeredExpiry = (await Recruiter().GetFromJsonAsync<OfferDetailDto>($"/api/offers/{answered.Id}"))!.ExpiresOn;
        var answeredLocal = answeredExpiry.AddDays(1).ToDateTime(new TimeOnly(12, 0));
        _factory.Clock.Now = new DateTimeOffset(answeredLocal, yangon.GetUtcOffset(answeredLocal)).ToUniversalTime();
        Assert.Equal(HttpStatusCode.NotFound, (await Candidate().GetAsync($"/api/public/offers/{answeredToken}")).StatusCode);
        Assert.Equal("Rejected", (await Recruiter().GetFromJsonAsync<OfferDetailDto>($"/api/offers/{answered.Id}"))!.Status);

        // Expired closes the offer: the application can be offered again.
        var create = await Recruiter().PostAsJsonAsync("/api/offers", new CreateOfferRequest { JobApplicationId = offer.JobApplicationId });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
    }

    // ── The delivery log ────────────────────────────────────────────────────

    [Fact]
    public async Task A_Department_Scoped_Viewer_Sees_That_The_Recruiter_Was_Told_Not_Their_Address()
    {
        var (offer, token) = await SentAsync("Portal Notice Redacted");
        var candidate = await UnlockedAsync(offer.Id, token);
        (await candidate.PostAsJsonAsync($"/api/public/offers/{token}/decline", new DeclineOfferRequest())).EnsureSuccessStatusCode();
        var recruiterEmail = await InDbAsync(db => db.Users.IgnoreQueryFilters()
            .Where(u => u.Id == _factory.RecruiterUserId).Select(u => u.Email).SingleAsync());

        var staff = await (await _scenario.Client(Roles.Admin, _factory.AdminUserId)
            .GetAsync($"/api/delivery?subjectId={offer.Id}&pageSize=50")).Content.ReadAsStringAsync();
        Assert.Contains(recruiterEmail, staff);

        var manager = await (await _scenario.SalesManager().GetAsync($"/api/delivery?subjectId={offer.Id}&pageSize=50")).Content.ReadAsStringAsync();
        Assert.Contains("The recruiter who made the offer", manager);
        Assert.DoesNotContain(recruiterEmail, manager);
    }

    [Fact]
    public async Task A_Deactivated_Creator_Is_Not_Sent_The_Answer()
    {
        var (offer, token) = await SentAsync("Portal Notice Inactive");
        await SetRecruiterActiveAsync(false);
        try
        {
            var candidate = await UnlockedAsync(offer.Id, token);
            (await candidate.PostAsJsonAsync($"/api/public/offers/{token}/decline",
                new DeclineOfferRequest { Reason = "The salary is too low." })).EnsureSuccessStatusCode();
        }
        finally
        {
            await SetRecruiterActiveAsync(true);
        }

        var notice = await InDbAsync(db => db.OutboundMessages.IgnoreQueryFilters()
            .SingleAsync(m => m.Kind == OutboundMessageKind.OfferAnswerNotice && m.SubjectId == offer.Id));
        Assert.Equal(string.Empty, notice.Recipient);
        await DrainOutboxAsync();
        var after = await InDbAsync(db => db.OutboundMessages.IgnoreQueryFilters().SingleAsync(m => m.Id == notice.Id));
        Assert.Equal(OutboundMessageStatus.Failed, after.Status);
        Assert.Contains("recruiter", after.LastError);
    }

    private Task<int> SetRecruiterActiveAsync(bool active) => InDbAsync(async db =>
    {
        var user = await db.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == _factory.RecruiterUserId);
        user.IsActive = active;
        return await db.SaveChangesAsync();
    });


    [Fact]
    public async Task Offer_Emails_Appear_In_The_Delivery_Log_Under_The_Candidates_Name_Without_Their_Contents()
    {
        var (offer, token) = await SentAsync("Portal Delivery Log");
        await Candidate().PostAsync($"/api/public/offers/{token}/code", null);
        var code = await LatestCodeAsync(offer.Id);

        var raw = await (await _scenario.Client(Roles.Admin, _factory.AdminUserId)
            .GetAsync($"/api/delivery?subjectId={offer.Id}&pageSize=50")).Content.ReadAsStringAsync();
        Assert.Contains("Offer code", raw);
        Assert.Contains("Offer sent", raw);
        Assert.Contains(offer.CandidateName, raw);
        Assert.DoesNotContain(code, raw);
        Assert.DoesNotContain(token, raw);
    }
}

file static class EmailMessageExtensions
{
    public static string Body(this EmailMessage message) => message.PlainTextBody;
}
