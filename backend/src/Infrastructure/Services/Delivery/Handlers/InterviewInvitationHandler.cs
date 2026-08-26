using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RecruitOps.Application.DTOs;
using RecruitOps.Application.Interfaces;
using RecruitOps.Domain.Entities;
using RecruitOps.Domain.Enums;
using RecruitOps.Infrastructure.Persistence;

namespace RecruitOps.Infrastructure.Services.Delivery.Handlers;

/// <summary>Module 3.2 — tells a candidate when their interview is.
///
/// <para><b>The first handler, so it is also the worked example.</b> Notice what it does not do:
/// no <c>IgnoreQueryFilters()</c>, no tenant predicate written by hand, no <c>ICurrentUser</c>. The
/// worker entered this scope's tenant from the message row before resolving anything, so these
/// queries are ordinary queries and the global filters do the isolating (ADR-0026 §4). A handler
/// that finds itself needing to widen a filter is a signal that something upstream is wrong.</para>
///
/// <para><b>Everything is read live.</b> The row only carries what could not be re-read — see
/// <see cref="InterviewInvitationPayload"/>. That is what makes a reschedule work: the same
/// invitation kind is enqueued again and renders the <i>current</i> slot, and an invitation still
/// queued when the round is called off is suppressed rather than sent.</para>
/// </summary>
public sealed class InterviewInvitationHandler : IOutboundMessageHandler
{
    private readonly AppDbContext _db;
    private readonly IEmailSender _email;
    private readonly TimeProvider _clock;
    private readonly ILogger<InterviewInvitationHandler> _logger;

    public InterviewInvitationHandler(
        AppDbContext db,
        IEmailSender email,
        TimeProvider clock,
        ILogger<InterviewInvitationHandler> logger)
    {
        _db = db;
        _email = email;
        _clock = clock;
        _logger = logger;
    }

    public OutboundMessageKind Kind => OutboundMessageKind.InterviewInvitation;

    public async Task<DeliveryOutcome> HandleAsync(OutboundMessage message, CancellationToken ct = default)
    {
        if (message.SubjectId is null)
        {
            return DeliveryOutcome.Failed(
                "This invitation is not attached to an interview, so there was nothing to send.");
        }

        var interview = await _db.Interviews.AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == message.SubjectId.Value, ct);

        if (interview is null)
        {
            // Terminal on purpose. Either the round was deleted or the tenant scope is wrong, and
            // neither gets better by trying again — but the row says so where a recruiter reads it.
            return DeliveryOutcome.Failed(
                "The interview this invitation was for no longer exists, so it was not sent.");
        }

        // Suppressed, not Failed: the system did the right thing. ADR-0026 §2 makes this a
        // first-class status precisely so the delivery log does not colour it red.
        if (interview.Status != InterviewStatus.Scheduled)
        {
            return DeliveryOutcome.Suppressed(
                $"The round was {Lower(interview.Status)} before the invitation went out — "
                + "the candidate was not emailed.");
        }

        // A back-filled round, or a queue that was stuck while the slot came and went. Inviting
        // somebody to an interview that has already happened is worse than saying nothing.
        if (interview.ScheduledStart <= _clock.GetUtcNow())
        {
            return DeliveryOutcome.Suppressed(
                "The interview time had already passed before the invitation could be sent — "
                + "the candidate was not emailed.");
        }

        if (string.IsNullOrWhiteSpace(message.Recipient))
        {
            // The row exists so that "was this candidate told?" has an answer. This is the answer:
            // no, and here is why, visible in the delivery log instead of nowhere.
            return DeliveryOutcome.Failed(
                "There is no email address on record for this candidate, so no invitation could "
                + "be sent. Contact them another way.");
        }

        var application = await _db.JobApplications.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == interview.JobApplicationId, ct);
        if (application is null)
        {
            return DeliveryOutcome.Failed(
                "The application this interview belongs to no longer exists, so nothing was sent.");
        }

        var candidate = await _db.Candidates.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == application.CandidateId, ct);

        var posting = await _db.JobPostings.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == application.JobPostingId, ct);

        // Not ITenantScoped — one row per deployment (ADR-0004) — so it is fetched by id rather
        // than by a filter, and it is allowed to be missing without failing the send.
        var company = await _db.Companies.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == message.TenantId, ct);

        var payload = ReadPayload(message);
        var zone = ResolveZone(payload.TimeZoneId, message.Id);

        var email = Render(
            to: message.Recipient,
            candidateName: candidate?.FullName,
            jobTitle: posting?.Title,
            companyName: company?.Name,
            interview: interview,
            zone: zone,
            isReschedule: payload.IsReschedule);

        try
        {
            await _email.SendAsync(email, ct);
        }
        catch (EmailDeliveryException ex)
        {
            // The transport decides whether it is worth trying again; the worker owns how often.
            // A handler that made up its own retry policy is how a queue develops moods.
            return ex.IsPermanent
                ? DeliveryOutcome.Failed(ex.Message)
                : DeliveryOutcome.Retry(ex.Message);
        }

        return DeliveryOutcome.Sent();
    }

    // ---------------------------------------------------------------- payload

    private InterviewInvitationPayload ReadPayload(OutboundMessage message)
    {
        try
        {
            var payload = JsonSerializer.Deserialize<InterviewInvitationPayload>(message.PayloadJson);
            if (payload is not null) return payload;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex,
                "Invitation {MessageId} has an unreadable payload; falling back to UTC.", message.Id);
        }

        // Deliberately not a failure. The payload only chooses which clock the time is written in,
        // and an invitation labelled UTC still reaches the candidate — refusing to send one over a
        // malformed JSON field would be the worse outcome by a distance.
        return new InterviewInvitationPayload(TimeZoneInfo.Utc.Id);
    }

    private TimeZoneInfo ResolveZone(string? id, Guid messageId)
    {
        if (string.IsNullOrWhiteSpace(id)) return TimeZoneInfo.Utc;

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            // Windows and Linux both accept IANA ids on .NET 6+, so this means the id itself is
            // wrong. Send in UTC and say so in the body rather than not sending at all.
            _logger.LogWarning(ex,
                "Invitation {MessageId} names an unknown time zone '{TimeZoneId}'; using UTC.",
                messageId, id);
            return TimeZoneInfo.Utc;
        }
    }

    // ---------------------------------------------------------------- rendering

    /// <summary>Plain text, English, no HTML.
    ///
    /// <para>Plain text because it renders identically on every phone, carries Burmese without a
    /// font stack, and — the reason that settles it — cannot be injected into by a candidate's own
    /// name, which is the one value here that an outsider controls.</para>
    ///
    /// <para>English only. The posting-level language choice drawn in
    /// <c>design/internal/postings.html</c> has no backing field yet, and guessing a candidate's
    /// language from nothing would be worse than one clear language. Recorded as a gap.</para></summary>
    private static EmailMessage Render(
        string to,
        string? candidateName,
        string? jobTitle,
        string? companyName,
        Interview interview,
        TimeZoneInfo zone,
        bool isReschedule)
    {
        var hasTitle = !string.IsNullOrWhiteSpace(jobTitle);

        // Two forms, because one does not fit both places. A subject wants the bare title —
        // "Interview invitation — Collections Officer (Field)". A sentence does not:
        // "Thank you for your interest in Collections Officer (Field)" reads as though a word
        // went missing. Found by rendering one and reading it, not by a test.
        var role = hasTitle ? jobTitle! : "the role you applied for";
        var rolePhrase = hasTitle ? $"the {jobTitle} role" : "the role you applied for";

        var greeting = string.IsNullOrWhiteSpace(candidateName) ? "Hello" : $"Dear {candidateName}";

        var start = TimeZoneInfo.ConvertTime(interview.ScheduledStart, zone);
        var end = start.AddMinutes(interview.DurationMinutes);
        var offset = FormatOffset(start.Offset);

        var subject = isReschedule
            ? $"Your interview time has changed — {role}"
            : $"Interview invitation — {role}";

        var body = new StringBuilder();
        body.AppendLine(greeting + ",");
        body.AppendLine();

        body.AppendLine(isReschedule
            ? $"The interview we arranged with you for {rolePhrase} has been moved. The new details are below."
            : $"Thank you for your interest in {rolePhrase}. We would like to invite you to an interview.");
        body.AppendLine();

        body.AppendLine(Row("Date:", start.ToString("dddd, d MMMM yyyy", CultureInfo.InvariantCulture)));
        body.AppendLine(Row("Time:", $"{Time(start)} to {Time(end)} ({offset})"));
        body.AppendLine(Row("Round:", interview.Round.ToString(CultureInfo.InvariantCulture)));

        var (label, detail) = LocationLine(interview);
        if (detail is not null) body.AppendLine(Row(label, detail));

        body.AppendLine();
        body.AppendLine("If this time does not suit you, reply to this email and we will arrange another.");
        body.AppendLine();
        body.AppendLine("Kind regards,");
        body.AppendLine(string.IsNullOrWhiteSpace(companyName) ? "Recruitment" : $"{companyName} Recruitment");

        return new EmailMessage(to, subject, body.ToString());
    }

    /// <summary>What <c>Interview.Location</c> means depends on the mode — that is why the mode is
    /// an enum. A "Location: https://meet…" line reads as a mistake.</summary>
    private static (string Label, string? Detail) LocationLine(Interview interview) => interview.Mode switch
    {
        InterviewMode.Video => ("Join:", Blank(interview.Location)
            ? "we will send you a link before the interview"
            : interview.Location),
        InterviewMode.Phone => ("Call:", Blank(interview.Location)
            ? "we will call you on the number you gave us"
            : $"we will call you on {interview.Location}"),
        _ => ("Where:", Blank(interview.Location) ? null : interview.Location),
    };

    private static bool Blank(string? value) => string.IsNullOrWhiteSpace(value);

    /// <summary>One label-and-value line in the details block, aligned so the four of them read as
    /// a block on a phone rather than as a paragraph.</summary>
    private static string Row(string label, string value) => $"  {label,-9}{value}";

    private static string Time(DateTimeOffset value) =>
        value.ToString("h:mm tt", CultureInfo.InvariantCulture);

    /// <summary>"UTC+06:30". The zone's own display name is not used: it differs between Windows
    /// and Linux ICU data, so it would be one more thing that reads differently in production
    /// than in the test that pinned it. An offset is unambiguous everywhere.</summary>
    private static string FormatOffset(TimeSpan offset)
    {
        if (offset == TimeSpan.Zero) return "UTC";
        var sign = offset < TimeSpan.Zero ? "-" : "+";
        var absolute = offset.Duration();
        return $"UTC{sign}{absolute.Hours:00}:{absolute.Minutes:00}";
    }

    private static string Lower(InterviewStatus status) =>
        status.ToString().ToLowerInvariant();
}
