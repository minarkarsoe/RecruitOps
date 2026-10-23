using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using RecruitOps.Application.Common;
using RecruitOps.Application.DTOs;
using RecruitOps.Application.Interfaces;
using RecruitOps.Domain;
using RecruitOps.Domain.Entities;
using RecruitOps.Domain.Enums;
using RecruitOps.Infrastructure.Options;
using RecruitOps.Infrastructure.Persistence;

namespace RecruitOps.Infrastructure.Services;

/// <summary>The candidate's offer link (Module 4.3, ADR-0030 §5a), drawn in
/// <c>design/public/offer-portal.html</c>.
///
/// <para>⚠️ <b>Anonymous, like <see cref="PublicJobService"/>, and built the same way.</b> No request
/// here carries a tenant claim, so the global filters would return nothing: every read uses
/// <c>IgnoreQueryFilters()</c> and re-applies the tenant the <b>token</b> resolved to, and every row
/// written sets its <c>TenantId</c> explicitly. The token establishes the company; nothing is read or
/// written outside it.</para>
///
/// <para><b>Three keys, each checked where it is used.</b> The token finds the offer and shows only the
/// company and a masked address. The emailed code buys a session. The session — and only the session —
/// reads the terms and answers. Every method that reads or answers checks the session itself rather
/// than trusting a caller to have checked it.</para>
///
/// <para><b>Limits are counted after the write, never before.</b> A limit read and then acted on lets a
/// burst of parallel requests all see the same count and all pass (database and security reviews,
/// 2026-10-23). So a try at a code, and a request for one, is written first and then counted.</para>
///
/// <para>Refusals written for the candidate are <see cref="PortalRefusalException"/>s; a missing or
/// expired session is an <see cref="UnauthorizedAccessException"/>.</para>
///
/// <para>Joining documents, after accepting (ADR-0030 §5b), are in <c>OfferPortalService.Documents.cs</c>.</para></summary>
public partial class OfferPortalService : IOfferPortalService
{
    private const string AnsweredMeanwhile = "This offer was answered a moment ago. Reload the page to see it.";

    /// <summary>One answer for every way a code can fail once it has been tried — so no response, not even
    /// one produced by a race, tells a guesser that the code they sent was the right one.</summary>
    private const string CodeStopped = "That code has stopped working. Send yourself a new one.";

    private readonly AppDbContext _db;
    private readonly TimeProvider _clock;
    private readonly IOptions<SmtpOptions> _smtp;
    private readonly OfferPortalSecret _secret;
    private readonly IFileStorage _storage;
    private readonly ILogger<OfferPortalService> _logger;

    public OfferPortalService(
        AppDbContext db, TimeProvider clock, IOptions<SmtpOptions> smtp, OfferPortalSecret secret,
        IFileStorage storage, ILogger<OfferPortalService> logger)
    {
        _db = db;
        _clock = clock;
        _smtp = smtp;
        _secret = secret;
        _storage = storage;
        _logger = logger;
    }

    /// <summary>What the offer is read through, in the offer's own company.</summary>
    private sealed record Context(
        string CompanyName,
        string? TimeZoneId,
        string CandidateName,
        string Position,
        string DepartmentName,
        PreboardingItems PreboardingRequired);

    // ── Finding the offer ───────────────────────────────────────────────────

    /// <summary>The sent offer a token names, tracked, with what it is read through — or null: for an
    /// unknown, malformed or never-sent token alike, and for an answered offer past its reply-by date.
    /// The link closes on that date whatever the answer was; the candidate's copy was emailed (security
    /// review M1, 2026-10-23) — except that an accepted offer's stays open to the joining date, for the
    /// documents (§5b). An unanswered offer past it becomes Expired here — there is no sweep job.</summary>
    private async Task<(Offer Offer, Context Context)?> LoadAsync(string token, CancellationToken ct)
    {
        if (!OfferPortal.LooksLikeToken(token)) return null;
        var offer = await _db.Offers.IgnoreQueryFilters().FirstOrDefaultAsync(o => o.PortalToken == token, ct);

        // Only sending sets a token, so this is belt and braces: a Draft or an offer awaiting approval
        // never reaches a candidate.
        if (offer?.Status is not (OfferStatus.Sent or OfferStatus.Viewed or OfferStatus.Accepted
            or OfferStatus.Rejected or OfferStatus.Expired))
        {
            return null;
        }

        var context = await ContextAsync(offer, ct);
        if (offer.Status == OfferStatus.Accepted)
        {
            var openUntil = Preboarding.LinkOpenUntil(offer.ExpiresOn, offer.JoiningDate);
            return OfferPortal.IsPastExpiry(openUntil, _clock.GetUtcNow(), context.TimeZoneId) ? null : (offer, context);
        }
        if (OfferPortal.IsPastExpiry(offer.ExpiresOn, _clock.GetUtcNow(), context.TimeZoneId))
        {
            if (offer.Status is OfferStatus.Accepted or OfferStatus.Rejected) return null;
            if (offer.Status is OfferStatus.Sent or OfferStatus.Viewed)
            {
                offer.Status = OfferStatus.Expired;
                try
                {
                    await _db.SaveChangesAsync(ct);
                }
                catch (DbUpdateConcurrencyException)
                {
                    // Answered or expired by another request a moment ago: read what won.
                    await _db.Entry(offer).ReloadAsync(ct);
                    if (offer.Status is OfferStatus.Accepted or OfferStatus.Rejected) return null;
                }
            }
        }
        return (offer, context);
    }

    private async Task<Context> ContextAsync(Offer offer, CancellationToken ct)
    {
        var tenantId = offer.TenantId;
        var row = await (
            from a in _db.JobApplications.IgnoreQueryFilters()
            join c in _db.Candidates.IgnoreQueryFilters() on a.CandidateId equals c.Id
            join p in _db.JobPostings.IgnoreQueryFilters() on a.JobPostingId equals p.Id
            join d in _db.Departments.IgnoreQueryFilters() on p.DepartmentId equals d.Id
            where a.Id == offer.JobApplicationId
                  && a.TenantId == tenantId && c.TenantId == tenantId && p.TenantId == tenantId && d.TenantId == tenantId
            select new { c.FullName, p.Title, DepartmentName = d.Name }
        ).AsNoTracking().FirstAsync(ct);

        // Not ITenantScoped — one row per company, fetched by id.
        var company = await _db.Companies.AsNoTracking().Where(c => c.Id == tenantId)
            .Select(c => new { c.Name, c.TimeZoneId, c.PreboardingRequired }).FirstOrDefaultAsync(ct);

        return new Context(
            company?.Name ?? string.Empty, company?.TimeZoneId, row.FullName, row.Title, row.DepartmentName,
            company?.PreboardingRequired ?? Preboarding.DefaultRequired);
    }

    private string? ContactEmail()
    {
        var from = _smtp.Value.FromAddress.Trim();
        return from.Length == 0 ? null : from;
    }

    private static string Long(DateOnly date) => date.ToString("d MMMM yyyy", CultureInfo.InvariantCulture);

    private static PortalRefusalException Expired(Offer offer, string? then = null) =>
        new($"This offer expired on {Long(offer.ExpiresOn)}{then}.");

    // ── The link ────────────────────────────────────────────────────────────

    public async Task<PortalOfferLinkDto?> OpenAsync(string token, CancellationToken ct = default)
    {
        if (await LoadAsync(token, ct) is not var (offer, context)) return null;

        return new PortalOfferLinkDto(
            context.CompanyName,
            offer.Status == OfferStatus.Expired ? "Expired" : "Open",
            string.IsNullOrWhiteSpace(offer.SentToEmail) ? null : OfferPortal.MaskEmail(offer.SentToEmail),
            offer.ExpiresOn,
            ContactEmail());
    }

    // ── The code ────────────────────────────────────────────────────────────

    public async Task<bool> SendCodeAsync(string token, CancellationToken ct = default)
    {
        if (await LoadAsync(token, ct) is not var (offer, _)) return false;
        if (offer.Status == OfferStatus.Expired) throw Expired(offer, ", so no code can be sent");

        // To the address the offer was sent to, and nowhere else — not to anything typed here, and not to
        // whatever the candidate record says now.
        if (string.IsNullOrWhiteSpace(offer.SentToEmail))
            throw new PortalRefusalException("There is no email address for this offer, so we can't send you a code. Please contact the recruitment team.");

        var now = _clock.GetUtcNow();
        var code = OfferPortal.NewCode();
        // CreatedAt from the same clock as every limit measured against it — BaseEntity's default reads
        // the machine's, and the daily count and the code handler's lifetime check both compare with it.
        var access = new OfferPortalAccess
        {
            TenantId = offer.TenantId,
            OfferId = offer.Id,
            CodeExpiresAt = now + OfferPortal.CodeLifetime,
            CreatedAt = now,
        };
        access.CodeHash = OfferPortal.CodeHash(_secret.Key, access.Id, code);

        // The code and its email are committed together: a code that exists has been queued for the
        // candidate. Saved apart, a request cut off in between left a "latest" code nobody was ever sent,
        // which silently replaced the one the candidate had (security re-review N1, 2026-10-23).
        var message = new OutboundMessage
        {
            TenantId = offer.TenantId,
            Kind = OutboundMessageKind.OfferAccessCode,
            Recipient = offer.SentToEmail,
            SubjectType = nameof(Offer),
            SubjectId = offer.Id,
            // Cleared by the handler once the message is finished with (OfferAccessCodeHandler).
            PayloadJson = JsonSerializer.Serialize(new OfferAccessCodePayload(code)),
            Status = OutboundMessageStatus.Pending,
            NextAttemptAt = now,
            CreatedAt = now,
        };
        _db.OfferPortalAccesses.Add(access);
        _db.OutboundMessages.Add(message);
        await _db.SaveChangesAsync(ct);

        // Counted after this request's own row is committed: of a burst of requests, only the first five
        // rows committed can see five or fewer.
        var since = now - OfferPortal.CodeWindow;
        var inWindow = await _db.OfferPortalAccesses.IgnoreQueryFilters()
            .CountAsync(a => a.TenantId == offer.TenantId && a.OfferId == offer.Id && a.CreatedAt > since, ct);
        if (inWindow > OfferPortal.MaxCodesPerDay)
        {
            // Over the limit: this code and its email never existed. Removed whatever happens to the
            // request — and if the removal itself never runs, what is left is a code the candidate is
            // emailed, which is the safe way round. (A burst can refuse one request too many while other
            // refused rows are still being removed.)
            _db.OfferPortalAccesses.Remove(access);
            _db.OutboundMessages.Remove(message);
            await _db.SaveChangesAsync(CancellationToken.None);
            throw new PortalRefusalException(
                $"You've asked for {OfferPortal.MaxCodesPerDay} codes in the last 24 hours. Use the most recent one, or try again tomorrow.");
        }

        return true;
    }

    public async Task<PortalSession?> VerifyAsync(string token, string code, CancellationToken ct = default)
    {
        if (await LoadAsync(token, ct) is not var (offer, _)) return null;
        if (offer.Status == OfferStatus.Expired) throw Expired(offer);

        var typed = OfferPortal.NormalizeCode(code)
                    ?? throw new PortalRefusalException("Enter the 6-digit code from the email.");

        // Only the most recent code counts: sending a new one replaces the old.
        var access = await _db.OfferPortalAccesses.IgnoreQueryFilters()
            .Where(a => a.TenantId == offer.TenantId && a.OfferId == offer.Id)
            .OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id)
            .FirstOrDefaultAsync(ct)
            ?? throw new PortalRefusalException("Ask for a code first.");

        var now = _clock.GetUtcNow();
        if (access.VerifiedAt is not null)
            throw new PortalRefusalException("That code has already been used. Send yourself a new one.");
        if (access.CodeExpiresAt <= now)
            throw new PortalRefusalException("That code has expired. Send yourself a new one.");

        // The try is recorded and committed BEFORE the code is compared, then counted. Inserts never
        // conflict, so every parallel guess is counted; a try is compared only if it is among the first
        // five committed for this code, however the requests interleave.
        _db.OfferPortalAttempts.Add(new OfferPortalAttempt
        {
            TenantId = offer.TenantId,
            OfferPortalAccessId = access.Id,
            CreatedAt = now,
        });
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // The code this try was for was removed a moment ago — an over-limit request's, withdrawn
            // (see SendCodeAsync). Nothing was compared, so nothing is said about the code.
            throw new PortalRefusalException("Please enter the code again.");
        }
        var tries = await _db.OfferPortalAttempts.IgnoreQueryFilters()
            .CountAsync(t => t.TenantId == offer.TenantId && t.OfferPortalAccessId == access.Id, ct);
        if (tries > OfferPortal.MaxCodeAttempts)
            throw new PortalRefusalException(CodeStopped);

        if (!OfferPortal.HashesEqual(access.CodeHash, OfferPortal.CodeHash(_secret.Key, access.Id, typed)))
        {
            var left = OfferPortal.MaxCodeAttempts - tries;
            throw new PortalRefusalException(left > 0
                ? $"That code isn't right. {left} {(left == 1 ? "try" : "tries")} left."
                : CodeStopped);
        }

        // Claimed once: VerifiedAt is a concurrency token, so of two correct entries at the same moment only
        // one opens a session; the other is told the same as any spent code.
        var session = OfferPortal.NewToken();
        access.VerifiedAt = now;
        access.SessionHash = OfferPortal.Sha256Hex(session);
        access.SessionExpiresAt = now + OfferPortal.SessionLifetime;

        // A new code means a new session: any earlier one for this offer ends now.
        await EndSessionsAsync(offer, now, except: access.Id, ct);

        // Opening the link shows nothing, so this — the first unlock — is when the offer was viewed.
        if (offer.Status == OfferStatus.Sent)
        {
            offer.Status = OfferStatus.Viewed;
            offer.ViewedAt = now;
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new PortalRefusalException(CodeStopped);
        }

        return new PortalSession(session, access.SessionExpiresAt.Value);
    }

    /// <summary>Ends this offer's live sessions (tracked; saved with the caller's change).</summary>
    private async Task EndSessionsAsync(Offer offer, DateTimeOffset now, Guid? except, CancellationToken ct)
    {
        var live = await _db.OfferPortalAccesses.IgnoreQueryFilters()
            .Where(a => a.TenantId == offer.TenantId && a.OfferId == offer.Id
                        && a.SessionHash != null && a.SessionExpiresAt > now && a.Id != except)
            .ToListAsync(ct);
        foreach (var a in live) a.SessionExpiresAt = now;
    }

    /// <summary>Throws <see cref="UnauthorizedAccessException"/> unless <paramref name="session"/> is a
    /// live session for this offer.</summary>
    private async Task RequireSessionAsync(Offer offer, string? session, CancellationToken ct)
    {
        if (!OfferPortal.LooksLikeToken(session)) throw new UnauthorizedAccessException();
        var hash = OfferPortal.Sha256Hex(session!); // LooksLikeToken is false for null
        var now = _clock.GetUtcNow();
        var live = await _db.OfferPortalAccesses.IgnoreQueryFilters().AnyAsync(a =>
            a.TenantId == offer.TenantId && a.OfferId == offer.Id
            && a.SessionHash == hash && a.SessionExpiresAt > now, ct);
        if (!live) throw new UnauthorizedAccessException();
    }

    // ── The offer ───────────────────────────────────────────────────────────

    public async Task<PortalOfferDto?> GetOfferAsync(string token, string? session, CancellationToken ct = default)
    {
        if (await LoadAsync(token, ct) is not var (offer, context)) return null;
        await RequireSessionAsync(offer, session, ct);
        if (offer.Status == OfferStatus.Expired) throw Expired(offer);

        return await MapAsync(offer, context, ct);
    }

    private static string Render(Offer offer, Context context) =>
        OfferLetter.Render(offer.LetterBody, new OfferLetter.Terms(
            context.CandidateName, context.Position, context.DepartmentName, context.CompanyName,
            offer.BaseSalary, offer.Allowances, offer.JoiningDate, offer.ProbationMonths, offer.ExpiresOn));

    private async Task<PortalOfferDto> MapAsync(Offer offer, Context context, CancellationToken ct)
    {
        // Once accepted, the letter shown is the one signed — kept, not re-rendered.
        var signature = offer.Status == OfferStatus.Accepted
            ? await _db.OfferSignatures.IgnoreQueryFilters().AsNoTracking()
                .FirstOrDefaultAsync(x => x.TenantId == offer.TenantId && x.OfferId == offer.Id, ct)
            : null;
        var letter = signature?.LetterText ?? Render(offer, context);

        return new PortalOfferDto(
            context.CompanyName, context.Position, context.DepartmentName,
            offer.BaseSalary, offer.Allowances, offer.JoiningDate, offer.ProbationMonths, offer.ExpiresOn,
            offer.Status switch
            {
                OfferStatus.Accepted => "Accepted",
                OfferStatus.Rejected => "Declined",
                _ => "Open",
            },
            letter,
            signature?.LetterSha256 ?? OfferPortal.Sha256Hex(letter),
            signature?.TypedName,
            offer.RespondedAt,
            ContactEmail(),
            offer.Status == OfferStatus.Accepted
                && Preboarding.IsWindowOpen(offer.JoiningDate, _clock.GetUtcNow(), context.TimeZoneId));
    }

    // ── The answer ──────────────────────────────────────────────────────────

    /// <summary>The checks both answers share: a live session, an offer still waiting for an answer.</summary>
    private async Task<(Offer Offer, Context Context)?> AnswerableAsync(string token, string? session, string verb, CancellationToken ct)
    {
        if (await LoadAsync(token, ct) is not var (offer, context)) return null;
        await RequireSessionAsync(offer, session, ct);

        return offer.Status switch
        {
            OfferStatus.Sent or OfferStatus.Viewed => (offer, context),
            OfferStatus.Expired => throw Expired(offer, $", so it can no longer be {verb}"),
            OfferStatus.Accepted => throw new PortalRefusalException("You have already accepted this offer."),
            _ => throw new PortalRefusalException("You declined this offer, so it can no longer be answered here. Please contact the recruitment team if you have changed your mind."),
        };
    }

    public async Task<PortalOfferDto?> AcceptAsync(
        string token, string? session, AcceptOfferRequest request, string? ipAddress, string? userAgent,
        CancellationToken ct = default)
    {
        if (await AnswerableAsync(token, session, "accepted", ct) is not var (offer, context)) return null;

        if (request.Consent != true)
            throw new PortalRefusalException("Tick the box to confirm you have read the letter and accept the offer.");
        var name = OfferPortal.CleanSignatureName(request.TypedName)
                   ?? throw new PortalRefusalException($"Type your full name, in up to {OfferPortal.MaxSignatureNameLength} characters.");

        // What is recorded is what was shown. The page sends back the hash of the letter it displayed; if
        // the letter would render differently now — a name corrected in between — the candidate reads it
        // again. (It is a staleness check, not proof of reading: the server records its own render.)
        var letter = Render(offer, context);
        var hash = OfferPortal.Sha256Hex(letter);
        if (!string.Equals(hash, request.LetterSha256, StringComparison.OrdinalIgnoreCase))
            throw new PortalRefusalException("The offer letter changed since you opened it. Reload the page and read it again before signing.");

        var now = _clock.GetUtcNow();
        _db.OfferSignatures.Add(new OfferSignature
        {
            TenantId = offer.TenantId,
            OfferId = offer.Id,
            TypedName = name,
            SignedAt = now,
            IpAddress = Truncate(ipAddress, 64),
            UserAgent = Truncate(userAgent, 512),
            SentToEmail = offer.SentToEmail,
            LetterText = letter,
            LetterSha256 = hash,
        });

        offer.Status = OfferStatus.Accepted;
        offer.RespondedAt = now;
        offer.ViewedAt ??= now;

        // What the candidate is asked for is fixed now (§5b): the company's choice today, kept.
        _db.PreboardingRequests.Add(new PreboardingRequest
        {
            TenantId = offer.TenantId,
            OfferId = offer.Id,
            Required = context.PreboardingRequired,
            CreatedAt = now,
        });

        // The session stays: signing leads straight on to the documents (§5b).
        await FinishAnswerAsync(offer, now, endSessions: false, ct);
        return await MapAsync(offer, context, ct);
    }

    public async Task<PortalOfferDto?> DeclineAsync(string token, string? session, DeclineOfferRequest request, CancellationToken ct = default)
    {
        if (await AnswerableAsync(token, session, "declined", ct) is not var (offer, context)) return null;

        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        if (reason is { Length: > OfferPortal.MaxDeclineReasonLength })
            throw new PortalRefusalException($"Please keep it under {OfferPortal.MaxDeclineReasonLength:N0} characters.");

        var now = _clock.GetUtcNow();
        offer.Status = OfferStatus.Rejected; // the candidate's answer — ADR-0030 §3
        offer.RespondedAt = now;
        offer.DeclineReason = reason;
        await FinishAnswerAsync(offer, now, endSessions: true, ct);
        return await MapAsync(offer, context, ct);
    }

    /// <summary>Everything an answer does besides changing the offer, saved with it: the candidate's copy
    /// (or acknowledgement), a notice to the recruiter who created the offer, and — on declining — the end
    /// of every session: a declined offer on a shared computer does not stay open to the next person. An
    /// acceptance keeps the session for the documents, which follow at once (§5b).
    /// <para>Two answers at once — two tabs, a double tap — cannot both commit: the offer's status is a
    /// concurrency token and a signature is unique per offer.</para></summary>
    private async Task FinishAnswerAsync(Offer offer, DateTimeOffset now, bool endSessions, CancellationToken ct)
    {
        var recruiter = await RecruiterEmailAsync(offer, ct);

        foreach (var (kind, recipient) in new[]
                 {
                     (OutboundMessageKind.OfferAnswered, offer.SentToEmail),
                     (OutboundMessageKind.OfferAnswerNotice, recruiter),
                 })
        {
            _db.OutboundMessages.Add(new OutboundMessage
            {
                TenantId = offer.TenantId,
                Kind = kind,
                Recipient = recipient ?? string.Empty,
                SubjectType = nameof(Offer),
                SubjectId = offer.Id,
                Status = OutboundMessageStatus.Pending,
                NextAttemptAt = now,
            });
        }

        if (endSessions) await EndSessionsAsync(offer, now, except: null, ct);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new PortalRefusalException(AnsweredMeanwhile);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new PortalRefusalException(AnsweredMeanwhile);
        }
    }

    /// <summary>The recruiter who created the offer — only while they still are one: a deactivated or demoted
    /// creator is not sent news of the offer, which may quote the salary. Null then, and the empty row says
    /// so in the delivery log.</summary>
    private Task<string?> RecruiterEmailAsync(Offer offer, CancellationToken ct)
    {
        var staff = new[] { UserRole.Admin, UserRole.HrDirector, UserRole.Recruiter };
        return _db.Users.IgnoreQueryFilters().AsNoTracking()
            .Where(u => u.Id == offer.CreatedByUserId && u.TenantId == offer.TenantId && u.IsActive && staff.Contains(u.Role))
            .Select(u => u.Email).FirstOrDefaultAsync(ct);
    }

    private static string? Truncate(string? value, int max) =>
        value is null ? null : value.Length <= max ? value : value[..max];
}
