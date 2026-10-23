using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RecruitOps.Api.Auth;
using RecruitOps.Application.DTOs;
using RecruitOps.Application.Interfaces;
using RecruitOps.Domain.Enums;
using RecruitOps.Infrastructure.Persistence;
using RecruitOps.Infrastructure.Services.Delivery;
using Xunit;

namespace RecruitOps.Api.Tests;

/// <summary>Module 4 slice C — joining documents on the candidate's link, the recruiter's review and the
/// IT/Admin handoff (ADR-0030 §5b). Its own fixture: these tests move the clock past joining dates.</summary>
public class PreboardingTests : IClassFixture<OfferPortalWebAppFactory>
{
    private readonly OfferPortalWebAppFactory _factory;
    private readonly Module3Scenario _scenario;

    public PreboardingTests(OfferPortalWebAppFactory factory)
    {
        _factory = factory;
        _scenario = new Module3Scenario(factory);
    }

    private HttpClient Recruiter() => _scenario.Client(Roles.Recruiter, _factory.RecruiterUserId);
    private HttpClient Admin() => _scenario.Client(Roles.Admin, _factory.AdminUserId);

    private async Task<T> InDbAsync<T>(Func<AppDbContext, Task<T>> read)
    {
        using var scope = _factory.Services.CreateScope();
        return await read(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private InMemoryFileStorage Storage => (InMemoryFileStorage)_factory.Services.GetRequiredService<IFileStorage>();

    private async Task DrainOutboxAsync()
    {
        var worker = _factory.Services.GetRequiredService<OutboundMessageWorker>();
        for (var pass = 0; pass < 50 && await worker.RunOnceAsync() > 0; pass++) { }
    }

    private static async Task<string> DetailOf(HttpResponseMessage res) =>
        (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString()!;

    // ── Getting to an accepted offer ───────────────────────────────────────

    private sealed record Accepted(OfferDetailDto Offer, string Token, HttpClient Browser)
    {
        public string Documents => $"/api/public/offers/{Token}/documents";
        public string Review => $"/api/offers/{Offer.Id}/preboarding";
    }

    /// <summary>A within-budget offer, sent, unlocked and accepted — the browser still holding its session.</summary>
    private async Task<Accepted> AcceptedAsync(string title)
    {
        var draft = await OfferTests.DraftAsync(_scenario, Recruiter(), title);
        (await Recruiter().PostAsync($"/api/offers/{draft.Id}/send", null)).EnsureSuccessStatusCode();
        var token = await InDbAsync(db => db.Offers.IgnoreQueryFilters().Where(o => o.Id == draft.Id)
            .Select(o => o.PortalToken!).SingleAsync());
        var browser = await UnlockedAsync(draft.Id, token);
        var shown = (await browser.GetFromJsonAsync<PortalOfferDto>($"/api/public/offers/{token}/offer"))!;
        (await browser.PostAsJsonAsync($"/api/public/offers/{token}/accept",
            new AcceptOfferRequest { TypedName = "Aung Kyaw Moe", Consent = true, LetterSha256 = shown.LetterSha256 }))
            .EnsureSuccessStatusCode();
        return new Accepted(draft, token, browser);
    }

    private async Task<HttpClient> UnlockedAsync(Guid offerId, string token)
    {
        _factory.Clock.Now += TimeSpan.FromSeconds(1);
        var browser = _factory.CreateClient();
        (await browser.PostAsync($"/api/public/offers/{token}/code", null)).EnsureSuccessStatusCode();
        var code = await InDbAsync(async db =>
        {
            var row = await db.OutboundMessages.IgnoreQueryFilters()
                .Where(m => m.Kind == OutboundMessageKind.OfferAccessCode && m.SubjectId == offerId)
                .OrderByDescending(m => m.CreatedAt).FirstAsync();
            return JsonSerializer.Deserialize<OfferAccessCodePayload>(row.PayloadJson)!.Code;
        });
        var verify = await browser.PostAsJsonAsync($"/api/public/offers/{token}/verify", new VerifyOfferCodeRequest { Code = code });
        Assert.Equal(HttpStatusCode.NoContent, verify.StatusCode);
        return browser;
    }

    // ── Files ───────────────────────────────────────────────────────────────

    private static byte[] Png(int size = 2048) => Bytes(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, size);
    private static byte[] Jpeg(int size = 2048) => Bytes(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, size);
    private static byte[] Pdf(int size = 2048) => Bytes("%PDF-1.7\n"u8.ToArray(), size);

    private static byte[] Bytes(byte[] head, int size)
    {
        var bytes = new byte[size];
        Random.Shared.NextBytes(bytes);
        head.CopyTo(bytes, 0);
        return bytes;
    }

    private static Task<HttpResponseMessage> UploadAsync(Accepted a, string kind, byte[] bytes, string name = "photo.jpg", string declared = "image/jpeg")
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(declared);
        form.Add(file, "file", name);
        return a.Browser.PostAsync($"{a.Documents}/{kind}/files", form);
    }

    private static async Task<PortalDocumentsDto> UploadedAsync(Accepted a, string kind, byte[] bytes, string name = "photo.jpg")
    {
        var res = await UploadAsync(a, kind, bytes, name);
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
        return (await res.Content.ReadFromJsonAsync<PortalDocumentsDto>())!;
    }

    private static Task<HttpResponseMessage> SaveBankAsync(Accepted a, string number = "0012 3456 7890 4471") =>
        a.Browser.PutAsJsonAsync($"{a.Documents}/bank", new SaveBankDetailsRequest
        {
            BankName = "KBZ Bank", AccountName = "AUNG KYAW MOE", AccountNumber = number,
        });

    /// <summary>Every required item, plus the optional photo.</summary>
    private static async Task CompleteAsync(Accepted a)
    {
        await UploadedAsync(a, "NrcFront", Jpeg());
        await UploadedAsync(a, "NrcBack", Jpeg());
        (await SaveBankAsync(a)).EnsureSuccessStatusCode();
        await UploadedAsync(a, "BankAccount", Jpeg());
        await UploadedAsync(a, "Education", Pdf(), "degree.pdf");
        await UploadedAsync(a, "Photo", Png(), "me.png");
    }

    private async Task<PreboardingDto> ReviewAsync(Accepted a) =>
        (await Recruiter().GetFromJsonAsync<PreboardingDto>(a.Review))!;

    private async Task<HttpResponseMessage> ApproveAsync(Accepted a, string kind, int? revision = null)
    {
        revision ??= (await ReviewAsync(a)).Documents.Single(d => d.Kind == kind).Revision;
        return await Recruiter().PostAsJsonAsync($"{a.Review}/{kind}/approve", new ReviewPreboardingDocumentRequest { Revision = revision });
    }

    private async Task SettingsAsync(bool photoRequired, string? it, string? admin)
    {
        var res = await Admin().PutAsJsonAsync("/api/settings/preboarding", new UpdatePreboardingSettingsRequest
        {
            NrcRequired = true, BankAccountRequired = true, EducationRequired = true, PhotoRequired = photoRequired,
            ItHandoffEmail = it, AdminHandoffEmail = admin,
        });
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
    }

    // ── The candidate's side ───────────────────────────────────────────────

    [Fact]
    public async Task Signing_Leads_Straight_To_The_Documents_With_The_Required_Set_Fixed_At_Acceptance()
    {
        await SettingsAsync(photoRequired: false, it: null, admin: null);
        var a = await AcceptedAsync("Pre Fixed Set");

        var docs = (await a.Browser.GetFromJsonAsync<PortalDocumentsDto>(a.Documents))!;
        Assert.Equal(new[] { "NrcFront", "NrcBack", "BankAccount", "Education", "Photo" }, docs.Documents.Select(d => d.Kind));
        Assert.Equal(new[] { true, true, true, true, false }, docs.Documents.Select(d => d.Required));
        Assert.Equal(3, docs.RequiredItems);
        Assert.Equal(0, docs.RequiredItemsReady);
        Assert.False(docs.CanSend);
        Assert.Equal(a.Offer.JoiningDate, docs.OpenUntil);
        Assert.All(docs.Documents, d => Assert.Equal("Missing", d.Status));

        // Changing the company's set later does not move this candidate's.
        await SettingsAsync(photoRequired: true, it: null, admin: null);
        docs = (await a.Browser.GetFromJsonAsync<PortalDocumentsDto>(a.Documents))!;
        Assert.False(docs.Documents.Single(d => d.Kind == "Photo").Required);
        var next = await AcceptedAsync("Pre Fixed Set Next");
        Assert.True((await next.Browser.GetFromJsonAsync<PortalDocumentsDto>(next.Documents))!.Documents.Single(d => d.Kind == "Photo").Required);
        await SettingsAsync(photoRequired: false, it: null, admin: null);
    }

    [Fact]
    public async Task A_File_Is_What_Its_First_Bytes_Say_And_Is_Stored_Under_A_Key_The_Candidate_Did_Not_Choose()
    {
        var a = await AcceptedAsync("Pre Sniff");

        // A text file dressed as a photo.
        var fake = await UploadAsync(a, "NrcFront", Encoding.UTF8.GetBytes("<script>alert(1)</script>"), "nrc.jpg", "image/jpeg");
        Assert.Equal(HttpStatusCode.Conflict, fake.StatusCode);
        Assert.Equal("Upload a photo (JPG or PNG) or a PDF.", await DetailOf(fake));
        // The staff-card photo is a picture, never a PDF.
        var pdfPhoto = await UploadAsync(a, "Photo", Pdf(), "me.pdf", "application/pdf");
        Assert.Equal(HttpStatusCode.Conflict, pdfPhoto.StatusCode);
        Assert.Equal("Upload a photo — a JPG or PNG.", await DetailOf(pdfPhoto));
        // Too big, whatever the length the client declared.
        var big = await UploadAsync(a, "NrcFront", Jpeg(10 * 1024 * 1024 + 1));
        Assert.Equal(HttpStatusCode.Conflict, big.StatusCode);
        Assert.Contains("over 10 MB", await DetailOf(big));
        // A kind is named exactly — not by number, not by a list.
        Assert.Equal(HttpStatusCode.Conflict, (await UploadAsync(a, "0", Jpeg())).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await UploadAsync(a, "NrcFront,NrcBack", Jpeg())).StatusCode);

        // A PNG declared as a JPEG named like a path is stored as a PNG, under a key with no name in it.
        var bytes = Png();
        var docs = await UploadedAsync(a, "NrcFront", bytes, "..\\..\\etc/evil\u200Bname.png");
        var file = docs.Documents.Single(d => d.Kind == "NrcFront").Files.Single();
        Assert.Equal("evilname.png", file.FileName);
        var row = await InDbAsync(db => db.PreboardingFiles.IgnoreQueryFilters().SingleAsync(f => f.Id == file.Id));
        Assert.Equal("image/png", row.ContentType);
        Assert.Equal($"preboarding/{a.Offer.Id}/{file.Id}", row.StorageKey);
        var stored = await Storage.DownloadAsync(row.StorageKey);
        using var copy = new MemoryStream();
        await stored!.Content.CopyToAsync(copy);
        Assert.Equal(bytes, copy.ToArray());
        Assert.Equal("Submitted", docs.Documents.Single(d => d.Kind == "NrcFront").Status);
    }

    [Fact]
    public async Task A_One_File_Document_Is_Replaced_And_Education_Collects_Up_To_Five()
    {
        var a = await AcceptedAsync("Pre Replace");
        var first = (await UploadedAsync(a, "NrcBack", Jpeg())).Documents.Single(d => d.Kind == "NrcBack").Files.Single();
        var firstKey = await InDbAsync(db => db.PreboardingFiles.IgnoreQueryFilters().Where(f => f.Id == first.Id).Select(f => f.StorageKey).SingleAsync());
        var second = (await UploadedAsync(a, "NrcBack", Jpeg())).Documents.Single(d => d.Kind == "NrcBack").Files.Single();
        Assert.NotEqual(first.Id, second.Id);
        // The old object is gone; its row is kept, marked removed.
        Assert.False(await Storage.ExistsAsync(firstKey));
        Assert.NotNull(await InDbAsync(db => db.PreboardingFiles.IgnoreQueryFilters().Where(f => f.Id == first.Id).Select(f => f.RemovedAt).SingleAsync()));

        for (var i = 0; i < 5; i++) await UploadedAsync(a, "Education", Pdf(), $"cert-{i}.pdf");
        var sixth = await UploadAsync(a, "Education", Pdf(), "cert-6.pdf", "application/pdf");
        Assert.Equal(HttpStatusCode.Conflict, sixth.StatusCode);
        Assert.Contains("up to 5 files", await DetailOf(sixth));

        var docs = (await a.Browser.GetFromJsonAsync<PortalDocumentsDto>(a.Documents))!;
        var one = docs.Documents.Single(d => d.Kind == "Education").Files.First();
        var removed = await a.Browser.DeleteAsync($"{a.Documents}/files/{one.Id}");
        removed.EnsureSuccessStatusCode();
        Assert.Equal(4, (await removed.Content.ReadFromJsonAsync<PortalDocumentsDto>())!.Documents.Single(d => d.Kind == "Education").Files.Count);
        Assert.Equal(HttpStatusCode.Conflict, (await a.Browser.DeleteAsync($"{a.Documents}/files/{one.Id}")).StatusCode);
    }

    [Fact]
    public async Task Without_A_Session_Or_For_An_Unanswered_Offer_There_Are_No_Documents()
    {
        var a = await AcceptedAsync("Pre Session");
        var stranger = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await stranger.GetAsync(a.Documents)).StatusCode);
        var form = new MultipartFormDataContent { { new ByteArrayContent(Jpeg()), "file", "x.jpg" } };
        Assert.Equal(HttpStatusCode.Unauthorized, (await stranger.PostAsync($"{a.Documents}/NrcFront/files", form)).StatusCode);

        // Another offer's session does not open this one's documents.
        var other = await AcceptedAsync("Pre Session Other");
        Assert.Equal(HttpStatusCode.Unauthorized, (await other.Browser.GetAsync(a.Documents)).StatusCode);

        // An offer still waiting for an answer has nothing to upload.
        var draft = await OfferTests.DraftAsync(_scenario, Recruiter(), "Pre Session Unanswered");
        (await Recruiter().PostAsync($"/api/offers/{draft.Id}/send", null)).EnsureSuccessStatusCode();
        var token = await InDbAsync(db => db.Offers.IgnoreQueryFilters().Where(o => o.Id == draft.Id).Select(o => o.PortalToken!).SingleAsync());
        var browser = await UnlockedAsync(draft.Id, token);
        var early = await browser.GetAsync($"/api/public/offers/{token}/documents");
        Assert.Equal(HttpStatusCode.Conflict, early.StatusCode);
        Assert.Contains("once the offer is accepted", await DetailOf(early));
    }

    [Fact]
    public async Task Bank_Details_Are_Typed_Masked_Everywhere_And_Read_Whole_Only_Through_An_Audited_Reveal()
    {
        var a = await AcceptedAsync("Pre Bank");
        var bad = await SaveBankAsync(a, "12-AB-34");
        Assert.Equal(HttpStatusCode.Conflict, bad.StatusCode);
        Assert.Contains("digits only", await DetailOf(bad));

        // Burmese digits, as a Myanmar keyboard types them.
        var res = await SaveBankAsync(a, "၀၀၁၂ ၃၄၅၆ ၇၈၉၀ ၄၄၇၁");
        res.EnsureSuccessStatusCode();
        var portalJson = await res.Content.ReadAsStringAsync();
        Assert.DoesNotContain("0012345678904471", portalJson);
        var bank = JsonSerializer.Deserialize<PortalDocumentsDto>(portalJson, new JsonSerializerOptions(JsonSerializerDefaults.Web))!
            .Documents.Single(d => d.Kind == "BankAccount");
        Assert.Equal("•••• 4471", bank.Bank!.AccountNumberMasked);
        Assert.False(bank.Complete); // the bank book photo is still to come
        Assert.Equal("0012345678904471", await InDbAsync(db => db.PreboardingDocuments.IgnoreQueryFilters()
            .Where(d => d.OfferId == a.Offer.Id && d.Kind == PreboardingDocumentKind.BankAccount).Select(d => d.BankAccountNumber!).SingleAsync()));

        var reviewJson = await Recruiter().GetStringAsync(a.Review);
        Assert.DoesNotContain("0012345678904471", reviewJson);
        Assert.Contains("•••• 4471", reviewJson);

        var before = await InDbAsync(db => db.AuditLogEntries.IgnoreQueryFilters().CountAsync(e => e.Action == AuditAction.PreboardingBankNumberRevealed));
        var reveal = await Recruiter().PostAsync($"{a.Review}/bank/reveal", null);
        reveal.EnsureSuccessStatusCode();
        Assert.Equal("0012345678904471", (await reveal.Content.ReadFromJsonAsync<RevealedBankNumberDto>())!.AccountNumber);
        var entry = await InDbAsync(db => db.AuditLogEntries.IgnoreQueryFilters()
            .Where(e => e.Action == AuditAction.PreboardingBankNumberRevealed).OrderByDescending(e => e.OccurredAt).FirstAsync());
        Assert.Equal(before + 1, await InDbAsync(db => db.AuditLogEntries.IgnoreQueryFilters().CountAsync(e => e.Action == AuditAction.PreboardingBankNumberRevealed)));
        Assert.Equal("Candidate", entry.TargetType);
        Assert.Null(entry.TargetName);
        Assert.Equal(_factory.RecruiterUserId, entry.ActorUserId);
        Assert.DoesNotContain("4471", entry.DetailsJson ?? string.Empty);
    }

    [Fact]
    public async Task Sending_For_Checking_Needs_Every_Required_Item_And_Tells_The_Recruiter_Once_Per_Change()
    {
        var a = await AcceptedAsync("Pre Send");
        await UploadedAsync(a, "NrcFront", Jpeg());
        var early = await a.Browser.PostAsync($"{a.Documents}/send", null);
        Assert.Equal(HttpStatusCode.Conflict, early.StatusCode);
        Assert.Equal("Add both sides of your NRC and your bank account details and your education certificates first.", await DetailOf(early));

        await CompleteAsync(a);
        var sent = await a.Browser.PostAsync($"{a.Documents}/send", null);
        sent.EnsureSuccessStatusCode();
        var docs = (await sent.Content.ReadFromJsonAsync<PortalDocumentsDto>())!;
        Assert.True(docs.Sent);
        Assert.False(docs.CanSend);
        Assert.Equal(3, docs.RequiredItemsReady);

        var again = await a.Browser.PostAsync($"{a.Documents}/send", null);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Contains("Nothing has changed", await DetailOf(again));

        await DrainOutboxAsync();
        var notice = Assert.Single(_factory.Email.Sent, m => m.Subject.StartsWith("Joining documents to check") && m.Subject.Contains(a.Offer.Reference));
        Assert.Equal("recruiter@alpha.test", notice.To);
        Assert.DoesNotContain("4471", notice.PlainTextBody);

        // A change makes it sendable again, and "sent" no longer describes it. (A tick first, as on a real
        // clock: the change is after the sending.)
        _factory.Clock.Now += TimeSpan.FromSeconds(1);
        docs = await UploadedAsync(a, "Photo", Png(), "me-again.png");
        Assert.False(docs.Sent);
        Assert.True(docs.CanSend);
    }

    // ── The recruiter's side ───────────────────────────────────────────────

    [Fact]
    public async Task Only_Recruitment_Staff_Reach_The_Review_And_Only_For_An_Accepted_Offer()
    {
        var a = await AcceptedAsync("Pre Reach");
        await UploadedAsync(a, "NrcFront", Jpeg());
        var fileId = (await ReviewAsync(a)).Documents.Single(d => d.Kind == "NrcFront").Files.Single().Id;

        foreach (var outsider in new[]
                 {
                     _scenario.SalesManager(), _scenario.FinanceApprover(),
                     _scenario.Client(Roles.Interviewer, _factory.HiringManagerUserId),
                 })
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await outsider.GetAsync(a.Review)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await outsider.GetAsync($"{a.Review}/files/{fileId}")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await outsider.PostAsync($"{a.Review}/bank/reveal", null)).StatusCode);
        }
        // A Hiring Manager's own view of the offer carries nothing about documents.
        var managerJson = await _scenario.SalesManager().GetStringAsync($"/api/offers/{a.Offer.Id}");
        Assert.DoesNotContain("NrcFront", managerJson);

        var draft = await OfferTests.DraftAsync(_scenario, Recruiter(), "Pre Reach Draft");
        Assert.Equal(HttpStatusCode.NotFound, (await Recruiter().GetAsync($"/api/offers/{draft.Id}/preboarding")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Recruiter().GetAsync(a.Review)).StatusCode);
    }

    [Fact]
    public async Task Opening_A_File_Is_Recorded_Before_It_Is_Sent_As_The_Type_Detected()
    {
        var a = await AcceptedAsync("Pre Open");
        var bytes = Png();
        await UploadedAsync(a, "NrcFront", bytes, "nrc.jpg");
        var review = await ReviewAsync(a);
        var file = review.Documents.Single(d => d.Kind == "NrcFront").Files.Single();
        Assert.Equal("image/png", file.ContentType);

        var res = await Recruiter().GetAsync($"{a.Review}/files/{file.Id}");
        res.EnsureSuccessStatusCode();
        Assert.Equal("image/png", res.Content.Headers.ContentType!.MediaType);
        Assert.Equal("attachment", res.Content.Headers.ContentDisposition!.DispositionType);
        Assert.Equal($"{a.Offer.Reference}-NrcFront.png", res.Content.Headers.ContentDisposition.FileNameStar ?? res.Content.Headers.ContentDisposition.FileName);
        Assert.Equal(bytes, await res.Content.ReadAsByteArrayAsync());

        var entry = await InDbAsync(db => db.AuditLogEntries.IgnoreQueryFilters()
            .Where(e => e.Action == AuditAction.PreboardingDocumentOpened && e.DetailsJson!.Contains(file.Id.ToString()))
            .SingleAsync());
        Assert.Equal(review.CandidateId, entry.TargetId);
        Assert.Contains("NrcFront", entry.DetailsJson);

        // A replaced file is not openable; a file of another offer is not reached through this one.
        await UploadedAsync(a, "NrcFront", Jpeg());
        Assert.Equal(HttpStatusCode.NotFound, (await Recruiter().GetAsync($"{a.Review}/files/{file.Id}")).StatusCode);
        var other = await AcceptedAsync("Pre Open Other");
        Assert.Equal(HttpStatusCode.NotFound, (await Recruiter().GetAsync($"{other.Review}/files/{(await ReviewAsync(a)).Documents.Single(d => d.Kind == "NrcFront").Files.Single().Id}")).StatusCode);
    }

    [Fact]
    public async Task A_Decision_Names_The_Revision_It_Saw_And_Approval_Is_Final()
    {
        var a = await AcceptedAsync("Pre Revision");
        await UploadedAsync(a, "NrcFront", Jpeg());
        var seen = (await ReviewAsync(a)).Documents.Single(d => d.Kind == "NrcFront").Revision;

        // The candidate replaces it after the page was loaded.
        await UploadedAsync(a, "NrcFront", Jpeg());
        var stale = await ApproveAsync(a, "NrcFront", seen);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Contains("changed this document after the page was loaded", await DetailOf(stale));

        var approved = await ApproveAsync(a, "NrcFront");
        approved.EnsureSuccessStatusCode();
        var doc = (await approved.Content.ReadFromJsonAsync<PreboardingDto>())!.Documents.Single(d => d.Kind == "NrcFront");
        Assert.Equal("Approved", doc.Status);
        Assert.NotNull(doc.ReviewedByName);

        var replace = await UploadAsync(a, "NrcFront", Jpeg());
        Assert.Equal(HttpStatusCode.Conflict, replace.StatusCode);
        Assert.Contains("approved", await DetailOf(replace));
        Assert.Equal(HttpStatusCode.Conflict, (await ApproveAsync(a, "NrcFront")).StatusCode);

        // Nothing to approve: never sent, or bank details without the photo.
        Assert.Equal(HttpStatusCode.Conflict, (await ApproveAsync(a, "NrcBack", 0)).StatusCode);
        (await SaveBankAsync(a)).EnsureSuccessStatusCode();
        var half = await ApproveAsync(a, "BankAccount");
        Assert.Equal(HttpStatusCode.Conflict, half.StatusCode);
        Assert.Contains("bank book photo", await DetailOf(half));
    }

    [Fact]
    public async Task Sending_Back_Emails_The_Candidate_The_Recruiters_Words_And_Shows_Them_Until_Changed()
    {
        var a = await AcceptedAsync("Pre Return");
        await UploadedAsync(a, "Education", Jpeg(), "degree.jpg");
        var revision = (await ReviewAsync(a)).Documents.Single(d => d.Kind == "Education").Revision;

        // Nothing written, nothing sent: refused by validation (whitespace) and by the service (only
        // invisible characters).
        var blank = await Recruiter().PostAsJsonAsync($"{a.Review}/Education/return", new ReturnPreboardingDocumentRequest { Revision = revision, Message = "   " });
        Assert.Equal(HttpStatusCode.BadRequest, blank.StatusCode);
        var invisible = await Recruiter().PostAsJsonAsync($"{a.Review}/Education/return", new ReturnPreboardingDocumentRequest { Revision = revision, Message = "\u200B" });
        Assert.Equal(HttpStatusCode.Conflict, invisible.StatusCode);

        const string message = "ဘွဲ့လက်မှတ် ဓာတ်ပုံသည် မှုန်ဝါးနေပါသည်။\nThe photo is blurred — please retake it.";
        var returned = await Recruiter().PostAsJsonAsync($"{a.Review}/Education/return", new ReturnPreboardingDocumentRequest { Revision = revision, Message = message });
        returned.EnsureSuccessStatusCode();
        Assert.Equal("Returned", (await returned.Content.ReadFromJsonAsync<PreboardingDto>())!.Documents.Single(d => d.Kind == "Education").Status);

        var portal = (await a.Browser.GetFromJsonAsync<PortalDocumentsDto>(a.Documents))!.Documents.Single(d => d.Kind == "Education");
        Assert.Equal("Returned", portal.Status);
        Assert.Equal(message, portal.Message);

        await DrainOutboxAsync();
        var email = Assert.Single(_factory.Email.Sent, m => m.Subject.StartsWith("Please send a document again") && m.PlainTextBody.Contains(a.Token));
        Assert.StartsWith("applicant.", email.To);
        Assert.Contains("> ဘွဲ့လက်မှတ် ဓာတ်ပုံသည် မှုန်ဝါးနေပါသည်။\n> The photo is blurred — please retake it.", email.PlainTextBody);
        Assert.Contains($"https://careers.test/offer/{a.Token}", email.PlainTextBody);

        // Once the candidate changes it, it is waiting for review again and the message is not shown.
        await UploadedAsync(a, "Education", Jpeg(), "degree-retaken.jpg");
        portal = (await a.Browser.GetFromJsonAsync<PortalDocumentsDto>(a.Documents))!.Documents.Single(d => d.Kind == "Education");
        Assert.Equal("Submitted", portal.Status);
        Assert.Null(portal.Message);
    }

    [Fact]
    public async Task The_Handoff_Fires_Once_When_The_Last_Required_Document_Is_Approved_And_Carries_Only_What_Each_Team_Needs()
    {
        await SettingsAsync(photoRequired: false, it: "it-helpdesk@alpha.test", admin: "admin-desk@alpha.test");
        var a = await AcceptedAsync("Pre Handoff");
        await CompleteAsync(a);
        var photo = Png(4096);
        await UploadedAsync(a, "Photo", photo, "me.png");

        Assert.All((await ReviewAsync(a)).Handoff, t => Assert.Equal("Held", t.Status));
        foreach (var kind in new[] { "NrcFront", "NrcBack", "BankAccount", "Photo" }) (await ApproveAsync(a, kind)).EnsureSuccessStatusCode();
        Assert.Null((await ReviewAsync(a)).HandoffAt); // education is still outstanding

        var last = await ApproveAsync(a, "Education");
        last.EnsureSuccessStatusCode();
        var review = (await last.Content.ReadFromJsonAsync<PreboardingDto>())!;
        Assert.NotNull(review.HandoffAt);
        Assert.Equal(review.RequiredDocuments, review.RequiredApproved);
        Assert.All(review.Handoff, t => Assert.Equal("Queued", t.Status));

        await DrainOutboxAsync();
        var name = a.Offer.CandidateName;
        var it = Assert.Single(_factory.Email.Sent, m => m.To == "it-helpdesk@alpha.test" && m.PlainTextBody.Contains(name));
        var admin = Assert.Single(_factory.Email.Sent, m => m.To == "admin-desk@alpha.test" && m.PlainTextBody.Contains(name));
        Assert.Null(it.Attachments);
        var attachment = Assert.Single(admin.Attachments!);
        Assert.Equal(photo, attachment.Content);
        Assert.Equal("staff-card-photo.png", attachment.FileName);
        foreach (var mail in new[] { it, admin })
        {
            // No salary, NRC or bank details cross.
            Assert.DoesNotContain("4471", mail.PlainTextBody);
            Assert.DoesNotContain(a.Offer.BaseSalary!.Value.ToString("#,##0"), mail.PlainTextBody);
            Assert.DoesNotContain("KBZ", mail.PlainTextBody);
        }
        Assert.All((await ReviewAsync(a)).Handoff, t => Assert.Equal("Sent", t.Status));

        // Once: nothing more is queued however the documents are touched afterwards.
        Assert.Equal(2, await InDbAsync(db => db.OutboundMessages.IgnoreQueryFilters()
            .CountAsync(m => m.Kind == OutboundMessageKind.PreboardingHandoff && m.SubjectId == a.Offer.Id)));
    }

    [Fact]
    public async Task A_Team_Without_An_Address_Is_Not_Sent_Anything_And_The_Screen_Says_So()
    {
        await SettingsAsync(photoRequired: false, it: "it-only@alpha.test", admin: null);
        var a = await AcceptedAsync("Pre No Admin");
        Assert.Equal(new[] { "Held", "NotSetUp" }, (await ReviewAsync(a)).Handoff.Select(t => t.Status));

        await CompleteAsync(a);
        foreach (var kind in new[] { "NrcFront", "NrcBack", "BankAccount", "Education" }) (await ApproveAsync(a, kind)).EnsureSuccessStatusCode();
        var review = await ReviewAsync(a);
        Assert.Equal("Queued", review.Handoff.Single(t => t.Team == "IT").Status);
        Assert.Equal("NotSent", review.Handoff.Single(t => t.Team == "Admin").Status);
        Assert.Equal(1, await InDbAsync(db => db.OutboundMessages.IgnoreQueryFilters()
            .CountAsync(m => m.Kind == OutboundMessageKind.PreboardingHandoff && m.SubjectId == a.Offer.Id)));
        await SettingsAsync(photoRequired: false, it: null, admin: null);
    }

    [Fact]
    public async Task Remind_Lists_What_Is_Missing_At_Most_Once_A_Day()
    {
        var a = await AcceptedAsync("Pre Remind");
        await UploadedAsync(a, "NrcFront", Jpeg());
        await UploadedAsync(a, "NrcBack", Jpeg());

        var first = await Recruiter().PostAsync($"{a.Review}/remind", null);
        first.EnsureSuccessStatusCode();
        Assert.False((await first.Content.ReadFromJsonAsync<PreboardingDto>())!.CanRemind);
        var again = await Recruiter().PostAsync($"{a.Review}/remind", null);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Contains("last 24 hours", await DetailOf(again));

        await DrainOutboxAsync();
        var email = Assert.Single(_factory.Email.Sent, m => m.Subject.StartsWith("Documents needed before your first day") && m.PlainTextBody.Contains(a.Token));
        Assert.Contains("- your bank account details", email.PlainTextBody);
        Assert.Contains("- your education certificates", email.PlainTextBody);
        Assert.DoesNotContain("NRC", email.PlainTextBody);

        _factory.Clock.Now += TimeSpan.FromHours(25);
        (await Recruiter().PostAsync($"{a.Review}/remind", null)).EnsureSuccessStatusCode();

        // With everything sent there is nothing to remind them of.
        var b = await AcceptedAsync("Pre Remind Done");
        await CompleteAsync(b);
        Assert.False((await ReviewAsync(b)).CanRemind);
        Assert.Equal(HttpStatusCode.Conflict, (await Recruiter().PostAsync($"{b.Review}/remind", null)).StatusCode);
    }

    [Fact]
    public async Task An_Accepted_Offers_Link_Outlives_Its_Reply_By_Date_Until_The_Joining_Date()
    {
        var a = await AcceptedAsync("Pre Window");
        await UploadedAsync(a, "NrcFront", Jpeg());

        // Past the reply-by date (7 days) but before joining (30): still open, for the documents.
        _factory.Clock.Now += TimeSpan.FromDays(10);
        var browser = await UnlockedAsync(a.Offer.Id, a.Token);
        var offer = (await browser.GetFromJsonAsync<PortalOfferDto>($"/api/public/offers/{a.Token}/offer"))!;
        Assert.True(offer.DocumentsOpen);
        (await UploadAsync(a with { Browser = browser }, "NrcBack", Jpeg())).EnsureSuccessStatusCode();

        // Past the joining date: the link is closed, and the recruiter can no longer chase or send back.
        _factory.Clock.Now += TimeSpan.FromDays(25);
        Assert.Equal(HttpStatusCode.NotFound, (await browser.GetAsync(a.Documents)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _factory.CreateClient().GetAsync($"/api/public/offers/{a.Token}")).StatusCode);
        var review = await ReviewAsync(a);
        Assert.False(review.WindowOpen);
        Assert.False(review.CanRemind);
        var back = await Recruiter().PostAsJsonAsync($"{a.Review}/NrcBack/return", new ReturnPreboardingDocumentRequest
        {
            Revision = review.Documents.Single(d => d.Kind == "NrcBack").Revision, Message = "Please retake it.",
        });
        Assert.Equal(HttpStatusCode.Conflict, back.StatusCode);
        Assert.Contains("joining date has passed", await DetailOf(back));
    }

    // ── Settings ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Settings_Are_Company_Wide_Validated_And_Audited()
    {
        Assert.Equal(HttpStatusCode.Forbidden, (await _scenario.SalesManager().GetAsync("/api/settings/preboarding")).StatusCode);

        var two = await Admin().PutAsJsonAsync("/api/settings/preboarding", new UpdatePreboardingSettingsRequest
        {
            NrcRequired = true, BankAccountRequired = true, EducationRequired = true, PhotoRequired = false,
            ItHandoffEmail = "a@alpha.test, b@alpha.test",
        });
        Assert.Equal(HttpStatusCode.BadRequest, two.StatusCode);
        Assert.Contains("one email address", await DetailOf(two));

        var omitted = await Admin().PutAsJsonAsync("/api/settings/preboarding", new { nrcRequired = true });
        Assert.Equal(HttpStatusCode.BadRequest, omitted.StatusCode);

        var before = await InDbAsync(db => db.AuditLogEntries.IgnoreQueryFilters().CountAsync(e => e.Action == AuditAction.PreboardingSettingsChanged));
        await SettingsAsync(photoRequired: true, it: "it@alpha.test", admin: "admin@alpha.test");
        var saved = (await Admin().GetFromJsonAsync<PreboardingSettingsDto>("/api/settings/preboarding"))!;
        Assert.True(saved.PhotoRequired);
        Assert.Equal("admin@alpha.test", saved.AdminHandoffEmail);
        Assert.Equal(before + 1, await InDbAsync(db => db.AuditLogEntries.IgnoreQueryFilters().CountAsync(e => e.Action == AuditAction.PreboardingSettingsChanged)));

        // Saving the same values again records nothing.
        await SettingsAsync(photoRequired: true, it: "it@alpha.test", admin: "admin@alpha.test");
        Assert.Equal(before + 1, await InDbAsync(db => db.AuditLogEntries.IgnoreQueryFilters().CountAsync(e => e.Action == AuditAction.PreboardingSettingsChanged)));
        await SettingsAsync(photoRequired: false, it: null, admin: null);
    }
}
