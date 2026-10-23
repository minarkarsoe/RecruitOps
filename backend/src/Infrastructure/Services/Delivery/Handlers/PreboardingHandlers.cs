using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RecruitOps.Application.Interfaces;
using RecruitOps.Domain;
using RecruitOps.Domain.Entities;
using RecruitOps.Domain.Enums;
using RecruitOps.Infrastructure.Options;
using RecruitOps.Infrastructure.Persistence;

namespace RecruitOps.Infrastructure.Services.Delivery.Handlers;

/// <summary>What the pre-boarding emails share (ADR-0030 §5b): everything is read live at send time, and
/// an offer that is no longer accepted is not written about.</summary>
public abstract class PreboardingMessageHandler : OfferMessageHandler
{
    protected PreboardingMessageHandler(AppDbContext db, IEmailSender email, TimeProvider clock)
        : base(db, email, clock) { }

    protected sealed override async Task<Built> BuildAsync(OutboundMessage message, OfferContext context, CancellationToken ct)
    {
        if (context.Offer.Status != OfferStatus.Accepted)
            return Built.Instead(DeliveryOutcome.Suppressed(
                $"The offer is {context.Offer.Status.ToString().ToLowerInvariant()}, no longer accepted, so this was not sent."));
        var request = await Db.PreboardingRequests.AsNoTracking().FirstOrDefaultAsync(r => r.OfferId == context.Offer.Id, ct);
        if (request is null)
            return Built.Instead(DeliveryOutcome.Failed("This offer has no pre-boarding record, so there was nothing to send."));
        return await BuildAsync(message, context, request, ct);
    }

    protected abstract Task<Built> BuildAsync(OutboundMessage message, OfferContext context, PreboardingRequest request, CancellationToken ct);

    /// <summary>Required items still missing or sent back, in the candidate's words.</summary>
    protected async Task<List<string>> OutstandingAsync(Guid offerId, PreboardingItems required, CancellationToken ct)
    {
        var rows = await Db.PreboardingDocuments.AsNoTracking().Where(d => d.OfferId == offerId).ToListAsync(ct);
        var ids = rows.Select(r => r.Id).ToList();
        var withFiles = await Db.PreboardingFiles.AsNoTracking()
            .Where(f => ids.Contains(f.PreboardingDocumentId) && f.RemovedAt == null)
            .Select(f => f.PreboardingDocumentId).Distinct().ToListAsync(ct);

        bool Ready(PreboardingDocumentKind kind)
        {
            var row = rows.FirstOrDefault(r => r.Kind == kind);
            if (row is null || row.Status == PreboardingDocumentStatus.Returned) return false;
            if (row.Status == PreboardingDocumentStatus.Approved) return true;
            return withFiles.Contains(row.Id)
                   && (kind != PreboardingDocumentKind.BankAccount || row.BankAccountNumber is not null);
        }

        var names = new List<string>();
        if ((required & PreboardingItems.Nrc) != 0 && !(Ready(PreboardingDocumentKind.NrcFront) && Ready(PreboardingDocumentKind.NrcBack)))
            names.Add("both sides of your NRC");
        if ((required & PreboardingItems.BankAccount) != 0 && !Ready(PreboardingDocumentKind.BankAccount))
            names.Add("your bank account details and a photo of your bank book's first page");
        if ((required & PreboardingItems.Education) != 0 && !Ready(PreboardingDocumentKind.Education))
            names.Add("your education certificates");
        if ((required & PreboardingItems.Photo) != 0 && !Ready(PreboardingDocumentKind.Photo))
            names.Add("a photo for your staff card");
        return names;
    }
}

/// <summary>A pre-boarding email to the candidate: it carries their link, and is not sent once documents
/// can no longer be sent.</summary>
public abstract class PreboardingCandidateHandler : PreboardingMessageHandler
{
    private readonly IOptions<PublicSiteOptions> _site;

    protected PreboardingCandidateHandler(AppDbContext db, IEmailSender email, TimeProvider clock, IOptions<PublicSiteOptions> site)
        : base(db, email, clock) => _site = site;

    protected override async Task<Built> BuildAsync(OutboundMessage message, OfferContext context, PreboardingRequest request, CancellationToken ct)
    {
        var offer = context.Offer;
        if (!Preboarding.IsWindowOpen(offer.JoiningDate, Clock.GetUtcNow(), context.TimeZoneId))
            return Built.Instead(DeliveryOutcome.Suppressed("The joining date passed before this email went out, so the candidate was not emailed."));
        if (offer.PortalToken is null || _site.Value.Normalized() is not { } site)
            return Built.Instead(DeliveryOutcome.Failed(
                "The candidate's link could not be built: the public site's address (PublicSite:BaseUrl) is not configured."));
        return await BuildAsync(message, context, request, $"{site}/offer/{offer.PortalToken}", ct);
    }

    protected abstract Task<Built> BuildAsync(
        OutboundMessage message, OfferContext context, PreboardingRequest request, string link, CancellationToken ct);

    protected static string Footer(OfferContext context, string link) =>
        "Open your link to do it:\n" + link + "\n\n"
        + "The page will ask for a 6-digit code, which we will email to this address when you ask for it. "
        + $"Documents can be sent until {Long(context.Offer.JoiningDate)}.\n\n"
        + "This link is personal to you. Please do not forward it.\n";
}

/// <summary>To the candidate: one document was sent back, with the recruiter's message.</summary>
public sealed class PreboardingReturnedHandler : PreboardingCandidateHandler
{
    public PreboardingReturnedHandler(AppDbContext db, IEmailSender email, TimeProvider clock, IOptions<PublicSiteOptions> site)
        : base(db, email, clock, site) { }

    public override OutboundMessageKind Kind => OutboundMessageKind.PreboardingReturned;

    protected override async Task<Built> BuildAsync(
        OutboundMessage message, OfferContext context, PreboardingRequest request, string link, CancellationToken ct)
    {
        PreboardingDocumentKind? kind = null;
        try
        {
            kind = Preboarding.ParseKind(JsonSerializer.Deserialize<PreboardingReturnedPayload>(message.PayloadJson)?.Kind);
        }
        catch (JsonException)
        {
        }
        if (kind is null) return Built.Instead(DeliveryOutcome.Failed("This message does not say which document was sent back."));

        var document = await Db.PreboardingDocuments.AsNoTracking()
            .FirstOrDefaultAsync(d => d.OfferId == context.Offer.Id && d.Kind == kind, ct);
        if (document?.Status != PreboardingDocumentStatus.Returned || string.IsNullOrWhiteSpace(document.ReturnMessage))
            return Built.Instead(DeliveryOutcome.Suppressed("The candidate had already changed the document, so the request was not sent."));

        var body = new StringBuilder()
            .Append("Dear ").Append(context.CandidateName).Append(",\n\n")
            .Append("The recruitment team at ").Append(context.CompanyName).Append(" has asked you to send this again: ")
            .Append(Preboarding.Label(kind.Value)).Append(".\n\n")
            .Append("Their message:\n");
        // Quoted line by line, so the recruiter's words read as theirs and not as the rest of the email.
        foreach (var line in document.ReturnMessage.ReplaceLineEndings("\n").Split('\n'))
            body.Append("> ").Append(line).Append('\n');
        body.Append('\n').Append(Footer(context, link));

        return Built.Send(new EmailMessage(message.Recipient, $"Please send a document again — {context.CompanyName}", body.ToString()));
    }
}

/// <summary>To the candidate: the required documents still missing.</summary>
public sealed class PreboardingReminderHandler : PreboardingCandidateHandler
{
    public PreboardingReminderHandler(AppDbContext db, IEmailSender email, TimeProvider clock, IOptions<PublicSiteOptions> site)
        : base(db, email, clock, site) { }

    public override OutboundMessageKind Kind => OutboundMessageKind.PreboardingReminder;

    protected override async Task<Built> BuildAsync(
        OutboundMessage message, OfferContext context, PreboardingRequest request, string link, CancellationToken ct)
    {
        var outstanding = await OutstandingAsync(context.Offer.Id, request.Required, ct);
        if (outstanding.Count == 0)
            return Built.Instead(DeliveryOutcome.Suppressed("Everything required had been sent by the time this went out, so no reminder was needed."));

        var body = new StringBuilder()
            .Append("Dear ").Append(context.CandidateName).Append(",\n\n")
            .Append("Before you join ").Append(context.CompanyName).Append(" as ").Append(context.Position)
            .Append(" on ").Append(Long(context.Offer.JoiningDate)).Append(", we still need:\n\n");
        foreach (var item in outstanding) body.Append("- ").Append(item).Append('\n');
        body.Append('\n').Append(Footer(context, link));

        return Built.Send(new EmailMessage(message.Recipient, $"Documents needed before your first day — {context.CompanyName}", body.ToString()));
    }
}

/// <summary>To the recruiter who created the offer: the candidate sent their documents for checking.</summary>
public sealed class PreboardingSubmittedHandler : PreboardingMessageHandler
{
    public PreboardingSubmittedHandler(AppDbContext db, IEmailSender email, TimeProvider clock)
        : base(db, email, clock) { }

    public override OutboundMessageKind Kind => OutboundMessageKind.PreboardingSubmitted;

    protected override string NoAddress => "The recruiter who created this offer has no email address on record, or is no longer recruitment staff, so they were not told.";

    protected override Task<Built> BuildAsync(OutboundMessage message, OfferContext context, PreboardingRequest request, CancellationToken ct)
    {
        // Names and dates only — no document, no number, nothing the email could leak.
        var body = $"{context.CandidateName} sent their joining documents for checking (offer {context.Reference}, {context.Position}, "
                   + $"joining {Long(context.Offer.JoiningDate)}).\n\nOpen the offer's pre-boarding page in RecruitOps to review them.\n";
        return Task.FromResult(Built.Send(new EmailMessage(
            message.Recipient, $"Joining documents to check: {context.CandidateName} ({context.Reference})", body)));
    }
}

/// <summary>To IT or Admin, once every required document is approved (ADR-0030 §5b). IT gets a name, a
/// position and a date; Admin gets a name, a department and a date, and the staff-card photo if one was
/// approved. Nothing else crosses — no salary, NRC or bank details.</summary>
public sealed class PreboardingHandoffHandler : PreboardingMessageHandler
{
    private readonly IFileStorage _storage;

    public PreboardingHandoffHandler(AppDbContext db, IEmailSender email, TimeProvider clock, IFileStorage storage)
        : base(db, email, clock) => _storage = storage;

    public override OutboundMessageKind Kind => OutboundMessageKind.PreboardingHandoff;

    protected override string NoAddress => "No address was set for this team, so nothing was sent.";

    protected override async Task<Built> BuildAsync(OutboundMessage message, OfferContext context, PreboardingRequest request, CancellationToken ct)
    {
        var team = HandoffPayload.TeamOf(message.PayloadJson);
        if (team is null) return Built.Instead(DeliveryOutcome.Failed("This message does not say which team it is for."));
        var starts = Long(context.Offer.JoiningDate);

        if (team == HandoffPayload.It)
        {
            var it = $"Please create an email account and prepare a laptop for {context.CandidateName}, "
                     + $"who joins {context.CompanyName} as {context.Position} on {starts}.\n\n"
                     + "Sent by RecruitOps: their joining documents have been checked by the recruitment team.\n";
            return Built.Send(new EmailMessage(message.Recipient, $"New starter on {starts}: {context.CandidateName} — email account and laptop", it));
        }

        var department = await (
            from a in Db.JobApplications
            join p in Db.JobPostings on a.JobPostingId equals p.Id
            join d in Db.Departments on p.DepartmentId equals d.Id
            where a.Id == context.Offer.JobApplicationId
            select d.Name).FirstOrDefaultAsync(ct);

        // Only an approved photo travels — one nobody has checked does not go onto a staff card.
        var photo = await (
            from d in Db.PreboardingDocuments
            join f in Db.PreboardingFiles on d.Id equals f.PreboardingDocumentId
            where d.OfferId == context.Offer.Id && d.Kind == PreboardingDocumentKind.Photo
                  && d.Status == PreboardingDocumentStatus.Approved && f.RemovedAt == null
            orderby f.UploadedAt descending
            select f).AsNoTracking().FirstOrDefaultAsync(ct);

        var attachments = new List<EmailAttachment>();
        string photoLine;
        if (photo is null)
        {
            photoLine = "No photo was provided for the staff card — please take one on their first day.";
        }
        else
        {
            // Read now, at send time. A storage error escapes and the worker retries.
            await using var stored = await _storage.DownloadAsync(photo.StorageKey, null, ct);
            if (stored is null)
            {
                photoLine = "Their photo could not be attached — please ask the recruitment team for it.";
            }
            else
            {
                using var copy = new MemoryStream();
                await stored.Content.CopyToAsync(copy, ct);
                attachments.Add(new EmailAttachment($"staff-card-photo{Preboarding.Extension(photo.ContentType)}", photo.ContentType, copy.ToArray()));
                photoLine = "Their photo for the staff card is attached.";
            }
        }

        var admin = $"Please prepare a desk and a staff card for {context.CandidateName}, "
                    + $"who joins {OfferLetter.OneLine(department ?? "the company")} at {context.CompanyName} on {starts}.\n\n"
                    + photoLine + "\n\n"
                    + "Sent by RecruitOps: their joining documents have been checked by the recruitment team.\n";
        return Built.Send(new EmailMessage(
            message.Recipient, $"New starter on {starts}: {context.CandidateName} — desk and staff card", admin,
            Attachments: attachments.Count == 0 ? null : attachments));
    }
}
