using System.Globalization;
using Microsoft.EntityFrameworkCore;
using RecruitOps.Application.Interfaces;
using RecruitOps.Domain;
using RecruitOps.Domain.Entities;
using RecruitOps.Domain.Enums;
using RecruitOps.Infrastructure.Persistence;

namespace RecruitOps.Infrastructure.Services.Delivery.Handlers;

/// <summary>What every offer email shares (Module 4, ADR-0030 §5a): finding the offer and what it is read
/// through, and handing the rendered message to the transport.
///
/// <para>Like <see cref="InterviewInvitationHandler"/>, these run in the message's tenant scope — the
/// worker entered it before resolving them — so the queries are ordinary filtered queries, and everything
/// is read live: an offer answered or expired while its email waited is suppressed, not sent.</para>
///
/// <para>Candidate mail is plain text: it renders the same on every phone, carries Burmese without a font
/// stack, and a name cannot inject markup into it.</para></summary>
public abstract class OfferMessageHandler : IOutboundMessageHandler
{
    protected OfferMessageHandler(AppDbContext db, IEmailSender email, TimeProvider clock)
    {
        Db = db;
        Email = email;
        Clock = clock;
    }

    protected AppDbContext Db { get; }
    protected IEmailSender Email { get; }
    protected TimeProvider Clock { get; }

    public abstract OutboundMessageKind Kind { get; }

    /// <summary>The offer and what it is read through.</summary>
    protected sealed record OfferContext(
        Offer Offer,
        string Reference,
        string CompanyName,
        string? TimeZoneId,
        string CandidateName,
        string Position);

    /// <summary>Whether the payload is wiped once the message is finished with — sent, suppressed or failed;
    /// kept while it may still be retried. For a payload that is a secret (the code).</summary>
    protected virtual bool ClearPayloadWhenDone => false;

    public async Task<DeliveryOutcome> HandleAsync(OutboundMessage message, CancellationToken ct = default)
    {
        var outcome = await HandleOfferAsync(message, ct);
        // The worker saves this row after the handler returns, so the change goes with the outcome.
        if (ClearPayloadWhenDone && outcome.Kind != DeliveryOutcomeKind.Retry) message.PayloadJson = "{}";
        return outcome;
    }

    private async Task<DeliveryOutcome> HandleOfferAsync(OutboundMessage message, CancellationToken ct)
    {
        if (message.SubjectId is null)
            return DeliveryOutcome.Failed("This message is not attached to an offer, so there was nothing to send.");

        var row = await (
            from o in Db.Offers.AsNoTracking()
            join a in Db.JobApplications on o.JobApplicationId equals a.Id
            join c in Db.Candidates on a.CandidateId equals c.Id
            join p in Db.JobPostings on a.JobPostingId equals p.Id
            where o.Id == message.SubjectId.Value
            select new { Offer = o, CandidateName = c.FullName, Position = p.Title }
        ).FirstOrDefaultAsync(ct);
        if (row is null)
            return DeliveryOutcome.Failed("The offer this message was about no longer exists, so it was not sent.");

        if (string.IsNullOrWhiteSpace(message.Recipient))
            return DeliveryOutcome.Failed(NoAddress);

        var company = await Db.Companies.AsNoTracking().Where(c => c.Id == message.TenantId)
            .Select(c => new { c.Name, c.TimeZoneId }).FirstOrDefaultAsync(ct);

        // Names and titles on one line: a name typed into an anonymous form must not add paragraphs to
        // the email it is quoted in (security review, 2026-10-23).
        var context = new OfferContext(
            row.Offer, $"OFR-{row.Offer.Year}-{row.Offer.Number:D4}",
            OfferLetter.OneLine(company?.Name ?? "the company"), company?.TimeZoneId,
            OfferLetter.OneLine(row.CandidateName), OfferLetter.OneLine(row.Position));

        var built = await BuildAsync(message, context, ct);
        if (built.Outcome is { } outcome) return outcome;

        try
        {
            await Email.SendAsync(built.Message!, ct);
        }
        catch (EmailDeliveryException ex)
        {
            // The transport decides whether it is worth trying again; the worker owns how often.
            return ex.IsPermanent ? DeliveryOutcome.Failed(ex.Message) : DeliveryOutcome.Retry(ex.Message);
        }
        return DeliveryOutcome.Sent();
    }

    /// <summary>What a recruiter reads in the delivery log when there was nowhere to send it.</summary>
    protected virtual string NoAddress => "There is no email address on record for the candidate, so nothing was sent.";

    /// <summary>Either a message to send, or the outcome that replaces sending it (suppressed, failed).</summary>
    protected readonly record struct Built(EmailMessage? Message, DeliveryOutcome? Outcome)
    {
        public static Built Send(EmailMessage message) => new(message, null);
        public static Built Instead(DeliveryOutcome outcome) => new(null, outcome);
    }

    protected abstract Task<Built> BuildAsync(OutboundMessage message, OfferContext context, CancellationToken ct);

    protected static string Long(DateOnly date) => date.ToString("d MMMM yyyy", CultureInfo.InvariantCulture);
}
