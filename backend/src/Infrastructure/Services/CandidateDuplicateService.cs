using Microsoft.EntityFrameworkCore;
using Npgsql;
using RecruitOps.Application.Common;
using RecruitOps.Application.DTOs;
using RecruitOps.Application.Interfaces;
using RecruitOps.Domain.Entities;
using RecruitOps.Domain.Enums;
using RecruitOps.Infrastructure.Persistence;

namespace RecruitOps.Infrastructure.Services;

/// <summary>Module 2.7 — flag two records that may be one person; let a person merge them or say they
/// are not. Recruitment staff only (the controller's policy); those roles are company-wide, so there is
/// no department predicate to apply here — the tenant filter is the only scope.</summary>
public class CandidateDuplicateService : ICandidateDuplicateService
{
    /// <summary>One shared office phone among fifty candidates is 1,225 pairs; nobody reviews that many
    /// at once, and the next page appears as these are decided.</summary>
    private const int MaxPairs = 200;

    /// <summary>A phone or email on more than this many records is a shared line or a placeholder —
    /// an office number, "0", a family's one phone — not one person filed ten times. Such a value is
    /// not expanded pair by pair: fifty records would be 1,225 pairs, and an anonymous applicant can
    /// grow the group (security review, M3).</summary>
    private const int MaxGroupSize = 10;

    private const string ChangedMeanwhile =
        "One of these records was changed by someone else a moment ago. Reload and review again.";

    private readonly AppDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IAuditTrail _audit;
    private readonly TimeProvider _clock;

    public CandidateDuplicateService(AppDbContext db, ICurrentUser user, IAuditTrail audit, TimeProvider clock)
    {
        _db = db;
        _user = user;
        _audit = audit;
        _clock = clock;
    }

    public async Task<IReadOnlyList<DuplicatePairDto>> ListPairsAsync(CancellationToken ct = default)
    {
        var active = _db.Candidates.AsNoTracking().Where(c => c.MergedIntoCandidateId == null);

        // Grouped in SQL, so only the shared values come back — not every candidate in the company.
        var sharedEmails = await active.Where(c => c.Email != null)
            .GroupBy(c => c.Email!).Where(g => g.Count() > 1 && g.Count() <= MaxGroupSize)
            .Select(g => g.Key).ToListAsync(ct);
        var sharedPhones = await active.Where(c => c.Phone != null)
            .GroupBy(c => c.Phone!).Where(g => g.Count() > 1 && g.Count() <= MaxGroupSize)
            .Select(g => g.Key).ToListAsync(ct);
        if (sharedEmails.Count == 0 && sharedPhones.Count == 0) return Array.Empty<DuplicatePairDto>();

        var involved = await active
            .Where(c => (c.Email != null && sharedEmails.Contains(c.Email))
                        || (c.Phone != null && sharedPhones.Contains(c.Phone)))
            .ToListAsync(ct);

        var pairs = new Dictionary<(Guid, Guid), (bool Phone, bool Email)>();
        void AddPairs(IEnumerable<Candidate> group, bool byPhone)
        {
            var list = group.ToList();
            for (var i = 0; i < list.Count; i++)
                for (var j = i + 1; j < list.Count; j++)
                {
                    var key = Ordered(list[i].Id, list[j].Id);
                    var seen = pairs.GetValueOrDefault(key);
                    pairs[key] = byPhone ? seen with { Phone = true } : seen with { Email = true };
                }
        }
        foreach (var g in involved.Where(c => c.Phone != null && sharedPhones.Contains(c.Phone)).GroupBy(c => c.Phone))
            AddPairs(g, byPhone: true);
        foreach (var g in involved.Where(c => c.Email != null && sharedEmails.Contains(c.Email)).GroupBy(c => c.Email))
            AddPairs(g, byPhone: false);

        var ids = involved.Select(c => c.Id).ToList();
        var decided = (await _db.DistinctCandidatePairs.AsNoTracking()
                .Where(p => ids.Contains(p.FirstCandidateId))
                .Select(p => new { p.FirstCandidateId, p.SecondCandidateId })
                .ToListAsync(ct))
            .Select(p => (p.FirstCandidateId, p.SecondCandidateId))
            .ToHashSet();

        var byId = involved.ToDictionary(c => c.Id);
        var open = pairs.Where(p => !decided.Contains(p.Key))
            .Select(p => (A: byId[p.Key.Item1], B: byId[p.Key.Item2], p.Value))
            // Newest first: a pair is as new as its newer record — the one most likely just created by mistake.
            .OrderByDescending(p => p.A.CreatedAt > p.B.CreatedAt ? p.A.CreatedAt : p.B.CreatedAt)
            .Take(MaxPairs)
            .ToList();
        if (open.Count == 0) return Array.Empty<DuplicatePairDto>();

        var shown = open.SelectMany(p => new[] { p.A.Id, p.B.Id }).Distinct().ToList();
        var applications = await _db.JobApplications.AsNoTracking()
            .Where(a => shown.Contains(a.CandidateId))
            .GroupBy(a => a.CandidateId)
            .Select(g => new { CandidateId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CandidateId, x => x.Count, ct);
        var interviews = await (
                from i in _db.Interviews.AsNoTracking()
                join a in _db.JobApplications.AsNoTracking() on i.JobApplicationId equals a.Id
                where shown.Contains(a.CandidateId)
                group i by a.CandidateId into g
                select new { CandidateId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CandidateId, x => x.Count, ct);
        var scored = (await (
                from s in _db.Scorecards.AsNoTracking()
                join i in _db.Interviews.AsNoTracking() on s.InterviewId equals i.Id
                join a in _db.JobApplications.AsNoTracking() on i.JobApplicationId equals a.Id
                where shown.Contains(a.CandidateId) && s.Status == ScorecardStatus.Submitted
                select a.CandidateId).Distinct().ToListAsync(ct))
            .ToHashSet();

        DuplicateCandidateDto Map(Candidate c) => new(
            c.Id, c.FullName, c.Email, c.Phone, c.Source.ToString(), c.CreatedAt,
            applications.GetValueOrDefault(c.Id), interviews.GetValueOrDefault(c.Id), scored.Contains(c.Id));

        return open.Select(p =>
        {
            var (older, newer) = p.A.CreatedAt <= p.B.CreatedAt ? (p.A, p.B) : (p.B, p.A);
            var (matchedOn, value) = p.Value switch
            {
                { Phone: true, Email: true } => ("Phone and email", older.Phone!),
                { Phone: true } => ("Phone", older.Phone!),
                _ => ("Email", older.Email!),
            };
            return new DuplicatePairDto(Map(older), Map(newer), matchedOn, value);
        }).ToList();
    }

    public async Task<MergeCandidatesResultDto?> MergeAsync(MergeCandidatesRequest request, CancellationToken ct = default)
    {
        if (request.KeepCandidateId == request.MergeCandidateId)
            throw new InvalidOperationException("Choose two different records.");

        var both = await _db.Candidates
            .Where(c => c.Id == request.KeepCandidateId || c.Id == request.MergeCandidateId)
            .ToListAsync(ct);
        var keep = both.FirstOrDefault(c => c.Id == request.KeepCandidateId);
        var other = both.FirstOrDefault(c => c.Id == request.MergeCandidateId);
        if (keep is null || other is null) return null;

        await RequireFlaggedPairAsync(keep, other, ct);

        var now = _clock.GetUtcNow();
        var applications = await _db.JobApplications.Where(a => a.CandidateId == other.Id).ToListAsync(ct);
        foreach (var application in applications)
        {
            // Each keeps its own id, stage history, interviews, scorecards, notes and CV — they all hang
            // off the application, which is why moving the application is the whole merge.
            application.CandidateId = keep.Id;
            application.UpdatedAt = now;
        }

        // The kept record's fields win; a blank there is filled, never the reverse (the public path's rule).
        var filled = new List<string>();
        if (keep.Email is null && other.Email is not null) { keep.Email = other.Email; filled.Add("email"); }
        if (keep.Phone is null && other.Phone is not null) { keep.Phone = other.Phone; filled.Add("phone"); }

        other.MergedIntoCandidateId = keep.Id;
        // Both stamps renewed: the save then fails if anyone decided about either record since it was
        // read (Candidate.MergeStamp) — including a merge INTO `other`, which changes nothing else on it.
        Renew(keep, other);

        // Records merged into this one earlier now point at the kept one, so every old link resolves in
        // one step, never through a chain.
        foreach (var earlier in await _db.Candidates.Where(c => c.MergedIntoCandidateId == other.Id).ToListAsync(ct))
        {
            earlier.MergedIntoCandidateId = keep.Id;
            Renew(earlier);
        }

        // "Not the same person" answers about the folded record hold for the kept one too: if A is not
        // C and A is B, then B is not C. Without this, B and C would be flagged again — and a pair asked
        // about twice is how strangers get merged.
        var carried = 0;
        var keptDecisions = (await _db.DistinctCandidatePairs.AsNoTracking()
                .Where(p => p.FirstCandidateId == keep.Id || p.SecondCandidateId == keep.Id)
                .Select(p => new { p.FirstCandidateId, p.SecondCandidateId })
                .ToListAsync(ct))
            .Select(p => (p.FirstCandidateId, p.SecondCandidateId))
            .ToHashSet();
        var otherDecisions = await _db.DistinctCandidatePairs.AsNoTracking()
            .Where(p => p.FirstCandidateId == other.Id || p.SecondCandidateId == other.Id)
            .Select(p => new { p.FirstCandidateId, p.SecondCandidateId, p.DecidedByUserId })
            .ToListAsync(ct);
        foreach (var decision in otherDecisions)
        {
            var stranger = decision.FirstCandidateId == other.Id ? decision.SecondCandidateId : decision.FirstCandidateId;
            if (stranger == keep.Id) continue;
            var pair = Ordered(keep.Id, stranger);
            if (!keptDecisions.Add(pair)) continue;
            _db.DistinctCandidatePairs.Add(new DistinctCandidatePair
            {
                FirstCandidateId = pair.Item1,
                SecondCandidateId = pair.Item2,
                DecidedByUserId = decision.DecidedByUserId,
            });
            carried++;
        }

        // Not reversible, so it is answerable (ADR-0027). Record ids, never names: the log is permanent
        // and cannot be erased, a candidate's name is their data, and readers of the log are not all
        // allowed candidate data (security review, M2).
        await _audit.RecordAsync(AuditAction.CandidatesMerged, AuditTarget.Candidate(keep.Id),
            $"Merged one candidate record into another: {applications.Count} application{(applications.Count == 1 ? "" : "s")} moved.",
            new
            {
                keptCandidateId = keep.Id,
                mergedCandidateId = other.Id,
                applicationsMoved = applications.Select(a => a.Id),
                filledFromMerged = filled,
                decisionsCarried = carried,
            }, ct: ct);

        await SaveDecisionAsync(ct);
        return new MergeCandidatesResultDto(keep.Id, applications.Count);
    }

    public async Task<bool?> KeepSeparateAsync(KeepCandidatesSeparateRequest request, CancellationToken ct = default)
    {
        if (request.FirstCandidateId == request.SecondCandidateId)
            throw new InvalidOperationException("Choose two different records.");

        // Tracked: both stamps are renewed, so this answer and a merge of the same records cannot
        // both commit.
        var both = await _db.Candidates
            .Where(c => c.Id == request.FirstCandidateId || c.Id == request.SecondCandidateId)
            .ToListAsync(ct);
        if (both.Count != 2) return null;

        var (first, second) = Ordered(request.FirstCandidateId, request.SecondCandidateId);
        // Idempotent: a second "not the same person" for the same pair is the same answer.
        if (await DecidedDistinctAsync(first, second, ct)) return true;

        await RequireFlaggedPairAsync(both[0], both[1], ct);

        _db.DistinctCandidatePairs.Add(new DistinctCandidatePair
        {
            FirstCandidateId = first,
            SecondCandidateId = second,
            DecidedByUserId = _user.UserId ?? Guid.Empty,
        });
        Renew(both[0], both[1]);

        // It stops a merge of these two for good, so it is answerable too — ids only, as for a merge.
        await _audit.RecordAsync(AuditAction.CandidatesKeptSeparate, AuditTarget.Candidate(first),
            "Marked two candidate records as different people.",
            new { firstCandidateId = first, secondCandidateId = second }, ct: ct);

        try
        {
            await SaveDecisionAsync(ct);
        }
        catch (InvalidOperationException)
        {
            // A double click, or two reviewers giving the same answer: the first one's row is there.
            _db.ChangeTracker.Clear();
            if (await DecidedDistinctAsync(first, second, ct)) return true;
            throw;
        }
        return true;
    }

    private Task<bool> DecidedDistinctAsync(Guid first, Guid second, CancellationToken ct)
        => _db.DistinctCandidatePairs.AnyAsync(p => p.FirstCandidateId == first && p.SecondCandidateId == second, ct);

    private static void Renew(params Candidate[] candidates)
    {
        foreach (var c in candidates) c.MergeStamp = Guid.NewGuid();
    }

    /// <summary>Saves a decision, turning "someone decided about these records meanwhile" into the
    /// reviewer's message. That arrives three ways: a stamp that changed (the usual one), the unique
    /// pair index (the same answer twice at once), or — on PostgreSQL, when two decisions lock the
    /// same rows in opposite order — a deadlock. Nothing of a failed save is kept.</summary>
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
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.DeadlockDetected,
        })
        {
            throw new InvalidOperationException(ChangedMeanwhile);
        }
    }

    /// <summary>Only two records the list could pair can be decided: the API checks, not just the screen,
    /// so a hand-made request cannot merge two strangers who share nothing. (A value shared by more than
    /// <see cref="MaxGroupSize"/> records is not listed, but two of them may still be decided.)</summary>
    private async Task RequireFlaggedPairAsync(Candidate a, Candidate b, CancellationToken ct)
    {
        if (a.MergedIntoCandidateId is not null || b.MergedIntoCandidateId is not null)
            throw new InvalidOperationException("One of these records has already been merged.");

        var shared = (a.Phone is not null && a.Phone == b.Phone) || (a.Email is not null && a.Email == b.Email);
        if (!shared)
            throw new InvalidOperationException(
                "These records share no phone number or email, so they are not flagged as one person.");

        var (first, second) = Ordered(a.Id, b.Id);
        if (await DecidedDistinctAsync(first, second, ct))
            throw new InvalidOperationException("These records were marked as different people.");
    }

    private static (Guid, Guid) Ordered(Guid a, Guid b) => a.CompareTo(b) <= 0 ? (a, b) : (b, a);
}
