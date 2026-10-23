using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using RecruitOps.Application.Common;
using RecruitOps.Application.DTOs;
using RecruitOps.Domain;
using RecruitOps.Domain.Entities;
using RecruitOps.Domain.Enums;

namespace RecruitOps.Infrastructure.Services;

/// <summary>Joining documents on the candidate's link, after accepting (Module 4.3, ADR-0030 §5b), drawn
/// in <c>design/public/offer-portal.html</c> <c>#documents</c> and <c>#documents-sent</c>.
///
/// <para>Same three keys as the offer: the token finds it, the session reads and writes. Anonymous like
/// the rest of the link — every read re-applies the tenant the token resolved to, every row written sets
/// it.</para>
///
/// <para><b>Every change to a document is compare-and-set on its <see cref="PreboardingDocument.Revision"/>.</b>
/// Two uploads to one document at once cannot both commit; a recruiter approving names the revision they
/// saw, so an upload that lands first makes the approval fail rather than approve a file nobody opened.</para>
///
/// <para><b>Files.</b> The type is decided by the first bytes — never the name or the declared type — and
/// is what the object is stored and later served as. The object is written before the row that points at
/// it: a request cut off between the two leaves an object nothing names, never a row naming nothing. A
/// replaced or removed file's object is deleted after the change commits; its row is kept, marked
/// removed.</para></summary>
public partial class OfferPortalService
{
    private const string DocumentChangedMeanwhile = "That document was changed a moment ago. Reload the page and try again.";

    /// <summary>An accepted offer with a live session, and its pre-boarding. <paramref name="change"/>: the
    /// caller is about to change something, so the window must still be open.</summary>
    private async Task<(Offer Offer, Context Context, PreboardingRequest Request)?> DocumentsOfAsync(
        string token, string? session, bool change, CancellationToken ct)
    {
        if (await LoadAsync(token, ct) is not var (offer, context)) return null;
        await RequireSessionAsync(offer, session, ct);
        if (offer.Status != OfferStatus.Accepted)
            throw new PortalRefusalException("Documents are asked for once the offer is accepted.");

        var request = await _db.PreboardingRequests.IgnoreQueryFilters()
                          .FirstOrDefaultAsync(r => r.TenantId == offer.TenantId && r.OfferId == offer.Id, ct)
                      ?? await CreateRequestAsync(offer, context, ct);

        var openUntil = DocumentsOpenUntil(offer, context);
        if (change && !Preboarding.IsWindowOpen(openUntil, _clock.GetUtcNow(), context.TimeZoneId))
            throw new PortalRefusalException(
                $"Documents could be sent here until {Long(openUntil)}. Please contact the recruitment team.");

        return (offer, context, request);
    }

    /// <summary>For an offer accepted while an older release ran: its request is created on first use from
    /// the company's current choice, as the migration's backfill would have (database review, M2). Of two
    /// first uses at once, the unique index keeps one and the other reads it.</summary>
    private async Task<PreboardingRequest> CreateRequestAsync(Offer offer, Context context, CancellationToken ct)
    {
        var request = new PreboardingRequest
        {
            TenantId = offer.TenantId, OfferId = offer.Id, Required = context.PreboardingRequired, CreatedAt = _clock.GetUtcNow(),
        };
        _db.PreboardingRequests.Add(request);
        try
        {
            await _db.SaveChangesAsync(ct);
            return request;
        }
        catch (DbUpdateException)
        {
            _db.Entry(request).State = EntityState.Detached;
            return await _db.PreboardingRequests.IgnoreQueryFilters()
                .FirstAsync(r => r.TenantId == offer.TenantId && r.OfferId == offer.Id, ct);
        }
    }

    private async Task<List<PreboardingDocument>> DocumentRowsAsync(Offer offer, CancellationToken ct) =>
        await _db.PreboardingDocuments.IgnoreQueryFilters()
            .Where(d => d.TenantId == offer.TenantId && d.OfferId == offer.Id)
            .ToListAsync(ct);

    private async Task<List<PreboardingFile>> LiveFilesAsync(Offer offer, IEnumerable<Guid> documentIds, CancellationToken ct)
    {
        var ids = documentIds.ToList();
        return await _db.PreboardingFiles.IgnoreQueryFilters()
            .Where(f => f.TenantId == offer.TenantId && ids.Contains(f.PreboardingDocumentId) && f.RemovedAt == null)
            .OrderBy(f => f.UploadedAt).ThenBy(f => f.Id)
            .ToListAsync(ct);
    }

    /// <summary>The document of this kind, tracked — created (unsaved) on the candidate's first change.
    /// Refused once it is approved: that is final.</summary>
    private async Task<PreboardingDocument> ChangeableDocumentAsync(Offer offer, PreboardingDocumentKind kind, CancellationToken ct)
    {
        var document = await _db.PreboardingDocuments.IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.TenantId == offer.TenantId && d.OfferId == offer.Id && d.Kind == kind, ct);
        if (document is null)
        {
            document = new PreboardingDocument { TenantId = offer.TenantId, OfferId = offer.Id, Kind = kind };
            _db.PreboardingDocuments.Add(document);
        }
        else if (document.Status == PreboardingDocumentStatus.Approved)
        {
            throw new PortalRefusalException("This one has been checked and approved, so it can no longer be changed here.");
        }
        return document;
    }

    /// <summary>A change by the candidate: a new revision, waiting for the recruiter again.</summary>
    private static void Changed(PreboardingDocument document, DateTimeOffset now, bool empty)
    {
        document.Revision++;
        document.ChangedAt = now;
        document.Status = empty ? PreboardingDocumentStatus.Missing : PreboardingDocumentStatus.Submitted;
    }

    /// <summary>Saves a document change; a lost compare-and-set (or a twin first upload) is the candidate's
    /// to retry.</summary>
    private async Task SaveDocumentChangeAsync(CancellationToken ct)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another change to this document won.
            throw new PortalRefusalException(DocumentChangedMeanwhile);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Two first uploads to one document at once: the other one created it.
            throw new PortalRefusalException(DocumentChangedMeanwhile);
        }
    }

    /// <summary>Deletes objects whose rows are already marked removed. Best effort: an object left behind
    /// is named by nothing and reached by nobody.</summary>
    private async Task DeleteObjectsAsync(IEnumerable<string> keys)
    {
        foreach (var key in keys)
        {
            if (!await _storage.DeleteAsync(key, null, CancellationToken.None))
                _logger.LogWarning("A removed pre-boarding file's object {Key} could not be deleted.", key);
        }
    }

    public async Task<bool> HasSessionAsync(string token, string? session, CancellationToken ct = default)
    {
        if (!OfferPortal.LooksLikeToken(token) || !OfferPortal.LooksLikeToken(session)) return false;
        var offer = await _db.Offers.IgnoreQueryFilters().AsNoTracking()
            .Where(o => o.PortalToken == token).Select(o => new { o.Id, o.TenantId }).FirstOrDefaultAsync(ct);
        if (offer is null) return false;
        var hash = OfferPortal.Sha256Hex(session!); // LooksLikeToken is false for null
        var now = _clock.GetUtcNow();
        return await _db.OfferPortalAccesses.IgnoreQueryFilters().AnyAsync(a =>
            a.TenantId == offer.TenantId && a.OfferId == offer.Id && a.SessionHash == hash && a.SessionExpiresAt > now, ct);
    }

    // ── Reading ─────────────────────────────────────────────────────────────

    public async Task<PortalDocumentsDto?> GetDocumentsAsync(string token, string? session, CancellationToken ct = default)
    {
        if (await DocumentsOfAsync(token, session, change: false, ct) is not var (offer, context, request)) return null;
        return await MapDocumentsAsync(offer, context, request, ct);
    }

    /// <summary>What the page shows, and what "Send for checking" is allowed to do — one computation for
    /// both, so the button and the server cannot disagree.</summary>
    private sealed record DocumentsView(PortalDocumentsDto Dto, IReadOnlyList<string> MissingRequired);

    private async Task<PortalDocumentsDto> MapDocumentsAsync(Offer offer, Context context, PreboardingRequest request, CancellationToken ct) =>
        (await ViewAsync(offer, context, request, ct)).Dto;

    private async Task<DocumentsView> ViewAsync(Offer offer, Context context, PreboardingRequest request, CancellationToken ct)
    {
        var rows = await DocumentRowsAsync(offer, ct);
        var files = await LiveFilesAsync(offer, rows.Select(r => r.Id), ct);
        var signedAt = await _db.OfferSignatures.IgnoreQueryFilters().AsNoTracking()
            .Where(s => s.TenantId == offer.TenantId && s.OfferId == offer.Id)
            .Select(s => (DateTimeOffset?)s.SignedAt).FirstOrDefaultAsync(ct);

        var documents = Preboarding.Kinds.Select(kind =>
        {
            var row = rows.FirstOrDefault(r => r.Kind == kind);
            var own = row is null ? new List<PreboardingFile>() : files.Where(f => f.PreboardingDocumentId == row.Id).ToList();
            var status = row?.Status ?? PreboardingDocumentStatus.Missing;
            return new PortalDocumentDto(
                kind.ToString(),
                Preboarding.IsRequired(request.Required, kind),
                status.ToString(),
                IsComplete(kind, row, own.Count),
                status == PreboardingDocumentStatus.Returned ? row?.ReturnMessage : null,
                own.Select(f => new PortalFileDto(f.Id, f.FileName, f.SizeBytes)).ToList(),
                kind == PreboardingDocumentKind.BankAccount
                    ? new PortalBankDto(row?.BankName, row?.BankAccountName,
                        row?.BankAccountNumber is { } n ? Preboarding.MaskAccountNumber(n) : null)
                    : null);
        }).ToList();

        // An item is ready when every document in it is complete and none was sent back unchanged.
        var items = new[] { PreboardingItems.Nrc, PreboardingItems.BankAccount, PreboardingItems.Education, PreboardingItems.Photo };
        bool Ready(PreboardingItems item) => documents
            .Where(d => Preboarding.ItemOf(Enum.Parse<PreboardingDocumentKind>(d.Kind)) == item)
            .All(d => d.Complete && d.Status != nameof(PreboardingDocumentStatus.Returned));
        var required = items.Where(i => (request.Required & i) != 0).ToList();
        var missing = required.Where(i => !Ready(i)).Select(ItemName).ToList();

        var changedSince = rows.Any(r => r.ChangedAt > request.SubmittedAt) || request.SubmittedAt is null;
        var anyReturned = rows.Any(r => r.Status == PreboardingDocumentStatus.Returned);
        var openUntil = DocumentsOpenUntil(offer, context);
        var open = Preboarding.IsWindowOpen(openUntil, _clock.GetUtcNow(), context.TimeZoneId);

        var dto = new PortalDocumentsDto(
            openUntil,
            signedAt,
            string.IsNullOrWhiteSpace(offer.SentToEmail) ? null : OfferPortal.MaskEmail(offer.SentToEmail),
            documents,
            required.Count,
            required.Count - missing.Count,
            Sent: request.SubmittedAt is not null && missing.Count == 0 && !changedSince && !anyReturned,
            CanSend: open && missing.Count == 0 && changedSince);
        return new DocumentsView(dto, missing);
    }

    private static bool IsComplete(PreboardingDocumentKind kind, PreboardingDocument? row, int liveFiles) =>
        liveFiles > 0
        && (kind != PreboardingDocumentKind.BankAccount
            || (row?.BankName is not null && row.BankAccountName is not null && row.BankAccountNumber is not null));

    private static string ItemName(PreboardingItems item) => item switch
    {
        PreboardingItems.Nrc => "both sides of your NRC",
        PreboardingItems.BankAccount => "your bank account details",
        PreboardingItems.Education => "your education certificates",
        _ => "a photo for your staff card",
    };

    // ── Changing ────────────────────────────────────────────────────────────

    public async Task<PortalDocumentsDto?> UploadAsync(
        string token, string? session, string kind, string? fileName, long length, Stream content, CancellationToken ct = default)
    {
        if (await DocumentsOfAsync(token, session, change: true, ct) is not var (offer, context, request)) return null;
        var parsed = Preboarding.ParseKind(kind) ?? throw new PortalRefusalException("That isn't one of the documents asked for.");

        if (length <= 0) throw new PortalRefusalException("That file is empty. Choose another.");
        if (length > Preboarding.MaxFileBytes) throw new PortalRefusalException("That file is over 10 MB. Choose a smaller one, or take a new photo.");

        // Read whole, at most one byte past the limit: the declared length is the uploader's to choose.
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await content.ReadAsync(chunk, ct)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > Preboarding.MaxFileBytes)
                throw new PortalRefusalException("That file is over 10 MB. Choose a smaller one, or take a new photo.");
        }
        if (buffer.Length == 0) throw new PortalRefusalException("That file is empty. Choose another.");

        var type = Preboarding.Sniff(buffer.GetBuffer().AsSpan(0, (int)Math.Min(buffer.Length, 16)), parsed)
                   ?? throw new PortalRefusalException(parsed == PreboardingDocumentKind.Photo
                       ? "Upload a photo — a JPG or PNG."
                       : "Upload a photo (JPG or PNG) or a PDF.");

        var now = _clock.GetUtcNow();
        var document = await ChangeableDocumentAsync(offer, parsed, ct);
        var live = _db.Entry(document).State == EntityState.Added
            ? new List<PreboardingFile>()
            : await LiveFilesAsync(offer, new[] { document.Id }, ct);

        // A one-file document is replaced; education collects up to five.
        var replaced = new List<PreboardingFile>();
        if (Preboarding.MaxFiles(parsed) == 1) replaced.AddRange(live);
        else if (live.Count >= Preboarding.MaxFiles(parsed))
            throw new PortalRefusalException($"You can add up to {Preboarding.MaxFiles(parsed)} files here. Remove one first.");

        var file = new PreboardingFile
        {
            TenantId = offer.TenantId,
            PreboardingDocumentId = document.Id,
            FileName = Preboarding.CleanFileName(fileName),
            ContentType = type,
            SizeBytes = buffer.Length,
            Sha256 = Convert.ToHexStringLower(SHA256.HashData(buffer.GetBuffer().AsSpan(0, (int)buffer.Length))),
            UploadedAt = now,
            CreatedAt = now,
        };
        // No name the candidate chose is in the key.
        file.StorageKey = $"preboarding/{offer.Id}/{file.Id}";

        buffer.Position = 0;
        await _storage.UploadAsync(new UploadFileRequest(file.StorageKey, buffer, type, buffer.Length), ct);

        foreach (var old in replaced) old.RemovedAt = now;
        _db.PreboardingFiles.Add(file);
        Changed(document, now, empty: false);
        try
        {
            await SaveDocumentChangeAsync(ct);
        }
        catch
        {
            // Whatever stopped the row — a lost race, a database error, a cancelled request — the object it
            // would have named goes too: an object nothing names is an identity scan nobody can find to
            // delete (security review, 2026-10-23, L2).
            await DeleteObjectsAsync(new[] { file.StorageKey });
            throw;
        }
        await DeleteObjectsAsync(replaced.Select(f => f.StorageKey));

        return await MapDocumentsAsync(offer, context, request, ct);
    }

    public async Task<PortalDocumentsDto?> RemoveFileAsync(string token, string? session, Guid fileId, CancellationToken ct = default)
    {
        if (await DocumentsOfAsync(token, session, change: true, ct) is not var (offer, context, request)) return null;

        var target = await (
            from f in _db.PreboardingFiles.IgnoreQueryFilters()
            join d in _db.PreboardingDocuments.IgnoreQueryFilters() on f.PreboardingDocumentId equals d.Id
            where f.Id == fileId && f.RemovedAt == null
                  && f.TenantId == offer.TenantId && d.TenantId == offer.TenantId && d.OfferId == offer.Id
            select new { File = f, d.Kind }
        ).FirstOrDefaultAsync(ct)
            ?? throw new PortalRefusalException("That file has already been removed. Reload the page.");

        var document = await ChangeableDocumentAsync(offer, target.Kind, ct);
        var now = _clock.GetUtcNow();
        target.File.RemovedAt = now;
        var left = await _db.PreboardingFiles.IgnoreQueryFilters()
            .CountAsync(f => f.TenantId == offer.TenantId && f.PreboardingDocumentId == document.Id && f.RemovedAt == null && f.Id != fileId, ct);
        Changed(document, now, empty: left == 0 && document.BankAccountNumber is null);
        await SaveDocumentChangeAsync(ct);
        await DeleteObjectsAsync(new[] { target.File.StorageKey });

        return await MapDocumentsAsync(offer, context, request, ct);
    }

    public async Task<PortalDocumentsDto?> SaveBankDetailsAsync(
        string token, string? session, SaveBankDetailsRequest request, CancellationToken ct = default)
    {
        if (await DocumentsOfAsync(token, session, change: true, ct) is not var (offer, context, preboarding)) return null;

        var bank = Preboarding.CleanText(request.BankName, Preboarding.MaxBankNameLength)
                   ?? throw new PortalRefusalException($"Type the bank's name, in up to {Preboarding.MaxBankNameLength} characters.");
        var name = Preboarding.CleanText(request.AccountName, Preboarding.MaxAccountNameLength)
                   ?? throw new PortalRefusalException($"Type the name on the account, in up to {Preboarding.MaxAccountNameLength} characters.");
        var number = Preboarding.NormalizeAccountNumber(request.AccountNumber)
                     ?? throw new PortalRefusalException("Type the account number — digits only, at least 6.");

        var document = await ChangeableDocumentAsync(offer, PreboardingDocumentKind.BankAccount, ct);
        document.BankName = bank;
        document.BankAccountName = name;
        document.BankAccountNumber = number;
        Changed(document, _clock.GetUtcNow(), empty: false);
        await SaveDocumentChangeAsync(ct);

        return await MapDocumentsAsync(offer, context, preboarding, ct);
    }

    public async Task<PortalDocumentsDto?> SendForCheckingAsync(string token, string? session, CancellationToken ct = default)
    {
        if (await DocumentsOfAsync(token, session, change: true, ct) is not var (offer, context, request)) return null;

        var view = await ViewAsync(offer, context, request, ct);
        if (view.MissingRequired.Count > 0)
            throw new PortalRefusalException($"Add {string.Join(" and ", view.MissingRequired)} first.");
        if (!view.Dto.CanSend)
            throw new PortalRefusalException("Nothing has changed since you last sent them. The recruitment team has them.");

        // Once per change, and the recruiter is emailed at most once an hour however often the button is
        // pressed (security review, L4) — the screen shows the rest. The request's version makes two
        // presses at once one.
        var now = _clock.GetUtcNow();
        request.SubmittedAt = now;
        request.Version++;
        if (request.NotifiedAt is null || now - request.NotifiedAt >= Preboarding.NotifyInterval)
        {
            request.NotifiedAt = now;
            _db.OutboundMessages.Add(new OutboundMessage
            {
                TenantId = offer.TenantId,
                Kind = OutboundMessageKind.PreboardingSubmitted,
                Recipient = await RecruiterEmailAsync(offer, ct) ?? string.Empty,
                SubjectType = nameof(Offer),
                SubjectId = offer.Id,
                Status = OutboundMessageStatus.Pending,
                NextAttemptAt = now,
                CreatedAt = now,
            });
        }
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new PortalRefusalException("Something changed a moment ago. Reload the page and try again.");
        }

        return await MapDocumentsAsync(offer, context, request, ct);
    }
}
