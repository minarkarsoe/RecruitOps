using Microsoft.EntityFrameworkCore;
using RecruitOps.Application.Common;
using RecruitOps.Application.DTOs;
using RecruitOps.Application.Interfaces;
using RecruitOps.Domain;
using RecruitOps.Domain.Entities;
using RecruitOps.Infrastructure.Persistence;

namespace RecruitOps.Infrastructure.Services;

/// <summary>Module 3.4 — collaborative notes with @mentions.</summary>
public class NoteService : INoteService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IApplicationAccess _access;

    public NoteService(AppDbContext db, ICurrentUser user, IApplicationAccess access)
    {
        _db = db;
        _user = user;
        _access = access;
    }

    public async Task<IReadOnlyList<NoteDto>?> ListForApplicationAsync(
        Guid jobApplicationId, CancellationToken ct = default)
    {
        // Reading the thread is exactly what a cross-department panel member needs, so
        // participation reach is enough — that is the point of the grant (ADR-0017 §4).
        var reach = await _access.ResolveAsync(jobApplicationId, ct);
        if (reach is null) return null;

        var notes = await _db.Notes.AsNoTracking()
            .Where(n => n.JobApplicationId == jobApplicationId)
            .OrderBy(n => n.CreatedAt)
            .ToListAsync(ct);

        return await MapAsync(notes, ct);
    }

    public async Task<NoteDto?> CreateAsync(
        Guid jobApplicationId, CreateNoteRequest request, CancellationToken ct = default)
    {
        // Reach, not reach.CanWrite, and on purpose: a panel member debriefing in the thread
        // is the job they were added to do (ADR-0017 §4). CanWrite gates writes to the
        // *process* — rescheduling, panels, stage moves — not a participant's own
        // contribution. Pinned by A_Panel_Member_Can_Read_And_Join_The_Thread.
        var reach = await _access.ResolveAsync(jobApplicationId, ct);
        if (reach is null) return null;

        var userId = _user.UserId;
        if (userId is null) return null;

        if (string.IsNullOrWhiteSpace(request.Body))
            throw new InvalidOperationException("A note needs a body.");

        // A note pinned to an interview must be pinned to an interview on THIS application,
        // or the id becomes a way to attach commentary to a candidate the author cannot see.
        if (request.InterviewId is not null)
        {
            var belongs = await _db.Interviews.AsNoTracking()
                .AnyAsync(i => i.Id == request.InterviewId.Value
                               && i.JobApplicationId == jobApplicationId, ct);

            if (!belongs)
                throw new InvalidOperationException(
                    "That interview does not belong to this application.");
        }

        var note = new Note
        {
            JobApplicationId = jobApplicationId,
            InterviewId = request.InterviewId,
            AuthorUserId = userId.Value,
            Body = request.Body.Trim(),
        };
        _db.Notes.Add(note);

        foreach (var target in await ResolveMentionsAsync(note.Body, jobApplicationId, ct))
        {
            _db.NoteMentions.Add(new NoteMention
            {
                NoteId = note.Id,
                MentionedUserId = target.UserId,
            });
        }

        await _db.SaveChangesAsync(ct);

        return (await MapAsync(new[] { note }, ct)).Single();
    }

    // ---------- helpers ----------

    /// <summary>Turns `@handle` tokens in the body into users.
    ///
    /// <para>Handles are parsed from the text, never taken from the request: a
    /// client-supplied mention list would let anyone post a note that appears addressed to
    /// a colleague, and — once Module 7 delivers notifications — make the system notify on
    /// their behalf.</para>
    ///
    /// <para>A handle only resolves if that user could reach this application themselves.
    /// Otherwise a mention becomes a disclosure channel: "@finance.approver what do you
    /// think of this candidate" would put a name and a judgement in front of someone with
    /// no business seeing either, and (again, with notifications) mail it to them.</para>
    /// </summary>
    private async Task<List<MentionParser.MentionTarget>> ResolveMentionsAsync(
        string body, Guid jobApplicationId, CancellationToken ct)
    {
        var handles = MentionParser.DistinctHandles(body);
        if (handles.Count == 0) return new List<MentionParser.MentionTarget>();

        // Candidates for matching: the email local-part, or the display name with spaces
        // removed. Both are compared lower-cased.
        var users = await _db.Users.AsNoTracking()
            .Where(u => u.IsActive)
            .Select(u => new { u.Id, u.Email, u.DisplayName })
            .ToListAsync(ct);

        var wanted = handles.ToHashSet(StringComparer.Ordinal);
        var matched = new List<MentionParser.MentionTarget>();
        var seen = new HashSet<Guid>();

        foreach (var u in users)
        {
            var local = u.Email.Contains('@')
                ? u.Email[..u.Email.IndexOf('@')].ToLowerInvariant()
                : u.Email.ToLowerInvariant();

            var handle = u.DisplayName.Replace(" ", string.Empty).ToLowerInvariant();

            if (!wanted.Contains(local) && !wanted.Contains(handle)) continue;
            if (!seen.Add(u.Id)) continue;

            matched.Add(new MentionParser.MentionTarget(u.Id, u.DisplayName));
        }

        if (matched.Count == 0) return matched;

        // Second pass: keep only those who can reach this application on their own. Done
        // after matching rather than as part of the query because reach is a rule, not a
        // column, and it lives in one place by design.
        var allowed = new List<MentionParser.MentionTarget>();
        foreach (var target in matched)
        {
            // One implementation of the reach rule, asked about a third party (ADR-0018).
            // This used to be a private copy in this file, and the copy had already drifted:
            // it granted an Approver every application in the company, which is the exact
            // case the doc comment above gives as the reason the check exists.
            if (await _access.ResolveForUserAsync(target.UserId, jobApplicationId, ct) is not null)
                allowed.Add(target);
        }

        return allowed;
    }

    private async Task<IReadOnlyList<NoteDto>> MapAsync(
        IReadOnlyCollection<Note> notes, CancellationToken ct)
    {
        if (notes.Count == 0) return Array.Empty<NoteDto>();

        var noteIds = notes.Select(n => n.Id).ToList();

        var mentions = await (
            from m in _db.NoteMentions.AsNoTracking()
            join u in _db.Users.AsNoTracking() on m.MentionedUserId equals u.Id
            where noteIds.Contains(m.NoteId)
            select new { m.NoteId, m.MentionedUserId, u.DisplayName, u.Email }
        ).ToListAsync(ct);

        var authorIds = notes.Select(n => n.AuthorUserId).Distinct().ToList();
        var authors = await _db.Users.AsNoTracking()
            .Where(u => authorIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);

        return notes.Select(n =>
        {
            var mine = mentions.Where(m => m.NoteId == n.Id).ToList();

            // Keyed by every handle form the parser could have produced, so the markup lands
            // on the token the author actually typed.
            var lookup = new Dictionary<string, MentionParser.MentionTarget>(StringComparer.Ordinal);
            foreach (var m in mine)
            {
                var target = new MentionParser.MentionTarget(m.MentionedUserId, m.DisplayName);

                var local = m.Email.Contains('@')
                    ? m.Email[..m.Email.IndexOf('@')].ToLowerInvariant()
                    : m.Email.ToLowerInvariant();

                lookup[local] = target;
                lookup[m.DisplayName.Replace(" ", string.Empty).ToLowerInvariant()] = target;
            }

            return new NoteDto(
                n.Id,
                n.JobApplicationId,
                n.InterviewId,
                n.AuthorUserId,
                authors.GetValueOrDefault(n.AuthorUserId) ?? "Unknown",
                n.Body,
                // Escaped here, once, so "escape on output" is the default path rather than
                // something every consumer has to remember (ADR-0017 consequences).
                MentionParser.ToSafeHtml(n.Body, lookup),
                n.CreatedAt,
                mine.Select(m => new NoteMentionDto(m.MentionedUserId, m.DisplayName)).ToList());
        }).ToList();
    }
}
