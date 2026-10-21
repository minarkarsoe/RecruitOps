using Microsoft.EntityFrameworkCore;
using Npgsql;
using RecruitOps.Application.Common;
using RecruitOps.Application.DTOs;
using RecruitOps.Application.Interfaces;
using RecruitOps.Domain;
using RecruitOps.Domain.Entities;
using RecruitOps.Domain.Enums;
using RecruitOps.Infrastructure.Persistence;

namespace RecruitOps.Infrastructure.Services;

/// <summary>Module 4 offers, slice A: drafting from an application, the budget check, internal
/// approval and the dashboard (ADR-0030). Sending, the candidate portal and pre-boarding are later
/// slices.
///
/// <para><b>Reach</b> (ADR-0030 §4) is decided per offer, in one place (<see cref="ReachAsync"/>):
/// recruitment staff manage every offer; anyone named on an offer's approval step — in any round —
/// sees and decides that offer, never the candidate's file; a Hiring Manager sees their departments'
/// offers read-only, without salary unless the company shows it. Everyone else: null, which the
/// controller turns into a 404.</para></summary>
public class OfferService : IOfferService
{
    /// <summary>Offers that block a new one for the same application: anything not yet closed.
    /// A declined or expired offer can be followed by a new one.</summary>
    private static readonly OfferStatus[] Open =
    {
        OfferStatus.Draft, OfferStatus.PendingApproval, OfferStatus.Approved,
        OfferStatus.Sent, OfferStatus.Viewed, OfferStatus.Accepted,
    };

    private const int MaxListed = 500;

    private const string NumberIndex = "IX_Offers_TenantId_Year_Number";
    private const string OneOpenIndex = "IX_Offers_OneOpenPerApplication";
    private const string ChangedMeanwhile = "This offer was changed by someone else a moment ago. Reload and try again.";

    private readonly AppDbContext _db;
    private readonly ICurrentUser _user;
    private readonly ICurrentTenant _tenant;
    private readonly IDepartmentAccess _departments;
    private readonly IAuditTrail _audit;
    private readonly TimeProvider _clock;

    /// <summary>How far ahead an offer may expire. The candidate's link lives until then, and the link
    /// reaches salary and, after acceptance, identity documents (ADR-0030 §5).</summary>
    private const int MaxDaysToExpiry = 90;

    public OfferService(
        AppDbContext db, ICurrentUser user, ICurrentTenant tenant, IDepartmentAccess departments,
        IAuditTrail audit, TimeProvider clock)
    {
        _db = db;
        _user = user;
        _tenant = tenant;
        _departments = departments;
        _audit = audit;
        _clock = clock;
    }

    // ── Reach ───────────────────────────────────────────────────────────────

    private enum Access { View, Decide, Manage }

    private sealed record Reach(Access Access, bool SalaryVisible);

    /// <summary>The same set as <c>Policies.RecruitmentStaff</c>; a custom role carries its base role.</summary>
    private bool IsRecruitmentStaff =>
        RoleScope.Parse(_user.Role) is UserRole.Admin or UserRole.HrDirector or UserRole.Recruiter;

    private bool IsHiringManager => RoleScope.Parse(_user.Role) is UserRole.HiringManager;

    private async Task<Reach?> ReachAsync(Guid offerId, Guid departmentId, CancellationToken ct)
    {
        if (IsRecruitmentStaff) return new Reach(Access.Manage, true);
        if (_user.UserId is not { } userId) return null;

        // Being named on the LIVE round is the grant (ADR-0030 §4) — the same shape as the panel
        // exception: it reaches the offer, with its terms, because deciding it needs them; not the
        // candidate's file. Not any round, ever: an old round's approver whose chain has since changed
        // would otherwise keep reading this offer's salary for good (security review, M2).
        if (await NamedOnLiveRound().AnyAsync(a => a.OfferId == offerId && a.ApproverUserId == userId, ct))
            return new Reach(Access.Decide, true);

        if (IsHiringManager && await _departments.CanReachCandidatesInAsync(departmentId, ct))
            return new Reach(Access.View, await ShowSalaryToHiringManagersAsync(ct));

        return null;
    }

    /// <summary>Approval steps of each offer's highest round.</summary>
    private IQueryable<OfferApproval> NamedOnLiveRound() =>
        _db.OfferApprovals.Where(a => a.Round == _db.OfferApprovals.Where(b => b.OfferId == a.OfferId).Max(b => b.Round));

    private Task<bool> ShowSalaryToHiringManagersAsync(CancellationToken ct) =>
        _db.Companies.Where(c => c.Id == _tenant.TenantId).Select(c => c.ShowOfferSalaryToHiringManagers)
            .FirstOrDefaultAsync(ct);

    // ── Reads ───────────────────────────────────────────────────────────────

    /// <summary>An offer with what it is read through — never copied onto it: the candidate, the
    /// posting's title and department, the requisition's budget.
    /// <para>Init properties, not a positional record: EF maps a member-initialiser projection, so
    /// the list's filters can still run in SQL after it; a constructor it cannot see through.</para></summary>
    private sealed record Row
    {
        public required Offer Offer { get; init; }
        public required string CandidateName { get; init; }
        public required string Position { get; init; }
        public required Guid DepartmentId { get; init; }
        public required string DepartmentName { get; init; }
        public required Guid RequisitionId { get; init; }
        public required decimal? Budget { get; init; }
    }

    private IQueryable<Row> Rows() =>
        from o in _db.Offers
        join a in _db.JobApplications on o.JobApplicationId equals a.Id
        join c in _db.Candidates on a.CandidateId equals c.Id
        join p in _db.JobPostings on a.JobPostingId equals p.Id
        join d in _db.Departments on p.DepartmentId equals d.Id
        join r in _db.Requisitions on p.RequisitionId equals r.Id
        select new Row
        {
            Offer = o,
            CandidateName = c.FullName,
            Position = p.Title,
            DepartmentId = p.DepartmentId,
            DepartmentName = d.Name,
            RequisitionId = r.Id,
            Budget = r.SalaryBudget,
        };

    public async Task<IReadOnlyList<OfferListItemDto>> ListAsync(
        string? group, Guid? departmentId, DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        var query = Rows();
        var staff = IsRecruitmentStaff;
        var named = new HashSet<Guid>();

        if (!staff)
        {
            if (_user.UserId is not { } userId) return Array.Empty<OfferListItemDto>();
            named = (await NamedOnLiveRound().AsNoTracking().Where(a => a.ApproverUserId == userId)
                .Select(a => a.OfferId).Distinct().ToListAsync(ct)).ToHashSet();
            var departments = IsHiringManager
                ? await _departments.AccessibleDepartmentIdsAsync(ct)
                : Array.Empty<Guid>();
            query = query.Where(x => named.Contains(x.Offer.Id) || departments.Contains(x.DepartmentId));
        }

        var statuses = group switch
        {
            "awaiting" => new[] { OfferStatus.PendingApproval },
            "with-candidate" => new[] { OfferStatus.Sent, OfferStatus.Viewed },
            "closed" => new[] { OfferStatus.Accepted, OfferStatus.Rejected, OfferStatus.Expired },
            _ => null,
        };
        if (statuses is not null) query = query.Where(x => statuses.Contains(x.Offer.Status));
        if (departmentId is { } dept) query = query.Where(x => x.DepartmentId == dept);
        if (from is { } f)
        {
            var since = new DateTimeOffset(f.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            query = query.Where(x => x.Offer.CreatedAt >= since);
        }
        if (to is { } t)
        {
            var before = new DateTimeOffset(t.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            query = query.Where(x => x.Offer.CreatedAt < before);
        }

        var rows = await query.AsNoTracking()
            .OrderByDescending(x => x.Offer.CreatedAt)
            .Take(MaxListed)
            .ToListAsync(ct);

        var showToManagers = !staff && rows.Count > 0 && await ShowSalaryToHiringManagersAsync(ct);
        var (awaiting, waitingOn) = await LiveRoundsAsync(rows.Select(r => r.Offer).ToList(), ct);

        return rows.Select(x => new OfferListItemDto(
            x.Offer.Id, Reference(x.Offer), x.Offer.JobApplicationId, x.CandidateName, x.Position,
            x.DepartmentId, x.DepartmentName,
            staff || named.Contains(x.Offer.Id) || showToManagers ? x.Offer.BaseSalary : null,
            x.Offer.Status.ToString(), x.Offer.CreatedAt, x.Offer.SentAt,
            awaiting.Contains(x.Offer.Id), waitingOn.GetValueOrDefault(x.Offer.Id))).ToList();
    }

    /// <summary>For offers pending approval: those whose live round holds a waiting step of the
    /// caller's, and whose turn it is on each.</summary>
    private async Task<(HashSet<Guid> AwaitingMe, Dictionary<Guid, string> WaitingOn)> LiveRoundsAsync(
        IReadOnlyCollection<Offer> offers, CancellationToken ct)
    {
        var awaiting = new HashSet<Guid>();
        var waitingOn = new Dictionary<Guid, string>();
        var pending = offers.Where(o => o.Status == OfferStatus.PendingApproval).Select(o => o.Id).ToList();
        if (pending.Count == 0) return (awaiting, waitingOn);

        var steps = await _db.OfferApprovals.AsNoTracking().Where(a => pending.Contains(a.OfferId)).ToListAsync(ct);
        foreach (var group in steps.GroupBy(s => s.OfferId))
        {
            var live = ApprovalRail.LiveRound(group);
            if (ApprovalRail.ActiveStep(live) is { } active) waitingOn[group.Key] = active.Label;
            if (_user.UserId is { } userId && ApprovalRail.WaitingStepOf(live, userId) is not null) awaiting.Add(group.Key);
        }
        return (awaiting, waitingOn);
    }

    public async Task<IReadOnlyList<OfferEligibleApplicationDto>> EligibleApplicationsAsync(CancellationToken ct = default)
    {
        if (!IsRecruitmentStaff) return Array.Empty<OfferEligibleApplicationDto>();

        return await (
            from a in _db.JobApplications.AsNoTracking()
            join c in _db.Candidates on a.CandidateId equals c.Id
            join p in _db.JobPostings on a.JobPostingId equals p.Id
            join d in _db.Departments on p.DepartmentId equals d.Id
            where a.Status == PipelineStatus.Offer
                  && !_db.Offers.Any(o => o.JobApplicationId == a.Id && Open.Contains(o.Status))
            orderby a.AppliedAt
            select new OfferEligibleApplicationDto(a.Id, c.FullName, p.Title, d.Name, a.AppliedAt)
        ).Take(MaxListed).ToListAsync(ct);
    }

    public async Task<OfferDetailDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var row = await Rows().AsNoTracking().FirstOrDefaultAsync(x => x.Offer.Id == id, ct);
        if (row is null) return null;
        var reach = await ReachAsync(id, row.DepartmentId, ct);
        return reach is null ? null : await DetailAsync(row, reach, ct);
    }

    public async Task<OfferLetterDto?> RenderLetterAsync(Guid id, CancellationToken ct = default)
    {
        var row = await Rows().AsNoTracking().FirstOrDefaultAsync(x => x.Offer.Id == id, ct);
        if (row is null) return null;

        // The letter quotes the salary, so it follows the salary rule.
        var reach = await ReachAsync(id, row.DepartmentId, ct);
        if (reach is null || !reach.SalaryVisible) return null;

        return new OfferLetterDto(await RenderAsync(row, ct));
    }

    private async Task<string> RenderAsync(Row row, CancellationToken ct)
    {
        var company = await _db.Companies.Where(c => c.Id == _tenant.TenantId).Select(c => c.Name)
            .FirstOrDefaultAsync(ct) ?? string.Empty;
        var o = row.Offer;
        return OfferLetter.Render(o.LetterBody, new OfferLetter.Terms(
            row.CandidateName, row.Position, row.DepartmentName, company,
            o.BaseSalary, o.Allowances, o.JoiningDate, o.ProbationMonths, o.ExpiresOn));
    }

    private async Task<OfferDetailDto> DetailAsync(Row row, Reach reach, CancellationToken ct)
    {
        var o = row.Offer;
        var approvals = await _db.OfferApprovals.AsNoTracking()
            .Where(a => a.OfferId == o.Id)
            .OrderBy(a => a.Round).ThenBy(a => a.Sequence)
            .Select(a => new ApprovalStepDto(
                a.Round, a.Sequence, a.Label, a.ApproverUserId, a.Decision.ToString(),
                a.DecidedAt, a.Comment, a.DecidedByUserId))
            .ToListAsync(ct);

        var over = OverBudget(o, row.Budget);
        var policy = await RequireApprovalAsync(ct);
        var needs = over || policy;
        var show = reach.SalaryVisible;

        // An approver's comment is free text, and "come down to 800,000" quotes the package: it follows
        // the salary rule like every money field (security review, M1).
        if (!show) approvals = approvals.Select(a => a with { Comment = null }).ToList();

        // "Approval this will need" — for the people who can submit it, while it can be submitted.
        IReadOnlyList<string>? preview = null;
        if (reach.Access == Access.Manage && o.Status == OfferStatus.Draft && needs
            && await OfferChainStepsAsync(row.DepartmentId, ct) is { Count: > 0 } steps)
            preview = steps.Select(s => s.Label).ToList();

        return new OfferDetailDto(
            o.Id, Reference(o), o.Status.ToString(), o.JobApplicationId, row.RequisitionId,
            row.CandidateName, row.Position, row.DepartmentId, row.DepartmentName,
            show,
            show ? o.BaseSalary : null,
            show ? o.Allowances : null,
            show ? row.Budget : null,
            show ? over : null,
            show ? needs : null,
            show ? policy : null,
            o.JoiningDate, o.ProbationMonths, o.ExpiresOn, o.OfferTemplateId,
            show ? o.LetterBody : null,
            reach.Access == Access.Manage,
            preview,
            approvals,
            o.CreatedAt, o.SubmittedAt, o.ApprovedAt, o.SentAt);
    }

    /// <summary>The department's offer chain, else the company-wide one — the way a requisition finds
    /// its chain, with the purpose that keeps the two apart (ADR-0030 §1). Null when none exists.</summary>
    private async Task<List<ApprovalChainStep>?> OfferChainStepsAsync(Guid departmentId, CancellationToken ct)
    {
        var chain = await _db.ApprovalChains.AsNoTracking()
            .Where(c => c.IsActive && c.Purpose == ApprovalChainPurpose.Offer
                        && (c.DepartmentId == departmentId || c.DepartmentId == null))
            .OrderBy(c => c.DepartmentId == null ? 1 : 0)
            .ThenByDescending(c => c.CreatedAt) // two active chains for one department: the newest, every time
            .FirstOrDefaultAsync(ct);
        if (chain is null) return null;
        return await _db.ApprovalChainSteps.AsNoTracking().Where(s => s.ApprovalChainId == chain.Id)
            .OrderBy(s => s.Sequence).ToListAsync(ct);
    }

    private static bool OverBudget(Offer o, decimal? budget) => budget is { } b && o.BaseSalary + o.Allowances > b;

    private Task<bool> RequireApprovalAsync(CancellationToken ct) =>
        _db.Companies.Where(c => c.Id == _tenant.TenantId).Select(c => c.RequireOfferApproval).FirstOrDefaultAsync(ct);

    private static string Reference(Offer o) => $"OFR-{o.Year}-{o.Number:D4}";

    // ── Writes ──────────────────────────────────────────────────────────────

    /// <summary>The offer, tracked, with its context — or null when it is out of the caller's reach
    /// for <paramref name="needed"/>.</summary>
    private async Task<(Offer Offer, Row Row)?> LoadAsync(Guid id, Access needed, CancellationToken ct)
    {
        var row = await Rows().AsNoTracking().FirstOrDefaultAsync(x => x.Offer.Id == id, ct);
        if (row is null) return null;
        var reach = await ReachAsync(id, row.DepartmentId, ct);
        if (reach is null || reach.Access < needed) return null;
        var offer = await _db.Offers.FirstOrDefaultAsync(o => o.Id == id, ct);
        return offer is null ? null : (offer, row with { Offer = offer });
    }

    private async Task<OfferDetailDto?> ReloadAsync(Guid id, CancellationToken ct)
    {
        _db.ChangeTracker.Clear();
        return await GetAsync(id, ct);
    }

    public async Task<OfferDetailDto?> CreateAsync(CreateOfferRequest request, CancellationToken ct = default)
    {
        if (!IsRecruitmentStaff || _user.UserId is not { } userId) return null;

        var source = await (
            from a in _db.JobApplications
            join p in _db.JobPostings on a.JobPostingId equals p.Id
            join r in _db.Requisitions on p.RequisitionId equals r.Id
            where a.Id == request.JobApplicationId
            select new { a.Id, a.Status, a.TenantId, r.SalaryBudget }
        ).FirstOrDefaultAsync(ct);
        if (source is null) return null;

        if (source.Status != PipelineStatus.Offer)
            throw new InvalidOperationException("Move the candidate to the Offer stage before drafting an offer.");

        var existing = await _db.Offers.AsNoTracking()
            .Where(o => o.JobApplicationId == source.Id && Open.Contains(o.Status))
            .FirstOrDefaultAsync(ct);
        if (existing is not null)
            throw new InvalidOperationException($"This application already has an open offer, {Reference(existing)}.");

        var body = OfferLetter.DefaultBody;
        if (request.OfferTemplateId is { } templateId)
        {
            body = await _db.OfferTemplates.Where(t => t.Id == templateId && t.IsActive)
                       .Select(t => t.Body).FirstOrDefaultAsync(ct)
                   ?? throw new InvalidOperationException("That offer template does not exist or is not active.");
        }

        var today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
        var offer = new Offer
        {
            TenantId = source.TenantId,
            JobApplicationId = source.Id,
            Year = today.Year,
            Status = OfferStatus.Draft,
            // Pre-filled, not re-entered (spec: "Data inflow"): the budget is the starting point.
            BaseSalary = source.SalaryBudget ?? 0,
            Allowances = 0,
            JoiningDate = today.AddDays(30),
            ProbationMonths = 3,
            ExpiresOn = today.AddDays(7),
            OfferTemplateId = request.OfferTemplateId,
            LetterBody = body,
            CreatedByUserId = userId,
        };

        // Numbered per company per year. The unique index is the guarantee; two drafts created at
        // the same moment collide on it, and the second simply takes the next number.
        for (var attempt = 0; ; attempt++)
        {
            offer.Number = 1 + (await _db.Offers.Where(o => o.Year == offer.Year)
                .Select(o => (int?)o.Number).MaxAsync(ct) ?? 0);
            _db.Offers.Add(offer);
            try
            {
                await _db.SaveChangesAsync(ct);
                break;
            }
            catch (DbUpdateException ex) when (ConstraintOf(ex) == OneOpenIndex)
            {
                throw new InvalidOperationException("This application already has an open offer.");
            }
            catch (DbUpdateException ex) when (attempt < 2 && ConstraintOf(ex) is NumberIndex or null)
            {
                // The number was taken a moment ago. (null: a provider that names no constraint —
                // the in-memory one enforces none, so this branch is Postgres's.)
                _db.Entry(offer).State = EntityState.Detached;
            }
        }

        return await ReloadAsync(offer.Id, ct);
    }

    public async Task<OfferDetailDto?> UpdateAsync(Guid id, UpdateOfferRequest request, CancellationToken ct = default)
    {
        if (await LoadAsync(id, Access.Manage, ct) is not { } loaded) return null;
        var offer = loaded.Offer;

        if (offer.Status != OfferStatus.Draft)
            throw new InvalidOperationException(
                $"Only a Draft can be edited; this offer is {offer.Status}. Revise it to change the terms.");

        offer.BaseSalary = request.BaseSalary;
        offer.Allowances = request.Allowances;
        // Validated present by [Required]; nullable only so that a missing date is a 400.
        offer.JoiningDate = request.JoiningDate!.Value;
        offer.ProbationMonths = request.ProbationMonths;
        offer.ExpiresOn = request.ExpiresOn!.Value;
        offer.LetterBody = request.LetterBody;

        await SaveAsync(ct);
        return await ReloadAsync(id, ct);
    }

    public async Task<OfferDetailDto?> SubmitAsync(Guid id, CancellationToken ct = default)
    {
        if (await LoadAsync(id, Access.Manage, ct) is not { } loaded) return null;
        var (offer, row) = loaded;

        if (offer.Status != OfferStatus.Draft)
            throw new InvalidOperationException($"Only a Draft can be sent for approval; this offer is {offer.Status}.");

        var today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
        if (offer.ExpiresOn < today)
            throw new InvalidOperationException("The offer's expiry date has passed. Set a new one first.");
        if (offer.ExpiresOn > today.AddDays(MaxDaysToExpiry))
            throw new InvalidOperationException($"An offer can expire at most {MaxDaysToExpiry} days ahead: the candidate's link lives until then.");
        if (offer.JoiningDate < today)
            throw new InvalidOperationException("The joining date is in the past.");

        if (!OverBudget(offer, row.Budget) && !await RequireApprovalAsync(ct))
            throw new InvalidOperationException(
                "This offer is within the requisition's budget and needs no internal approval.");

        var steps = await OfferChainStepsAsync(row.DepartmentId, ct)
            ?? throw new InvalidOperationException(
                "No offer approval chain is configured for this department. An Admin can add one under Approval chains.");
        if (steps.Count == 0)
            throw new InvalidOperationException("The offer approval chain has no steps configured.");

        // A resubmission opens the next round beside the earlier ones (ADR-0023).
        var round = ApprovalRail.NextRound(await _db.OfferApprovals.Where(a => a.OfferId == offer.Id).ToListAsync(ct));
        foreach (var step in steps)
        {
            _db.OfferApprovals.Add(new OfferApproval
            {
                TenantId = offer.TenantId,
                OfferId = offer.Id,
                Round = round,
                Sequence = step.Sequence,
                ApproverUserId = step.ApproverUserId,
                Label = step.Label,
                Decision = ApprovalDecision.Waiting,
            });
        }

        offer.Status = OfferStatus.PendingApproval;
        offer.SubmittedAt = _clock.GetUtcNow();
        offer.ApprovedAt = null;

        await SaveAsync(ct);
        return await ReloadAsync(id, ct);
    }

    public async Task<OfferDetailDto?> DecideAsync(Guid id, ApprovalDecisionRequest request, CancellationToken ct = default)
    {
        var offer = await _db.Offers.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (offer is null || _user.UserId is not { } userId) return null;

        var live = ApprovalRail.LiveRound(await _db.OfferApprovals.Where(a => a.OfferId == id).ToListAsync(ct));

        // The caller must hold a waiting step BEFORE anything about the offer's state is reported —
        // otherwise the 409 below would tell an outsider the offer exists (ADR-0003).
        var mine = ApprovalRail.WaitingStepOf(live, userId);
        if (mine is null) return null;

        if (offer.Status != OfferStatus.PendingApproval)
            throw new InvalidOperationException($"This offer is {offer.Status}, not awaiting approval.");

        // Bound to the round the approver was shown: if the offer was revised and resubmitted since,
        // the terms on their screen are not the terms they would approve (security review, M3).
        if (request.Round != mine.Round)
            throw new InvalidOperationException("This offer was revised since you opened it. Reload it and review the current terms.");

        var now = _clock.GetUtcNow();
        switch (ApprovalRail.Apply(live, mine, userId, request.Approve, request.Comment, now, "This offer"))
        {
            // Back to the recruiter with the reason; the round stays on record beside the next one.
            // Never OfferStatus.Rejected — that is the candidate's answer (ADR-0030 §3).
            case ApprovalRail.Outcome.Rejected:
                offer.Status = OfferStatus.Draft;
                break;
            case ApprovalRail.Outcome.FullyApproved:
                offer.Status = OfferStatus.Approved;
                offer.ApprovedAt = now;
                break;
        }

        await SaveAsync(ct);
        return await ReloadAsync(id, ct);
    }

    public async Task<OfferDetailDto?> ReviseAsync(Guid id, CancellationToken ct = default)
    {
        if (await LoadAsync(id, Access.Manage, ct) is not { } loaded) return null;
        var offer = loaded.Offer;

        // Pulling an offer back from approval, or changing an approved one, voids the approval: the
        // next submission opens a fresh round. The old round stays as the record of what happened.
        if (offer.Status is not (OfferStatus.PendingApproval or OfferStatus.Approved))
            throw new InvalidOperationException($"Only an offer awaiting or holding approval can be revised; this one is {offer.Status}.");

        offer.Status = OfferStatus.Draft;
        offer.ApprovedAt = null;

        await SaveAsync(ct);
        return await ReloadAsync(id, ct);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        if (await LoadAsync(id, Access.Manage, ct) is not { } loaded) return false;
        var offer = loaded.Offer;

        // A draft nobody else has seen. Once an approver has been asked, the approval record stays.
        if (offer.Status != OfferStatus.Draft)
            throw new InvalidOperationException($"Only a Draft can be deleted; this offer is {offer.Status}.");
        if (await _db.OfferApprovals.AnyAsync(a => a.OfferId == id, ct))
            throw new InvalidOperationException("This offer has an approval history, so it stays on record.");

        _db.Offers.Remove(offer);
        await SaveAsync(ct);
        return true;
    }

    private static string? ConstraintOf(DbUpdateException ex) => (ex.InnerException as PostgresException)?.ConstraintName;

    /// <summary>Saves a change to an offer, turning "someone changed it meanwhile" into the user's
    /// message rather than a 500: the status token (an approval racing a revise, two submits), or the
    /// approval round's unique index (two submits opening the same round).</summary>
    private async Task SaveAsync(CancellationToken ct)
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

    // ── Templates and settings ──────────────────────────────────────────────

    public async Task<IReadOnlyList<OfferTemplateDto>> TemplatesAsync(CancellationToken ct = default)
    {
        var stored = await _db.OfferTemplates.AsNoTracking().OrderBy(t => t.Name)
            .Select(t => new OfferTemplateDto(t.Id, t.Name, t.Body, t.IsActive)).ToListAsync(ct);

        // The built-in default is always offered first, with no id: an offer started from it keeps
        // OfferTemplateId null.
        return stored.Prepend(new OfferTemplateDto(null, OfferLetter.DefaultTemplateName, OfferLetter.DefaultBody, true)).ToList();
    }

    public async Task<OfferTemplateDto> CreateTemplateAsync(SaveOfferTemplateRequest request, CancellationToken ct = default)
    {
        var template = new OfferTemplate { Name = request.Name.Trim(), Body = request.Body, IsActive = request.IsActive };
        _db.OfferTemplates.Add(template);
        await _db.SaveChangesAsync(ct);
        return new OfferTemplateDto(template.Id, template.Name, template.Body, template.IsActive);
    }

    public async Task<OfferTemplateDto?> UpdateTemplateAsync(Guid id, SaveOfferTemplateRequest request, CancellationToken ct = default)
    {
        var template = await _db.OfferTemplates.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (template is null) return null;

        // Offers already started from it keep their own copy of the letter.
        template.Name = request.Name.Trim();
        template.Body = request.Body;
        template.IsActive = request.IsActive;
        await _db.SaveChangesAsync(ct);
        return new OfferTemplateDto(template.Id, template.Name, template.Body, template.IsActive);
    }

    public async Task<OfferSettingsDto> SettingsAsync(CancellationToken ct = default)
    {
        var company = await _db.Companies.AsNoTracking().FirstAsync(c => c.Id == _tenant.TenantId, ct);
        return new OfferSettingsDto(company.RequireOfferApproval, company.ShowOfferSalaryToHiringManagers);
    }

    public async Task<OfferSettingsDto> UpdateSettingsAsync(UpdateOfferSettingsRequest request, CancellationToken ct = default)
    {
        var company = await _db.Companies.FirstAsync(c => c.Id == _tenant.TenantId, ct);
        var before = new OfferSettingsDto(company.RequireOfferApproval, company.ShowOfferSalaryToHiringManagers);

        // Validated present by [Required]; nullable only so that an omitted field is a 400.
        company.RequireOfferApproval = request.RequireOfferApproval!.Value;
        company.ShowOfferSalaryToHiringManagers = request.ShowOfferSalaryToHiringManagers!.Value;
        var after = new OfferSettingsDto(company.RequireOfferApproval, company.ShowOfferSalaryToHiringManagers);

        // Showing salaries to Hiring Managers is an access change, and dropping "approve every offer"
        // removes a financial control: both are answerable (ADR-0027).
        if (after != before)
        {
            var changes = new List<string>();
            if (after.ShowOfferSalaryToHiringManagers != before.ShowOfferSalaryToHiringManagers)
                changes.Add(after.ShowOfferSalaryToHiringManagers ? "Hiring Managers now see offered salaries" : "Hiring Managers no longer see offered salaries");
            if (after.RequireOfferApproval != before.RequireOfferApproval)
                changes.Add(after.RequireOfferApproval ? "every offer now needs approval" : "only offers over budget now need approval");
            await _audit.RecordAsync(AuditAction.OfferSettingsChanged, new AuditTarget("Company", company.Id, company.Name),
                string.Join("; ", changes) + ".", new { before, after }, ct: ct);
        }

        await _db.SaveChangesAsync(ct);
        return after;
    }
}
