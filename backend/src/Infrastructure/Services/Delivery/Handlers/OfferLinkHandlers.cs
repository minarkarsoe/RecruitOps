using System.Text;
using Microsoft.Extensions.Options;
using RecruitOps.Application.Interfaces;
using RecruitOps.Domain;
using RecruitOps.Domain.Entities;
using RecruitOps.Domain.Enums;
using RecruitOps.Infrastructure.Options;
using RecruitOps.Infrastructure.Persistence;

namespace RecruitOps.Infrastructure.Services.Delivery.Handlers;

/// <summary>The email that carries the candidate's link — first when the offer is sent, again on a
/// reminder (ADR-0030 §5a). The link is read from the offer at send time, so both carry the same one.</summary>
public abstract class OfferLinkHandler : OfferMessageHandler
{
    private readonly IOptions<PublicSiteOptions> _site;

    protected OfferLinkHandler(AppDbContext db, IEmailSender email, TimeProvider clock, IOptions<PublicSiteOptions> site)
        : base(db, email, clock) => _site = site;

    protected abstract bool IsReminder { get; }

    protected override Task<Built> BuildAsync(OutboundMessage message, OfferContext context, CancellationToken ct)
    {
        var offer = context.Offer;
        if (offer.Status is not (OfferStatus.Sent or OfferStatus.Viewed))
        {
            return Task.FromResult(Built.Instead(DeliveryOutcome.Suppressed(
                $"The offer was {offer.Status.ToString().ToLowerInvariant()} before this email went out, so the candidate was not emailed.")));
        }
        if (OfferPortal.IsPastExpiry(offer.ExpiresOn, Clock.GetUtcNow(), context.TimeZoneId))
        {
            return Task.FromResult(Built.Instead(DeliveryOutcome.Suppressed(
                "The offer expired before this email went out, so the candidate was not emailed.")));
        }
        if (offer.PortalToken is null || _site.Value.Normalized() is not { } site)
        {
            return Task.FromResult(Built.Instead(DeliveryOutcome.Failed(
                "The candidate's link could not be built: the public site's address (PublicSite:BaseUrl) is not configured.")));
        }

        var link = $"{site}/offer/{offer.PortalToken}";
        var body = new StringBuilder()
            .Append("Dear ").Append(context.CandidateName).Append(",\n\n")
            .Append(IsReminder
                ? $"This is a reminder that your offer of employment from {context.CompanyName}, for the position of {context.Position}, is waiting for your answer.\n\n"
                : $"{context.CompanyName} is pleased to offer you the position of {context.Position}.\n\n")
            .Append("Open your offer here:\n").Append(link).Append("\n\n")
            .Append("To keep it private, the page will ask for a 6-digit code, which we will email to this address when you ask for it.\n\n")
            .Append("Please reply by ").Append(Long(offer.ExpiresOn)).Append(". After that the link stops working.\n\n")
            .Append("This link is personal to you. Please do not forward it.\n")
            .ToString();

        var subject = IsReminder
            ? $"Reminder: your offer from {context.CompanyName}"
            : $"Your offer from {context.CompanyName}";
        return Task.FromResult(Built.Send(new EmailMessage(message.Recipient, subject, body)));
    }
}

public sealed class OfferSentHandler : OfferLinkHandler
{
    public OfferSentHandler(AppDbContext db, IEmailSender email, TimeProvider clock, IOptions<PublicSiteOptions> site)
        : base(db, email, clock, site) { }

    public override OutboundMessageKind Kind => OutboundMessageKind.OfferSent;
    protected override bool IsReminder => false;
}

public sealed class OfferReminderHandler : OfferLinkHandler
{
    public OfferReminderHandler(AppDbContext db, IEmailSender email, TimeProvider clock, IOptions<PublicSiteOptions> site)
        : base(db, email, clock, site) { }

    public override OutboundMessageKind Kind => OutboundMessageKind.OfferReminder;
    protected override bool IsReminder => true;
}
