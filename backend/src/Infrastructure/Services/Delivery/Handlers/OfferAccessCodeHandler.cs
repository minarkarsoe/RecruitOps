using System.Text.Json;
using RecruitOps.Application.DTOs;
using RecruitOps.Application.Interfaces;
using RecruitOps.Domain;
using RecruitOps.Domain.Entities;
using RecruitOps.Domain.Enums;
using RecruitOps.Infrastructure.Persistence;

namespace RecruitOps.Infrastructure.Services.Delivery.Handlers;

/// <summary>The 6-digit code that unlocks an offer's link (ADR-0030 §5a). The code travels in the
/// payload because only its keyed hash is kept anywhere else; the delivery log never shows payloads, and
/// the payload is wiped once the message is finished with.</summary>
public sealed class OfferAccessCodeHandler : OfferMessageHandler
{
    public OfferAccessCodeHandler(AppDbContext db, IEmailSender email, TimeProvider clock)
        : base(db, email, clock) { }

    public override OutboundMessageKind Kind => OutboundMessageKind.OfferAccessCode;

    /// <summary>The code is wiped from the row once it is sent (or will never be): it is the credential,
    /// and the outbox is kept as the delivery log (security review M3, 2026-10-23).</summary>
    protected override bool ClearPayloadWhenDone => true;

    protected override Task<Built> BuildAsync(OutboundMessage message, OfferContext context, CancellationToken ct)
    {
        // A code that has already stopped working is worse than none: the candidate would type it and
        // be told it is wrong.
        if (Clock.GetUtcNow() >= message.CreatedAt + OfferPortal.CodeLifetime)
        {
            return Task.FromResult(Built.Instead(DeliveryOutcome.Suppressed(
                "The code expired before it could be sent. The candidate can ask for a new one.")));
        }

        string? code = null;
        try
        {
            code = JsonSerializer.Deserialize<OfferAccessCodePayload>(message.PayloadJson)?.Code;
        }
        catch (JsonException)
        {
            // Falls through to the failure below.
        }
        if (code is null)
            return Task.FromResult(Built.Instead(DeliveryOutcome.Failed("The code could not be read from this message, so it was not sent.")));

        var minutes = (int)OfferPortal.CodeLifetime.TotalMinutes;
        var body =
            $"Your code to open your offer from {context.CompanyName} is:\n\n"
            + $"{code}\n\n"
            + $"It works for {minutes} minutes. If you did not ask for it, you can ignore this email — nobody can open your offer without it.\n";

        // The code is not in the subject: subjects show on lock screens and in notification previews.
        return Task.FromResult(Built.Send(new EmailMessage(
            message.Recipient, $"Your code to open your offer from {context.CompanyName}", body)));
    }
}
