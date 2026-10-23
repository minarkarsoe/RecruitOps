using System.Text;
using Microsoft.EntityFrameworkCore;
using RecruitOps.Application.Interfaces;
using RecruitOps.Domain;
using RecruitOps.Domain.Entities;
using RecruitOps.Domain.Enums;
using RecruitOps.Infrastructure.Persistence;

namespace RecruitOps.Infrastructure.Services.Delivery.Handlers;

/// <summary>To the candidate, once they answer: on accepting, a copy of exactly what they signed; on
/// declining, an acknowledgement (ADR-0030 §5a).</summary>
public sealed class OfferAnsweredHandler : OfferMessageHandler
{
    public OfferAnsweredHandler(AppDbContext db, IEmailSender email, TimeProvider clock)
        : base(db, email, clock) { }

    public override OutboundMessageKind Kind => OutboundMessageKind.OfferAnswered;

    protected override async Task<Built> BuildAsync(OutboundMessage message, OfferContext context, CancellationToken ct)
    {
        var offer = context.Offer;
        if (offer.Status == OfferStatus.Rejected)
        {
            var declined =
                $"Dear {context.CandidateName},\n\n"
                + $"You declined the offer of the position of {context.Position} at {context.CompanyName} on {AnsweredOn(offer, context)}.\n\n"
                + "Thank you for letting us know, and for the time you gave to the process.\n";
            return Built.Send(new EmailMessage(message.Recipient, $"You declined the offer from {context.CompanyName}", declined));
        }

        if (offer.Status != OfferStatus.Accepted)
            return Built.Instead(DeliveryOutcome.Failed($"The offer is {offer.Status}, not answered, so there was nothing to confirm."));

        var signature = await Db.OfferSignatures.AsNoTracking().FirstOrDefaultAsync(s => s.OfferId == offer.Id, ct);
        if (signature is null)
            return Built.Instead(DeliveryOutcome.Failed("The offer is accepted but its signature record is missing, so no copy was sent."));

        // The letter as signed, from the signature record — never re-rendered.
        var body = new StringBuilder()
            .Append("Dear ").Append(context.CandidateName).Append(",\n\n")
            .Append("You accepted the offer of the position of ").Append(context.Position).Append(" at ").Append(context.CompanyName)
            .Append(", signing as \"").Append(signature.TypedName).Append("\" on ").Append(AnsweredOn(offer, context)).Append(".\n\n")
            .Append("This is the letter you signed:\n\n")
            .Append("----------------------------------------\n")
            .Append(signature.LetterText).Append('\n')
            .Append("----------------------------------------\n\n")
            .Append("Fingerprint of this letter (SHA-256): ").Append(signature.LetterSha256).Append('\n')
            .ToString();
        return Built.Send(new EmailMessage(message.Recipient, $"Your signed offer from {context.CompanyName}", body));
    }

    private static string AnsweredOn(Offer offer, OfferContext context) =>
        Long(OfferPortal.Today(offer.RespondedAt ?? offer.UpdatedAt ?? offer.CreatedAt, context.TimeZoneId));
}

/// <summary>To the recruiter who created the offer: the candidate has answered.</summary>
public sealed class OfferAnswerNoticeHandler : OfferMessageHandler
{
    public OfferAnswerNoticeHandler(AppDbContext db, IEmailSender email, TimeProvider clock)
        : base(db, email, clock) { }

    public override OutboundMessageKind Kind => OutboundMessageKind.OfferAnswerNotice;

    protected override string NoAddress => "The recruiter who created this offer has no email address on record, so they were not told.";

    protected override Task<Built> BuildAsync(OutboundMessage message, OfferContext context, CancellationToken ct)
    {
        var offer = context.Offer;
        var verb = offer.Status switch
        {
            OfferStatus.Accepted => "accepted",
            OfferStatus.Rejected => "declined",
            _ => null,
        };
        if (verb is null)
            return Task.FromResult(Built.Instead(DeliveryOutcome.Failed($"The offer is {offer.Status}, not answered, so there was nothing to report.")));

        var body = new StringBuilder()
            .Append(context.CandidateName).Append(' ').Append(verb).Append(" offer ").Append(context.Reference)
            .Append(" (").Append(context.Position).Append(").\n");
        if (offer.Status == OfferStatus.Rejected && !string.IsNullOrWhiteSpace(offer.DeclineReason))
        {
            // Quoted line by line, so the candidate's words cannot pass for the rest of the email.
            body.Append("\nTheir reason, in their words:\n");
            foreach (var line in offer.DeclineReason.ReplaceLineEndings("\n").Split('\n'))
                body.Append("> ").Append(line).Append('\n');
        }
        body.Append("\nOpen Offers in RecruitOps to see it.\n");

        return Task.FromResult(Built.Send(new EmailMessage(
            message.Recipient, $"{context.CandidateName} {verb} offer {context.Reference}", body.ToString())));
    }
}
