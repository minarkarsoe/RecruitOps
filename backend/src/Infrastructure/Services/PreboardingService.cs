using System.Net.Mail;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RecruitOps.Application.Common;
using RecruitOps.Application.DTOs;
using RecruitOps.Application.Interfaces;
using RecruitOps.Domain;
using RecruitOps.Domain.Entities;
using RecruitOps.Domain.Enums;
using RecruitOps.Infrastructure.Persistence;

namespace RecruitOps.Infrastructure.Services;

/// <summary>The recruiter's side of pre-boarding (Module 4.3, ADR-0030 §5b), drawn in
/// <c>design/internal/preboarding-review.html</c>. The candidate's side is
/// <c>OfferPortalService.Documents.cs</c>.
///
/// <para><b>Recruitment staff only.</b> This is the most sensitive data in the product — NRC scans and
/// bank details. A Hiring Manager, an approver or an interviewer gets null (a 404), whatever offer they
/// can otherwise see: "the correct variant is absence".</para>
///
/// <para><b>Opening a file and revealing an account number are audited before the data leaves</b>
/// (ADR-0027): the entry is saved first, and if it cannot be, nothing is returned.</para>
///
/// <para><b>Decisions name the revision they were made on.</b> A document the candidate changed after the
/// page was loaded is not approved or sent back on the strength of the old one.</para>
///
/// <para><b>The handoff fires once, when the last required document is approved</b> — checked after each
/// approval has committed (so two approvals at the same moment cannot both miss it) and claimed with a
/// compare-and-set on <see cref="PreboardingRequest.HandoffAt"/> (so it cannot fire twice).</para></summary>
public class PreboardingService : IPreboardingService
{
    private const string ChangedMeanwhile = "Someone changed this a moment ago. Reload and try again.";

    private readonly AppDbContext _db;
    private readonly ICurrentUser _user;
    private readonly ICurrentTenant _tenant;
    private readonly IAuditTrail _audit;
    private readonly IFileStorage _storage;
    private readonly TimeProvider _clock;
    private readonly ILogger<PreboardingService> _logger;

    public PreboardingService(
        AppDbContext db, ICurrentUser user, ICurrentTenant tenant, IAuditTrail audit, IFileStorage storage,
        TimeProvider clock, ILogger<PreboardingService> logger)
    {
        _db = db;
        _user = user;
        _tenant = tenant;
        _audit = audit;
        _storage = storage;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>The same set as <c>Policies.RecruitmentStaff</c>; a custom role carries its base role. The
    /// controller requires the policy too — this is the service not trusting that it did.</summary>
    private bool IsRecruitmentStaff =>
        RoleScope.Parse(_user.Role) is UserRole.Admin or UserRole.HrDirector or UserRole.Recruiter;

    // ── Finding it ──────────────────────────────────────────────────────────

    private sealed record Row
    {
        public required Offer Offer { get; init; }
        public required Guid CandidateId { get; init; }
        public required string CandidateName { get; init; }
        public required string Position { get; init; }
        public required string DepartmentName { get; init; }
    }

    private sealed record Loaded(Row Row, PreboardingRequest Request, string? TimeZoneId)
    {
        public Offer Offer => Row.Offer;
        public string Reference => $"OFR-{Offer.Year}-{Offer.Number:D4}";
    }

    /// <summary>An accepted offer with its pre-boarding, for recruitment staff — or null. The offer and the
    /// request are tracked.</summary>
    private async Task<Loaded?> LoadAsync(Guid offerId, CancellationToken ct)
    {
        if (!IsRecruitmentStaff) return null;

        var row = await (
            from o in _db.Offers
            join a in _db.JobApplications on o.JobApplicationId equals a.Id
            join c in _db.Candidates on a.CandidateId equals c.Id
            join p in _db.JobPostings on a.JobPostingId equals p.Id
            join d in _db.Departments on p.DepartmentId equals d.Id
            where o.Id == offerId && o.Status == OfferStatus.Accepted
            select new Row { Offer = o, CandidateId = c.Id, CandidateName = c.FullName, Position = p.Title, DepartmentName = d.Name }
        ).FirstOrDefaultAsync(ct);
        if (row is null) return null;

        var request = await _db.PreboardingRequests.FirstOrDefaultAsync(r => r.OfferId == offerId, ct);
        if (request is null) return null;

        var zone = await _db.Companies.Where(c => c.Id == _tenant.TenantId).Select(c => c.TimeZoneId).FirstOrDefaultAsync(ct);
        return new Loaded(row, request, zone);
    }

    private bool WindowOpen(Loaded loaded) =>
        Preboarding.IsWindowOpen(loaded.Offer.JoiningDate, _clock.GetUtcNow(), loaded.TimeZoneId);

    private Task<List<PreboardingDocument>> DocumentsAsync(Guid offerId, CancellationToken ct) =>
        _db.PreboardingDocuments.Where(d => d.OfferId == offerId).ToListAsync(ct);

    private Task<List<PreboardingFile>> LiveFilesAsync(IEnumerable<Guid> documentIds, CancellationToken ct)
    {
        var ids = documentIds.ToList();
        return _db.PreboardingFiles.Where(f => ids.Contains(f.PreboardingDocumentId) && f.RemovedAt == null)
            .OrderBy(f => f.UploadedAt).ThenBy(f => f.Id).ToListAsync(ct);
    }

    private static bool IsComplete(PreboardingDocument document, int liveFiles) =>
        liveFiles > 0
        && (document.Kind != PreboardingDocumentKind.BankAccount
            || (document.BankName is not null && document.BankAccountName is not null && document.BankAccountNumber is not null));

    // ── Reading ─────────────────────────────────────────────────────────────

    public async Task<PreboardingDto?> GetAsync(Guid offerId, CancellationToken ct = default)
    {
        var loaded = await LoadAsync(offerId, ct);
        return loaded is null ? null : await MapAsync(loaded, ct);
    }

    private async Task<PreboardingDto> MapAsync(Loaded loaded, CancellationToken ct)
    {
        var offer = loaded.Offer;
        var request = loaded.Request;
        var rows = await DocumentsAsync(offer.Id, ct);
        var files = await LiveFilesAsync(rows.Select(r => r.Id), ct);
        var reviewerIds = rows.Where(r => r.ReviewedByUserId is not null).Select(r => r.ReviewedByUserId!.Value).Distinct().ToList();
        // Display names only: a colleague's email is users:read (ADR-0019).
        var reviewers = await _db.Users.Where(u => reviewerIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName }).ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);
        var signedAt = await _db.OfferSignatures.Where(s => s.OfferId == offer.Id)
            .Select(s => (DateTimeOffset?)s.SignedAt).FirstOrDefaultAsync(ct);

        var documents = Preboarding.Kinds.Select(kind =>
        {
            var row = rows.FirstOrDefault(r => r.Kind == kind);
            var own = row is null ? new List<PreboardingFile>() : files.Where(f => f.PreboardingDocumentId == row.Id).ToList();
            return new PreboardingDocumentDto(
                kind.ToString(),
                Preboarding.Label(kind),
                Preboarding.IsRequired(request.Required, kind),
                (row?.Status ?? PreboardingDocumentStatus.Missing).ToString(),
                row is not null && IsComplete(row, own.Count),
                row?.Revision ?? 0,
                row?.ChangedAt,
                row?.ReviewedAt,
                row?.ReviewedByUserId is { } by ? reviewers.GetValueOrDefault(by) : null,
                row?.ReturnMessage,
                own.Select(f => new PreboardingFileDto(f.Id, f.FileName, f.ContentType, f.SizeBytes, f.UploadedAt)).ToList(),
                kind == PreboardingDocumentKind.BankAccount
                    ? new PreboardingBankDto(row?.BankName, row?.BankAccountName,
                        row?.BankAccountNumber is { } n ? Preboarding.MaskAccountNumber(n) : null)
                    : null);
        }).ToList();

        var required = documents.Where(d => d.Required).ToList();
        var outstanding = required.Any(d => d.Status != nameof(PreboardingDocumentStatus.Approved)
                                            && (!d.Complete || d.Status == nameof(PreboardingDocumentStatus.Returned)));
        var now = _clock.GetUtcNow();
        var canRemind = WindowOpen(loaded) && outstanding && !string.IsNullOrWhiteSpace(offer.SentToEmail)
                        && (request.RemindedAt is null || now - request.RemindedAt >= Preboarding.ReminderInterval);

        return new PreboardingDto(
            offer.Id, loaded.Reference, loaded.Row.CandidateId, loaded.Row.CandidateName, loaded.Row.Position,
            loaded.Row.DepartmentName, offer.JoiningDate, signedAt, WindowOpen(loaded),
            documents, required.Count, required.Count(d => d.Status == nameof(PreboardingDocumentStatus.Approved)),
            request.SubmittedAt, request.RemindedAt, canRemind, request.HandoffAt,
            await HandoffAsync(offer, request, ct));
    }

    private async Task<IReadOnlyList<PreboardingHandoffTeamDto>> HandoffAsync(Offer offer, PreboardingRequest request, CancellationToken ct)
    {
        var company = await _db.Companies.Where(c => c.Id == _tenant.TenantId)
            .Select(c => new { c.ItHandoffEmail, c.AdminHandoffEmail }).FirstOrDefaultAsync(ct);
        var messages = request.HandoffAt is null
            ? new List<OutboundMessage>()
            : await _db.OutboundMessages.AsNoTracking()
                .Where(m => m.Kind == OutboundMessageKind.PreboardingHandoff && m.SubjectId == offer.Id)
                .ToListAsync(ct);

        PreboardingHandoffTeamDto Team(string team, string? address)
        {
            if (request.HandoffAt is null)
                return new(team, address, address is null ? "NotSetUp" : "Held", null, null);

            var message = messages.FirstOrDefault(m => HandoffPayload.TeamOf(m.PayloadJson) == team);
            if (message is null)
                return new(team, null, "NotSent", null, $"No {team} address was set when the documents were approved.");
            return message.Status switch
            {
                OutboundMessageStatus.Sent => new(team, message.Recipient, "Sent", message.SentAt, null),
                OutboundMessageStatus.Pending => new(team, message.Recipient, "Queued", null, message.LastError),
                _ => new(team, message.Recipient, "Failed", null, message.LastError),
            };
        }

        return new[] { Team(HandoffPayload.It, company?.ItHandoffEmail), Team(HandoffPayload.Admin, company?.AdminHandoffEmail) };
    }

    // ── Opening and revealing (audited first) ───────────────────────────────

    public async Task<PreboardingFileContent?> OpenFileAsync(Guid offerId, Guid fileId, CancellationToken ct = default)
    {
        if (await LoadAsync(offerId, ct) is not { } loaded) return null;

        var target = await (
            from f in _db.PreboardingFiles
            join d in _db.PreboardingDocuments on f.PreboardingDocumentId equals d.Id
            where f.Id == fileId && f.RemovedAt == null && d.OfferId == offerId
            select new { File = f, d.Kind }
        ).AsNoTracking().FirstOrDefaultAsync(ct);
        if (target is null) return null;

        var stored = await _storage.DownloadAsync(target.File.StorageKey, null, ct);
        if (stored is null)
        {
            _logger.LogError("Pre-boarding file {FileId} has a row but no object ({Key}).", fileId, target.File.StorageKey);
            return null;
        }

        try
        {
            await _audit.RecordAsync(
                AuditAction.PreboardingDocumentOpened, AuditTarget.Candidate(loaded.Row.CandidateId),
                $"Opened {Preboarding.Label(target.Kind)} for offer {loaded.Reference}",
                new { offer = loaded.Reference, offerId, document = target.Kind.ToString(), fileId }, ct: ct);
            await _db.SaveChangesAsync(ct);
        }
        catch
        {
            await stored.DisposeAsync();
            throw;
        }

        // The type is the one detected at upload, never what the browser said; the name is ours.
        var name = $"{loaded.Reference}-{target.Kind}{Preboarding.Extension(target.File.ContentType)}";
        return new PreboardingFileContent(stored.Content, target.File.ContentType, name);
    }

    public async Task<RevealedBankNumberDto?> RevealBankNumberAsync(Guid offerId, CancellationToken ct = default)
    {
        if (await LoadAsync(offerId, ct) is not { } loaded) return null;
        var number = await _db.PreboardingDocuments
            .Where(d => d.OfferId == offerId && d.Kind == PreboardingDocumentKind.BankAccount)
            .Select(d => d.BankAccountNumber).FirstOrDefaultAsync(ct);
        if (number is null) return null;

        await _audit.RecordAsync(
            AuditAction.PreboardingBankNumberRevealed, AuditTarget.Candidate(loaded.Row.CandidateId),
            $"Revealed the bank account number for offer {loaded.Reference}",
            new { offer = loaded.Reference, offerId }, ct: ct);
        await _db.SaveChangesAsync(ct);
        return new RevealedBankNumberDto(number);
    }

    // ── Deciding ────────────────────────────────────────────────────────────

    /// <summary>The document, tracked, checked against the revision the recruiter was shown.</summary>
    private async Task<PreboardingDocument> DecidableAsync(Guid offerId, string kind, int revision, CancellationToken ct)
    {
        var parsed = Preboarding.ParseKind(kind) ?? throw new InvalidOperationException("There is no such document.");
        var document = await _db.PreboardingDocuments.FirstOrDefaultAsync(d => d.OfferId == offerId && d.Kind == parsed, ct);
        if (document is null || document.Status == PreboardingDocumentStatus.Missing)
            throw new InvalidOperationException("The candidate has not sent this one yet.");
        if (document.Revision != revision)
            throw new InvalidOperationException("The candidate changed this document after the page was loaded. Reload to see what they sent.");
        if (document.Status == PreboardingDocumentStatus.Approved)
            throw new InvalidOperationException("This document is already approved.");
        return document;
    }

    private async Task SaveDecisionAsync(CancellationToken ct)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new InvalidOperationException(ChangedMeanwhile);
        }
    }

    public async Task<PreboardingDto?> ApproveAsync(Guid offerId, string kind, int revision, CancellationToken ct = default)
    {
        if (await LoadAsync(offerId, ct) is not { } loaded) return null;
        var document = await DecidableAsync(offerId, kind, revision, ct);

        var files = await LiveFilesAsync(new[] { document.Id }, ct);
        if (!IsComplete(document, files.Count))
            throw new InvalidOperationException(document.Kind == PreboardingDocumentKind.BankAccount
                ? "The bank details need both the typed details and the bank book photo before they can be approved."
                : "There is no file to approve.");

        document.Status = PreboardingDocumentStatus.Approved;
        document.ReviewedAt = _clock.GetUtcNow();
        document.ReviewedByUserId = _user.UserId;
        document.Revision++;
        await SaveDecisionAsync(ct);

        await HandOffIfCompleteAsync(offerId, ct);
        return await GetAsync(offerId, ct);
    }

    /// <summary>Queues the IT and Admin emails if every required document is now approved and nobody has
    /// yet. Runs after the approval has committed, re-reading everything: of two approvals of the last two
    /// documents at once, the later to commit sees both. A lost claim is retried — the other change may have
    /// been a reminder, not the handoff.</summary>
    private async Task HandOffIfCompleteAsync(Guid offerId, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            _db.ChangeTracker.Clear();
            var request = await _db.PreboardingRequests.FirstAsync(r => r.OfferId == offerId, ct);
            if (request.HandoffAt is not null) return;

            var approved = await _db.PreboardingDocuments
                .Where(d => d.OfferId == offerId && d.Status == PreboardingDocumentStatus.Approved)
                .Select(d => d.Kind).ToListAsync(ct);
            if (Preboarding.Kinds.Any(k => Preboarding.IsRequired(request.Required, k) && !approved.Contains(k))) return;

            var company = await _db.Companies.Where(c => c.Id == _tenant.TenantId)
                .Select(c => new { c.ItHandoffEmail, c.AdminHandoffEmail }).FirstAsync(ct);
            var now = _clock.GetUtcNow();
            request.HandoffAt = now;
            foreach (var (team, address) in new[] { (HandoffPayload.It, company.ItHandoffEmail), (HandoffPayload.Admin, company.AdminHandoffEmail) })
            {
                // A team with no address is not sent anything; the screen said so beforehand.
                if (string.IsNullOrWhiteSpace(address)) continue;
                _db.OutboundMessages.Add(new OutboundMessage
                {
                    TenantId = _tenant.TenantId,
                    Kind = OutboundMessageKind.PreboardingHandoff,
                    Recipient = address,
                    SubjectType = nameof(Offer),
                    SubjectId = offerId,
                    PayloadJson = HandoffPayload.For(team),
                    Status = OutboundMessageStatus.Pending,
                    NextAttemptAt = now,
                    CreatedAt = now,
                });
            }

            try
            {
                await _db.SaveChangesAsync(ct);
                return;
            }
            catch (DbUpdateConcurrencyException)
            {
                // Someone else touched the request: re-read and decide again.
            }
        }
        _logger.LogWarning("The pre-boarding handoff for offer {OfferId} lost its claim three times; the next approval or read will not retry it.", offerId);
    }

    public async Task<PreboardingDto?> ReturnAsync(Guid offerId, string kind, int revision, string message, CancellationToken ct = default)
    {
        if (await LoadAsync(offerId, ct) is not { } loaded) return null;
        var text = Preboarding.CleanMessage(message);
        if (text is null) throw new InvalidOperationException("Write what the candidate should do — it is emailed to them.");
        if (text.Length > Preboarding.MaxReturnMessageLength)
            throw new InvalidOperationException($"Keep the message under {Preboarding.MaxReturnMessageLength:N0} characters.");
        if (!WindowOpen(loaded))
            throw new InvalidOperationException("The joining date has passed, so the candidate can no longer send documents through their link.");

        var document = await DecidableAsync(offerId, kind, revision, ct);
        var now = _clock.GetUtcNow();
        document.Status = PreboardingDocumentStatus.Returned;
        document.ReturnMessage = text;
        document.ReviewedAt = now;
        document.ReviewedByUserId = _user.UserId;
        document.Revision++;
        Enqueue(loaded.Offer, OutboundMessageKind.PreboardingReturned, JsonSerializer.Serialize(new PreboardingReturnedPayload(document.Kind.ToString())), now);
        await SaveDecisionAsync(ct);
        return await GetAsync(offerId, ct);
    }

    public async Task<PreboardingDto?> RemindAsync(Guid offerId, CancellationToken ct = default)
    {
        if (await LoadAsync(offerId, ct) is not { } loaded) return null;
        var current = await MapAsync(loaded, ct);
        if (!current.CanRemind)
        {
            var now0 = _clock.GetUtcNow();
            throw new InvalidOperationException(
                !current.WindowOpen ? "The joining date has passed, so there is nothing to remind the candidate of."
                : string.IsNullOrWhiteSpace(loaded.Offer.SentToEmail) ? "There is no email address for this offer."
                : loaded.Request.RemindedAt is { } at && now0 - at < Preboarding.ReminderInterval
                    ? "The candidate was reminded in the last 24 hours. Try again tomorrow."
                    : "Every required document has been sent. There is nothing to remind them of.");
        }

        var now = _clock.GetUtcNow();
        loaded.Request.RemindedAt = now;
        loaded.Request.ReminderCount++;
        Enqueue(loaded.Offer, OutboundMessageKind.PreboardingReminder, null, now);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new InvalidOperationException("The candidate was reminded a moment ago.");
        }
        return await GetAsync(offerId, ct);
    }

    /// <summary>To the address the offer was sent to, and nowhere else (§5a).</summary>
    private void Enqueue(Offer offer, OutboundMessageKind kind, string? payload, DateTimeOffset now) =>
        _db.OutboundMessages.Add(new OutboundMessage
        {
            TenantId = offer.TenantId,
            Kind = kind,
            Recipient = offer.SentToEmail ?? string.Empty,
            SubjectType = nameof(Offer),
            SubjectId = offer.Id,
            PayloadJson = payload ?? "{}",
            Status = OutboundMessageStatus.Pending,
            NextAttemptAt = now,
            CreatedAt = now,
        });

    // ── Settings ────────────────────────────────────────────────────────────

    private static PreboardingSettingsDto Settings(Company company) => new(
        (company.PreboardingRequired & PreboardingItems.Nrc) != 0,
        (company.PreboardingRequired & PreboardingItems.BankAccount) != 0,
        (company.PreboardingRequired & PreboardingItems.Education) != 0,
        (company.PreboardingRequired & PreboardingItems.Photo) != 0,
        company.ItHandoffEmail,
        company.AdminHandoffEmail);

    public async Task<PreboardingSettingsDto> SettingsAsync(CancellationToken ct = default)
    {
        var company = await _db.Companies.AsNoTracking().FirstAsync(c => c.Id == _tenant.TenantId, ct);
        return Settings(company);
    }

    public async Task<PreboardingSettingsDto> UpdateSettingsAsync(UpdatePreboardingSettingsRequest request, CancellationToken ct = default)
    {
        var it = Address(request.ItHandoffEmail, "IT");
        var admin = Address(request.AdminHandoffEmail, "Admin");

        var company = await _db.Companies.FirstAsync(c => c.Id == _tenant.TenantId, ct);
        var before = Settings(company);

        var required = PreboardingItems.None;
        if (request.NrcRequired!.Value) required |= PreboardingItems.Nrc; // [Required] on the request
        if (request.BankAccountRequired!.Value) required |= PreboardingItems.BankAccount;
        if (request.EducationRequired!.Value) required |= PreboardingItems.Education;
        if (request.PhotoRequired!.Value) required |= PreboardingItems.Photo;
        company.PreboardingRequired = required;
        company.ItHandoffEmail = it;
        company.AdminHandoffEmail = admin;
        var after = Settings(company);

        if (after != before)
        {
            await _audit.RecordAsync(
                AuditAction.PreboardingSettingsChanged, null, "Changed the pre-boarding settings",
                new { before, after }, ct: ct);
        }
        await _db.SaveChangesAsync(ct);
        return after;
    }

    /// <summary>One address or none. Refused rather than guessed: a handoff sent to a typo is a new
    /// employee's photo sent to a stranger.</summary>
    private static string? Address(string? typed, string team)
    {
        var value = typed?.Trim();
        if (string.IsNullOrEmpty(value)) return null;
        if (value.Contains(',') || value.Contains(';') || value.Any(char.IsWhiteSpace))
            throw new InvalidOperationException($"The {team} address must be one email address.");
        try
        {
            var parsed = new MailAddress(value);
            if (!string.Equals(parsed.Address, value, StringComparison.Ordinal) || !value.Contains('@'))
                throw new FormatException();
        }
        catch (FormatException)
        {
            throw new InvalidOperationException($"The {team} address is not a valid email address.");
        }
        return value;
    }
}

/// <summary>Which document a <see cref="OutboundMessageKind.PreboardingReturned"/> row is about — its payload.</summary>
public sealed record PreboardingReturnedPayload(string Kind);

/// <summary>Which team a <see cref="OutboundMessageKind.PreboardingHandoff"/> row is for — its payload.</summary>
public static class HandoffPayload
{
    public const string It = "IT";
    public const string Admin = "Admin";

    private sealed record Payload(string Team);

    public static string For(string team) => JsonSerializer.Serialize(new Payload(team));

    public static string? TeamOf(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<Payload>(json)?.Team switch
            {
                It => It,
                Admin => Admin,
                _ => null,
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
