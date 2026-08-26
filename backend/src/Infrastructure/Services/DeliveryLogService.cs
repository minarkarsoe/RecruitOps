using Microsoft.EntityFrameworkCore;
using RecruitOps.Application.Common;
using RecruitOps.Application.DTOs;
using RecruitOps.Application.Interfaces;
using RecruitOps.Domain.Enums;
using RecruitOps.Infrastructure.Persistence;

namespace RecruitOps.Infrastructure.Services;

/// <summary>The read side of the outbox (ADR-0026), and the screen drawn in
/// `design/internal/channels.html`.
///
/// <para><b>The whole risk in this file is one indirection.</b> Every other candidate-facing
/// service reaches a department off a row it already has — an application has a posting, a posting
/// has a department. <c>OutboundMessage</c> has neither. It has
/// <c>SubjectType</c> + <c>SubjectId</c>, a deliberately loose pointer, and a department is four
/// joins away through it. A filter you have to remember to write, on a table where forgetting it
/// silently shows a Hiring Manager every other department's candidates, is precisely the failure
/// ADR-0003 warns about — so it is written once, here, and it fails <b>closed</b>.</para>
/// </summary>
public class DeliveryLogService : IDeliveryLogService
{
    private const int MaxPageSize = 100;

    private readonly AppDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IDepartmentAccess _departments;

    public DeliveryLogService(AppDbContext db, ICurrentUser user, IDepartmentAccess departments)
    {
        _db = db;
        _user = user;
        _departments = departments;
    }

    public async Task<PagedResult<DeliveryLogEntryDto>> QueryAsync(
        DeliveryLogQuery query, CancellationToken ct = default)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);

        // Clause 0 (ADR-0018): an Approver has no standing reach into candidate data. The delivery
        // log is a list of things said to candidates, so it is candidate data — being company-wide
        // on the requisition axis buys nothing here.
        if (_user.IsExcludedFromCandidateData)
            return Empty(page, pageSize);

        // Clause 1 (ADR-0003): department scoping. `null` means "reaches every department", which
        // is Admin / HrDirector / Recruiter. An empty set means a scoped user attached to no
        // department, and that is nothing rather than everything.
        HashSet<Guid>? allowedDepartmentIds = null;
        if (_user.IsDepartmentScoped)
        {
            var ids = await _departments.AccessibleDepartmentIdsAsync(ct);
            if (ids.Count == 0) return Empty(page, pageSize);
            allowedDepartmentIds = ids.ToHashSet();
        }

        var messages = _db.OutboundMessages.AsNoTracking();

        if (query.Status is { } status) messages = messages.Where(m => m.Status == status);
        if (query.Kind is { } kind) messages = messages.Where(m => m.Kind == kind);
        if (!string.IsNullOrWhiteSpace(query.SubjectType))
            messages = messages.Where(m => m.SubjectType == query.SubjectType);
        if (query.SubjectId is { } subjectId)
            messages = messages.Where(m => m.SubjectId == subjectId);

        // Every interview, with the department it belongs to and the candidate it is about. This
        // is the only subject type resolved today, because InterviewInvitation is the only kind
        // anything enqueues. **Adding a kind means adding its resolution here**, and the
        // fail-closed rule below is what makes forgetting to loud rather than dangerous.
        var interviewSubjects =
            from i in _db.Interviews.AsNoTracking()
            join a in _db.JobApplications.AsNoTracking() on i.JobApplicationId equals a.Id
            join p in _db.JobPostings.AsNoTracking() on a.JobPostingId equals p.Id
            join c in _db.Candidates.AsNoTracking() on a.CandidateId equals c.Id
            select new { InterviewId = i.Id, p.DepartmentId, CandidateName = c.FullName };

        // Left join: a message whose subject we do not resolve still produces a row, and the
        // scoping decision below — not the join — decides who may see it.
        var rows =
            from m in messages
            join s in interviewSubjects on m.SubjectId equals (Guid?)s.InterviewId into subjects
            from s in subjects.DefaultIfEmpty()
            select new
            {
                Message = m,
                // The SubjectType guard is redundant against random Guids and deliberate anyway:
                // "this department came from an Interview" should be readable, not inferred from
                // the fact that two tables happened not to collide.
                DepartmentId = m.SubjectType == "Interview" ? (Guid?)s.DepartmentId : null,
                CandidateName = m.SubjectType == "Interview" ? s.CandidateName : null,
            };

        if (allowedDepartmentIds is not null)
        {
            // ⚠️ FAIL CLOSED, and this is the line worth arguing about.
            //
            // `DepartmentId == null` means one of: a kind whose subject we do not resolve yet
            // (OfferSent, ScheduledReport, ChannelNotification), or a subject row that has been
            // deleted. A scoped user sees none of them. The alternative — showing unresolvable
            // rows to everyone — reads as harmless right up to the first kind whose subject IS
            // departmental and whose join nobody remembered to add here, at which point every
            // Hiring Manager quietly gains the whole company's delivery log.
            //
            // The cost is that a scoped user's log goes quiet when a new kind ships. That is a
            // missing row, which someone reports. The other way round is a leak, which nobody does.
            rows = rows.Where(r => r.DepartmentId != null
                                   && allowedDepartmentIds.Contains(r.DepartmentId.Value));
        }

        var totalCount = await rows.CountAsync(ct);

        // Newest first, and by CreatedAt rather than SentAt: an unsent row has no SentAt, and a
        // log that buries the failures under everything that worked is the log this screen exists
        // to replace.
        var items = await rows
            .OrderByDescending(r => r.Message.CreatedAt)
            .ThenByDescending(r => r.Message.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new
            {
                r.Message.Id,
                r.Message.Kind,
                r.Message.Recipient,
                r.CandidateName,
                r.Message.SubjectType,
                r.Message.SubjectId,
                r.Message.Status,
                r.Message.Attempts,
                r.Message.NextAttemptAt,
                r.Message.LastError,
                r.Message.SentAt,
                r.Message.CreatedAt,
            })
            .ToListAsync(ct);

        // NextAttemptAt is nulled out on anything that is not Pending. On a terminal row it is a
        // leftover from the last claim rather than a promise, and forwarding it would let the
        // screen say "retrying at 14:20" about a message nobody is ever trying again.
        var dtos = items.Select(r => new DeliveryLogEntryDto(
            r.Id,
            r.Kind,
            LabelFor(r.Kind),
            ChannelFor(r.Kind),
            r.Recipient,
            r.CandidateName,
            r.SubjectType,
            r.SubjectId,
            r.Status,
            r.Attempts,
            r.Status == OutboundMessageStatus.Pending ? r.NextAttemptAt : null,
            r.LastError,
            r.SentAt,
            r.CreatedAt)).ToList();

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        return new PagedResult<DeliveryLogEntryDto>(dtos, page, pageSize, totalCount, totalPages);
    }

    private static PagedResult<DeliveryLogEntryDto> Empty(int page, int pageSize)
        => new(Array.Empty<DeliveryLogEntryDto>(), page, pageSize, 0, 0);

    /// <summary>What the recruiter reads in the "Message" column. Server-side so the log and any
    /// filter built from the same enum cannot drift into two names for one thing.</summary>
    private static string LabelFor(OutboundMessageKind kind) => kind switch
    {
        OutboundMessageKind.InterviewInvitation => "Interview invitation",
        OutboundMessageKind.OfferSent => "Offer sent",
        OutboundMessageKind.OfferReminder => "Offer reminder",
        OutboundMessageKind.PreboardingHandoff => "Pre-boarding handoff",
        OutboundMessageKind.ScheduledReport => "Scheduled report",
        OutboundMessageKind.ChannelNotification => "Channel notification",
        _ => kind.ToString(),
    };

    /// <summary>⚠️ Derived, not stored. Every kind the product sends today goes by email; only
    /// Module 8's <see cref="OutboundMessageKind.ChannelNotification"/> does not, and until that
    /// module lands there is no way to know whether a given one went by Viber, Telegram or
    /// Facebook. When Module 8 adds the field, delete this and read the column.</summary>
    private static string ChannelFor(OutboundMessageKind kind)
        => kind == OutboundMessageKind.ChannelNotification ? "Channel" : "Email";
}
