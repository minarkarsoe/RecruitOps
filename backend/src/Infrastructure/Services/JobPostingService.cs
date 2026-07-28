using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using RecruitOps.Application.Common;
using RecruitOps.Application.DTOs;
using RecruitOps.Application.Interfaces;
using RecruitOps.Domain;
using RecruitOps.Domain.Entities;
using RecruitOps.Domain.Enums;
using RecruitOps.Infrastructure.Persistence;

namespace RecruitOps.Infrastructure.Services;

public class JobPostingService : IJobPostingService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IDepartmentAccess _access;
    private readonly TimeProvider _clock;

    public JobPostingService(
        AppDbContext db, ICurrentUser user, IDepartmentAccess access, TimeProvider clock)
    {
        _db = db;
        _user = user;
        _access = access;
        _clock = clock;
    }

    // ── Reads ────────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<JobPostingListItemDto>> GetPostingsAsync(CancellationToken ct = default)
    {
        var query = _db.JobPostings.AsNoTracking();

        // ADR-0003: explicit predicate, never a global filter.
        if (_user.IsDepartmentScoped)
        {
            var allowed = await _access.AccessibleDepartmentIdsAsync(ct);
            if (allowed.Count == 0) return Array.Empty<JobPostingListItemDto>();
            query = query.Where(p => allowed.Contains(p.DepartmentId));
        }

        var rows = await (
            from p in query
            join d in _db.Departments.AsNoTracking() on p.DepartmentId equals d.Id
            orderby p.CreatedAt descending
            select new { p, DepartmentName = d.Name }
        ).ToListAsync(ct);

        if (rows.Count == 0) return Array.Empty<JobPostingListItemDto>();

        var ids = rows.Select(x => x.p.Id).ToList();
        var tokens = await TokensForAsync(ids, ct);
        var counts = await CountsForAsync(ids, ct);

        return rows.Select(x => new JobPostingListItemDto(
            x.p.Id,
            x.p.DepartmentId,
            x.DepartmentName,
            x.p.RequisitionId,
            x.p.Title,
            x.p.Status.ToString(),
            x.p.EmploymentType.ToString(),
            x.p.Location,
            x.p.Headcount,
            x.p.PostedAt,
            x.p.ClosedAt,
            tokens.GetValueOrDefault(x.p.Id),
            counts.GetValueOrDefault(x.p.Id)
        )).ToList();
    }

    public async Task<JobPostingDetailDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var posting = await _db.JobPostings.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
        if (posting is null) return null;
        if (!await _access.CanAccessAsync(posting.DepartmentId, ct)) return null;

        return await BuildDetailAsync(posting, ct);
    }

    // ── Mutations ────────────────────────────────────────────────────────────

    public async Task<JobPostingDetailDto?> CreateFromRequisitionAsync(
        CreateJobPostingRequest request, CancellationToken ct = default)
    {
        var requisition = await _db.Requisitions.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == request.RequisitionId, ct);

        if (requisition is null) return null;
        if (!await _access.CanAccessAsync(requisition.DepartmentId, ct)) return null;

        // The governing rule of the whole product: nothing is advertised that the business
        // has not approved. Enforced here as well as by the schema's unique index, so the
        // caller gets a comprehensible 409 instead of a constraint violation.
        if (requisition.Status != RequisitionStatus.Approved)
            throw new InvalidOperationException(
                $"Only an Approved requisition can be published; this one is {requisition.Status}.");

        if (await _db.JobPostings.AnyAsync(p => p.RequisitionId == requisition.Id, ct))
            throw new InvalidOperationException("This requisition already has a job posting.");

        var posting = new JobPosting
        {
            TenantId = requisition.TenantId,
            DepartmentId = requisition.DepartmentId,
            RequisitionId = requisition.Id,
            Status = JobStatus.Draft,
            // Copied, not referenced: the recruiter will rewrite an internal JD into
            // candidate-facing copy, and that must not alter what approvers signed off on.
            Title = requisition.Title,
            Description = requisition.JobDescription,
            Headcount = requisition.Headcount,
            SalaryMin = requisition.SalaryBudget,
            SalaryMax = requisition.SalaryBudget,
            ShowSalary = false,
        };

        _db.JobPostings.Add(posting);
        await _db.SaveChangesAsync(ct);

        return await GetByIdAsync(posting.Id, ct);
    }

    public async Task<JobPostingDetailDto?> UpdateAsync(
        Guid id, UpdateJobPostingRequest request, CancellationToken ct = default)
    {
        var posting = await _db.JobPostings.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (posting is null) return null;
        if (!await _access.CanAccessAsync(posting.DepartmentId, ct)) return null;

        // Closed is terminal. Editing a closed advert would change what applicants who
        // already applied were shown, with no way to tell them.
        if (posting.Status == JobStatus.Closed)
            throw new InvalidOperationException("A closed posting cannot be edited. Raise a new requisition.");

        if (!Enum.TryParse<EmploymentType>(request.EmploymentType, ignoreCase: false, out var employmentType))
            throw new InvalidOperationException($"'{request.EmploymentType}' is not a valid employment type.");

        if (request.SalaryMin is not null && request.SalaryMax is not null && request.SalaryMin > request.SalaryMax)
            throw new InvalidOperationException("The minimum salary cannot be above the maximum.");

        // Validate the form schema on the way IN, not when an applicant meets it. A broken
        // schema saved here would only surface as a failure on the public page, to a
        // stranger, with nobody watching.
        if (!ApplicationFormSchema.TryParse(request.ApplicationFormFieldsJson, out _, out var schemaError))
            throw new InvalidOperationException(schemaError!);

        posting.Title = request.Title;
        posting.Description = request.Description;
        posting.Location = request.Location;
        posting.EmploymentType = employmentType;
        posting.Headcount = request.Headcount;
        posting.SalaryMin = request.SalaryMin;
        posting.SalaryMax = request.SalaryMax;
        posting.ShowSalary = request.ShowSalary;
        posting.ApplicationFormFieldsJson = request.ApplicationFormFieldsJson;
        posting.UpdatedAt = _clock.GetUtcNow();

        await _db.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    public async Task<JobPostingDetailDto?> PublishAsync(Guid id, CancellationToken ct = default)
    {
        var posting = await _db.JobPostings.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (posting is null) return null;
        if (!await _access.CanAccessAsync(posting.DepartmentId, ct)) return null;

        if (posting.Status != JobStatus.Draft)
            throw new InvalidOperationException($"Only a Draft can be published; this one is {posting.Status}.");

        posting.Status = JobStatus.Live;
        posting.PostedAt = _clock.GetUtcNow();
        posting.UpdatedAt = posting.PostedAt.Value;

        // Mint the link once and keep it. Re-issuing on every publish would break every
        // share already posted to Facebook or sent to a candidate.
        var link = await _db.PortalLinks.FirstOrDefaultAsync(l => l.JobPostingId == posting.Id, ct);
        if (link is null)
        {
            _db.PortalLinks.Add(new PortalLink
            {
                TenantId = posting.TenantId,
                JobPostingId = posting.Id,
                Token = NewToken(),
                IsRevoked = false,
            });
        }
        else
        {
            // Re-publishing a posting whose link was revoked should work again.
            link.IsRevoked = false;
        }

        await _db.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    public async Task<JobPostingDetailDto?> CloseAsync(Guid id, CancellationToken ct = default)
    {
        var posting = await _db.JobPostings.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (posting is null) return null;
        if (!await _access.CanAccessAsync(posting.DepartmentId, ct)) return null;

        if (posting.Status == JobStatus.Closed)
            throw new InvalidOperationException("This posting is already closed.");

        posting.Status = JobStatus.Closed;
        posting.ClosedAt = _clock.GetUtcNow();
        posting.UpdatedAt = posting.ClosedAt.Value;

        // Applications already received are left exactly as they are — closing the advert
        // stops new candidates arriving, it does not reject the ones who did.
        await _db.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>256 bits from a CSPRNG, URL-safe. This token is the only thing protecting a
    /// page; a sequential id or a Guid.NewGuid() would be enumerable or predictable.</summary>
    private static string NewToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    private async Task<Dictionary<Guid, string>> TokensForAsync(List<Guid> postingIds, CancellationToken ct)
    {
        var rows = await _db.PortalLinks.AsNoTracking()
            .Where(l => postingIds.Contains(l.JobPostingId) && !l.IsRevoked)
            .Select(l => new { l.JobPostingId, l.Token })
            .ToListAsync(ct);

        return rows
            .GroupBy(l => l.JobPostingId)
            .ToDictionary(g => g.Key, g => g.First().Token);
    }

    private async Task<Dictionary<Guid, int>> CountsForAsync(List<Guid> postingIds, CancellationToken ct)
    {
        var rows = await _db.JobApplications.AsNoTracking()
            .Where(a => postingIds.Contains(a.JobPostingId))
            .Select(a => a.JobPostingId)
            .ToListAsync(ct);

        return rows.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
    }

    private async Task<JobPostingDetailDto> BuildDetailAsync(JobPosting p, CancellationToken ct)
    {
        var dept = await _db.Departments.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == p.DepartmentId, ct);

        var token = await _db.PortalLinks.AsNoTracking()
            .Where(l => l.JobPostingId == p.Id && !l.IsRevoked)
            .Select(l => l.Token)
            .FirstOrDefaultAsync(ct);

        var count = await _db.JobApplications.AsNoTracking()
            .CountAsync(a => a.JobPostingId == p.Id, ct);

        return new JobPostingDetailDto(
            p.Id,
            p.DepartmentId,
            dept?.Name ?? string.Empty,
            p.RequisitionId,
            p.Title,
            p.Description,
            p.Status.ToString(),
            p.EmploymentType.ToString(),
            p.Location,
            p.Headcount,
            p.SalaryMin,
            p.SalaryMax,
            p.ShowSalary,
            p.ApplicationFormFieldsJson,
            p.PostedAt,
            p.ClosedAt,
            token,
            count);
    }
}
