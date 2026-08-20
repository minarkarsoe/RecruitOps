using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RecruitOps.Application.Common;
using RecruitOps.Application.DTOs;
using RecruitOps.Application.Interfaces;
using RecruitOps.Domain.Entities;
using RecruitOps.Domain.Enums;
using RecruitOps.Infrastructure.Persistence;

namespace RecruitOps.Infrastructure.Services;

/// <summary>Module 3 — scheduling interview rounds and managing panels.</summary>
public class InterviewService : IInterviewService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IApplicationAccess _access;
    private readonly IScorecardTemplateService _templates;
    private readonly TimeProvider _clock;

    public InterviewService(
        AppDbContext db,
        ICurrentUser user,
        IApplicationAccess access,
        IScorecardTemplateService templates,
        TimeProvider clock)
    {
        _db = db;
        _user = user;
        _access = access;
        _templates = templates;
        _clock = clock;
    }

    public async Task<InterviewDto?> ScheduleAsync(
        Guid jobApplicationId, ScheduleInterviewRequest request, CancellationToken ct = default)
    {
        var reach = await _access.ResolveAsync(jobApplicationId, ct);
        // Null covers "no such application" and "not yours" alike — 404 either way, so
        // existence is never leaked (the established convention in this codebase).
        if (reach is null) return null;

        // A panel member from another department can read this application; they cannot
        // schedule against it. Participation is a read grant (ADR-0017 §4).
        if (!reach.CanWrite) return null;

        var application = await _db.JobApplications
            .FirstOrDefaultAsync(a => a.Id == jobApplicationId, ct);
        if (application is null) return null;

        // Terminal statuses are terminal everywhere, not just in PipelineService. Scheduling
        // against a Hired or Rejected application would otherwise drag it back into the
        // pipeline through a side door and corrupt Module 5's figures.
        if (application.Status is PipelineStatus.Hired or PipelineStatus.Rejected)
            throw new InvalidOperationException(
                $"This application is {application.Status}; that is final. "
                + "Re-apply instead of scheduling against it.");

        var mode = ParseMode(request.Mode);
        ValidateSlot(request.ScheduledStart, request.DurationMinutes);

        var panel = await ResolvePanelAsync(request.ParticipantUserIds, request.LeadUserId, ct);

        // Round is derived, never supplied: a caller-set round number can duplicate or skip,
        // and nothing downstream could tell which was right.
        var existingRounds = await _db.Interviews
            .Where(i => i.JobApplicationId == jobApplicationId)
            .CountAsync(ct);

        // Resolved once, at scheduling time, and stored. Re-resolving on read would mean
        // that moving a posting between departments retroactively changes the criteria an
        // interview was actually conducted against (ADR-0017 §1).
        var template = await _templates.ResolveForPostingAsync(reach.JobPostingId, ct);

        var now = _clock.GetUtcNow();

        var interview = new Interview
        {
            TenantId = application.TenantId,
            JobApplicationId = jobApplicationId,
            Round = existingRounds + 1,
            ScheduledStart = request.ScheduledStart,
            DurationMinutes = request.DurationMinutes,
            Mode = mode,
            Location = request.Location,
            Agenda = request.Agenda,
            Status = InterviewStatus.Scheduled,
            ScorecardTemplateId = template?.Id,
            ScheduledByUserId = _user.UserId,
        };
        _db.Interviews.Add(interview);

        foreach (var member in panel)
        {
            _db.InterviewParticipants.Add(new InterviewParticipant
            {
                TenantId = application.TenantId,
                InterviewId = interview.Id,
                UserId = member.Id,
                IsLead = member.Id == request.LeadUserId,
            });
        }

        // ADR-0017 §5 — the stage move rides along in the SAME SaveChangesAsync. Two writes
        // would allow an interview to exist against an application still sitting at
        // Screening, and Module 5 reads this history to compute time-to-interview; a gap
        // here cannot be reconstructed afterwards.
        if (application.Status != PipelineStatus.Interview)
        {
            var from = application.Status;
            application.Status = PipelineStatus.Interview;
            application.UpdatedAt = now;

            _db.ApplicationStageHistories.Add(new ApplicationStageHistory
            {
                TenantId = application.TenantId,
                JobApplicationId = application.Id,
                FromStatus = from,
                ToStatus = PipelineStatus.Interview,
                ChangedAt = now,
                ChangedByUserId = _user.UserId,
                Note = $"Interview round {interview.Round} scheduled.",
            });
        }

        // ADR-0026 §2 — the transactional outbox. The invitation is a row written in the SAME
        // SaveChangesAsync as the interview it is about, so there is no state in which the round
        // exists and the intention to tell the candidate does not. Sending happens later, in the
        // worker; nothing here waits on a mail server.
        await EnqueueInvitationAsync(interview, application, isReschedule: false, ct);

        await _db.SaveChangesAsync(ct);

        return await MapAsync(interview.Id, ct);
    }

    public async Task<IReadOnlyList<InterviewDto>?> ListForApplicationAsync(
        Guid jobApplicationId, CancellationToken ct = default)
    {
        // Read: participation reach is enough here, which is the whole point of the grant.
        var reach = await _access.ResolveAsync(jobApplicationId, ct);
        if (reach is null) return null;

        var ids = await _db.Interviews.AsNoTracking()
            .Where(i => i.JobApplicationId == jobApplicationId)
            .OrderBy(i => i.Round)
            .Select(i => i.Id)
            .ToListAsync(ct);

        var result = new List<InterviewDto>(ids.Count);
        foreach (var id in ids)
        {
            var dto = await MapAsync(id, ct);
            if (dto is not null) result.Add(dto);
        }
        return result;
    }

    public async Task<InterviewDto?> GetAsync(Guid interviewId, CancellationToken ct = default)
    {
        var reach = await _access.ResolveByInterviewAsync(interviewId, ct);
        return reach is null ? null : await MapAsync(interviewId, ct);
    }

    public async Task<InterviewDto?> RescheduleAsync(
        Guid interviewId, RescheduleInterviewRequest request, CancellationToken ct = default)
    {
        var interview = await LoadWritableAsync(interviewId, ct);
        if (interview is null) return null;

        RequireOpen(interview);
        ValidateSlot(request.ScheduledStart, request.DurationMinutes);

        interview.ScheduledStart = request.ScheduledStart;
        interview.DurationMinutes = request.DurationMinutes;
        interview.Mode = ParseMode(request.Mode);
        interview.Location = request.Location;
        interview.Agenda = request.Agenda;

        // A candidate who has already blocked out Monday morning has to be told it moved. The
        // application is re-read rather than passed in because Reschedule only ever loaded the
        // interview — see EnqueueInvitationAsync for the duplicate rule.
        var application = await _db.JobApplications
            .FirstOrDefaultAsync(a => a.Id == interview.JobApplicationId, ct);

        if (application is not null)
            await EnqueueInvitationAsync(interview, application, isReschedule: true, ct);

        await _db.SaveChangesAsync(ct);
        return await MapAsync(interviewId, ct);
    }

    public async Task<InterviewDto?> SetPanelAsync(
        Guid interviewId, SetPanelRequest request, CancellationToken ct = default)
    {
        var interview = await LoadWritableAsync(interviewId, ct);
        if (interview is null) return null;

        RequireOpen(interview);

        var panel = await ResolvePanelAsync(request.ParticipantUserIds, request.LeadUserId, ct);
        var wanted = panel.Select(p => p.Id).ToHashSet();

        var existing = await _db.InterviewParticipants
            .Where(p => p.InterviewId == interviewId)
            .ToListAsync(ct);

        // Removing someone who has already scored would delete their evaluation along with
        // the access that produced it. Refuse instead: a submitted assessment is a record of
        // what happened, and silently discarding it is how audit trails stop being true.
        var scored = await _db.Scorecards.AsNoTracking()
            .Where(s => s.InterviewId == interviewId)
            .Select(s => s.InterviewerUserId)
            .ToListAsync(ct);

        var droppedWithScores = existing
            .Where(p => !wanted.Contains(p.UserId) && scored.Contains(p.UserId))
            .ToList();

        if (droppedWithScores.Count > 0)
            throw new InvalidOperationException(
                "An interviewer who has already started a scorecard cannot be removed from "
                + "the panel. Cancel the round instead.");

        foreach (var gone in existing.Where(p => !wanted.Contains(p.UserId)))
            _db.InterviewParticipants.Remove(gone);

        var kept = existing.Where(p => wanted.Contains(p.UserId)).ToList();
        foreach (var row in kept)
            row.IsLead = row.UserId == request.LeadUserId;

        var alreadyThere = kept.Select(p => p.UserId).ToHashSet();
        foreach (var member in panel.Where(m => !alreadyThere.Contains(m.Id)))
        {
            _db.InterviewParticipants.Add(new InterviewParticipant
            {
                TenantId = interview.TenantId,
                InterviewId = interviewId,
                UserId = member.Id,
                IsLead = member.Id == request.LeadUserId,
            });
        }

        await _db.SaveChangesAsync(ct);
        return await MapAsync(interviewId, ct);
    }

    public async Task<InterviewDto?> CancelAsync(
        Guid interviewId, CancelInterviewRequest request, CancellationToken ct = default)
    {
        var interview = await LoadWritableAsync(interviewId, ct);
        if (interview is null) return null;

        RequireOpen(interview);

        interview.Status = InterviewStatus.Cancelled;
        interview.CancellationReason = request.Reason;

        // The application stays at Interview on purpose. A cancelled round does not undo
        // the decision to interview, and rewinding the stage would write a history row
        // claiming the candidate moved backwards when nothing about them changed.
        //
        // Nothing is done to a queued invitation here, and that is not an omission: the handler
        // reads the round's status at send time and suppresses an invitation to a round that is
        // no longer Scheduled. Cancelling is one write, in one place.
        await _db.SaveChangesAsync(ct);
        return await MapAsync(interviewId, ct);
    }

    public async Task<InterviewDto?> CompleteAsync(
        Guid interviewId, CompleteInterviewRequest request, CancellationToken ct = default)
    {
        var interview = await LoadWritableAsync(interviewId, ct);
        if (interview is null) return null;

        RequireOpen(interview);

        interview.Status = request.NoShow ? InterviewStatus.NoShow : InterviewStatus.Completed;

        await _db.SaveChangesAsync(ct);
        return await MapAsync(interviewId, ct);
    }

    // ---------- helpers ----------

    /// <summary>Loads an interview the caller may <b>write</b> to, or null for 404.
    /// <para>Every mutating method above goes through this one door. The recurring bug in
    /// this repo is a guard that reached two of three sibling methods, so the guard is not
    /// something each method gets to remember.</para></summary>
    private async Task<Interview?> LoadWritableAsync(Guid interviewId, CancellationToken ct)
    {
        var reach = await _access.ResolveByInterviewAsync(interviewId, ct);
        if (reach is null || !reach.CanWrite) return null;

        return await _db.Interviews.FirstOrDefaultAsync(i => i.Id == interviewId, ct);
    }

    private static void RequireOpen(Interview interview)
    {
        if (interview.Status != InterviewStatus.Scheduled)
            throw new InvalidOperationException(
                $"This round is {interview.Status}; it can no longer be changed.");
    }

    private static InterviewMode ParseMode(string value)
    {
        if (!Enum.TryParse<InterviewMode>(value, ignoreCase: false, out var mode))
            throw new InvalidOperationException($"'{value}' is not an interview mode.");
        return mode;
    }

    private static void ValidateSlot(DateTimeOffset start, int durationMinutes)
    {
        // Not "must be in the future": recording a round that already happened is a
        // legitimate back-fill, and refusing it would push people back into a spreadsheet.
        // What is refused is a slot no calendar could represent.
        if (start == default)
            throw new InvalidOperationException("An interview needs a start time.");

        if (durationMinutes is < 5 or > 480)
            throw new InvalidOperationException(
                "Interview length must be between 5 minutes and 8 hours.");
    }

    /// <summary>Validates the requested panel and returns the users, or throws.</summary>
    private async Task<List<User>> ResolvePanelAsync(
        IReadOnlyList<Guid> userIds, Guid? leadUserId, CancellationToken ct)
    {
        var distinct = userIds.Distinct().ToList();
        if (distinct.Count == 0)
            throw new InvalidOperationException("An interview needs at least one interviewer.");

        var users = await _db.Users
            .Where(u => distinct.Contains(u.Id) && u.IsActive)
            .ToListAsync(ct);

        // A 409 listing what was wrong, not a silent skip: an interviewer quietly dropped
        // from the panel is discovered on the day, by their absence.
        if (users.Count != distinct.Count)
        {
            var found = users.Select(u => u.Id).ToHashSet();
            var missing = distinct.Where(id => !found.Contains(id)).ToList();
            throw new InvalidOperationException(
                $"{missing.Count} of the requested interviewers are unknown or deactivated.");
        }

        if (leadUserId is not null && !distinct.Contains(leadUserId.Value))
            throw new InvalidOperationException("The panel lead must be on the panel.");

        return users;
    }

    /// <summary>Queues the candidate's invitation (Module 3.2, ADR-0026 §2).
    ///
    /// <para><b>Adds to the change tracker and does not save.</b> The caller's
    /// <c>SaveChangesAsync</c> is what makes this the transactional outbox: the interview and the
    /// intention to tell the candidate about it commit together or not at all.</para>
    ///
    /// <para><b>The duplicate rule.</b> If an invitation for this round is still Pending it is
    /// left exactly as it is, and nothing new is queued. It has not gone yet, and the handler
    /// renders the slot live — so that one row will carry the new time by itself. Queueing a
    /// second row would send the candidate an invitation and, seconds later, a notice that their
    /// time had "changed" from one they were never given.</para>
    ///
    /// <para>A candidate with no email address still gets a row, with an empty recipient. The row
    /// exists so that "was this person told?" has an answer, and <i>no, we have no address for
    /// them</i> is an answer — in the delivery log, where a recruiter will see it.</para></summary>
    private async Task EnqueueInvitationAsync(
        Interview interview, JobApplication application, bool isReschedule, CancellationToken ct)
    {
        var alreadyQueued = await _db.OutboundMessages.AnyAsync(m =>
            m.Kind == OutboundMessageKind.InterviewInvitation
            && m.SubjectId == interview.Id
            && m.Status == OutboundMessageStatus.Pending, ct);

        if (alreadyQueued) return;

        var recipient = await _db.Candidates
            .Where(c => c.Id == application.CandidateId)
            .Select(c => c.Email)
            .FirstOrDefaultAsync(ct);

        // Frozen into the payload rather than read at send time: it is the zone the recruiter
        // picked the slot in, and re-reading it later would silently rewrite an invitation that
        // has already gone out if the company setting is ever corrected.
        var timeZoneId = await _db.Companies
            .Where(c => c.Id == application.TenantId)
            .Select(c => c.TimeZoneId)
            .FirstOrDefaultAsync(ct);

        _db.OutboundMessages.Add(new OutboundMessage
        {
            TenantId = application.TenantId,
            Kind = OutboundMessageKind.InterviewInvitation,
            Recipient = recipient ?? string.Empty,
            SubjectType = nameof(Interview),
            SubjectId = interview.Id,
            PayloadJson = JsonSerializer.Serialize(
                new InterviewInvitationPayload(timeZoneId ?? TimeZoneInfo.Utc.Id, isReschedule)),
            Status = OutboundMessageStatus.Pending,
            NextAttemptAt = _clock.GetUtcNow(),
        });
    }

    private async Task<InterviewDto?> MapAsync(Guid interviewId, CancellationToken ct)
    {
        var interview = await _db.Interviews.AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == interviewId, ct);
        if (interview is null) return null;

        var participants = await (
            from p in _db.InterviewParticipants.AsNoTracking()
            join u in _db.Users.AsNoTracking() on p.UserId equals u.Id
            where p.InterviewId == interviewId
            select new { p.UserId, u.DisplayName, u.Email, p.IsLead }
        ).ToListAsync(ct);

        var submitted = await _db.Scorecards.AsNoTracking()
            .Where(s => s.InterviewId == interviewId && s.Status == ScorecardStatus.Submitted)
            .Select(s => s.InterviewerUserId)
            .ToListAsync(ct);

        string? templateName = null;
        if (interview.ScorecardTemplateId is not null)
        {
            templateName = await _db.ScorecardTemplates.AsNoTracking()
                .Where(t => t.Id == interview.ScorecardTemplateId.Value)
                .Select(t => t.Name)
                .FirstOrDefaultAsync(ct);
        }

        // Projected in memory: EF Core 10 will not translate enum.ToString().
        return new InterviewDto(
            interview.Id,
            interview.JobApplicationId,
            interview.Round,
            interview.ScheduledStart,
            interview.DurationMinutes,
            interview.Mode.ToString(),
            interview.Location,
            interview.Status.ToString(),
            interview.Agenda,
            interview.CancellationReason,
            interview.ScorecardTemplateId,
            templateName,
            participants
                .OrderByDescending(p => p.IsLead)
                .ThenBy(p => p.DisplayName)
                .Select(p => new InterviewParticipantDto(
                    p.UserId,
                    p.DisplayName,
                    p.Email,
                    p.IsLead,
                    submitted.Contains(p.UserId)))
                .ToList());
    }
}
