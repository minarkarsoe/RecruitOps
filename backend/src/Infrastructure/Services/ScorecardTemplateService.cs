using Microsoft.EntityFrameworkCore;
using RecruitOps.Application.Common;
using RecruitOps.Application.DTOs;
using RecruitOps.Application.Interfaces;
using RecruitOps.Domain.Entities;
using RecruitOps.Domain.Enums;
using RecruitOps.Infrastructure.Persistence;

namespace RecruitOps.Infrastructure.Services;

/// <summary>Module 3.3 configuration — criteria sets, and resolving which one applies.</summary>
public class ScorecardTemplateService : IScorecardTemplateService
{
    private readonly AppDbContext _db;
    private readonly IDepartmentAccess _departments;

    public ScorecardTemplateService(AppDbContext db, IDepartmentAccess departments)
    {
        _db = db;
        _departments = departments;
    }

    public async Task<IReadOnlyList<ScorecardTemplateDto>> ListAsync(CancellationToken ct = default)
    {
        var templates = await _db.ScorecardTemplates.AsNoTracking()
            .OrderBy(t => t.Name)
            .ToListAsync(ct);

        return await MapManyAsync(templates, ct);
    }

    public async Task<ScorecardTemplateDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var template = await _db.ScorecardTemplates.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, ct);

        return template is null ? null : (await MapManyAsync(new[] { template }, ct)).Single();
    }

    public async Task<ScorecardTemplateDto> CreateAsync(
        SaveScorecardTemplateRequest request, CancellationToken ct = default)
    {
        await ValidateAsync(request, ct);

        var template = new ScorecardTemplate
        {
            Name = request.Name.Trim(),
            Description = request.Description,
            DepartmentId = request.DepartmentId,
            JobPostingId = request.JobPostingId,
            IsActive = request.IsActive,
        };
        _db.ScorecardTemplates.Add(template);

        AddCriteria(template, request);

        await _db.SaveChangesAsync(ct);
        return (await GetAsync(template.Id, ct))!;
    }

    public async Task<ScorecardTemplateDto?> UpdateAsync(
        Guid id, SaveScorecardTemplateRequest request, CancellationToken ct = default)
    {
        var template = await _db.ScorecardTemplates.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (template is null) return null;

        await ValidateAsync(request, ct, excludeTemplateId: id);

        template.Name = request.Name.Trim();
        template.Description = request.Description;
        template.DepartmentId = request.DepartmentId;
        template.JobPostingId = request.JobPostingId;
        template.IsActive = request.IsActive;

        // Criteria are replaced wholesale. Safe because a submitted ScorecardResponse
        // snapshots the label and type it was answered against (ADR-0017 §2) — the old
        // evaluations stay readable on their own terms even though the criteria rows go.
        var old = await _db.ScorecardCriteria.Where(c => c.ScorecardTemplateId == id).ToListAsync(ct);
        _db.ScorecardCriteria.RemoveRange(old);

        AddCriteria(template, request);

        await _db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<ScorecardTemplateDto?> ResolveForPostingAsync(
        Guid jobPostingId, CancellationToken ct = default)
    {
        var departmentId = await _db.JobPostings.AsNoTracking()
            .Where(p => p.Id == jobPostingId)
            .Select(p => (Guid?)p.DepartmentId)
            .FirstOrDefaultAsync(ct);

        if (departmentId is null) return null;

        // Most specific wins (ADR-0017 §1). Fetched as three cheap lookups rather than one
        // clever ordered query: the precedence is the rule people will come here to read,
        // and it should be legible without reconstructing a CASE expression in their head.
        var posting = await _db.ScorecardTemplates.AsNoTracking()
            .FirstOrDefaultAsync(t => t.IsActive && t.JobPostingId == jobPostingId, ct);
        if (posting is not null) return (await MapManyAsync(new[] { posting }, ct)).Single();

        var department = await _db.ScorecardTemplates.AsNoTracking()
            .FirstOrDefaultAsync(t => t.IsActive && t.DepartmentId == departmentId.Value, ct);
        if (department is not null) return (await MapManyAsync(new[] { department }, ct)).Single();

        var companyWide = await _db.ScorecardTemplates.AsNoTracking()
            .FirstOrDefaultAsync(
                t => t.IsActive && t.DepartmentId == null && t.JobPostingId == null, ct);

        return companyWide is null
            ? null
            : (await MapManyAsync(new[] { companyWide }, ct)).Single();
    }

    // ---------- helpers ----------

    private async Task ValidateAsync(
        SaveScorecardTemplateRequest request, CancellationToken ct, Guid? excludeTemplateId = null)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new InvalidOperationException("A template needs a name.");

        // The two scopes are alternatives, not a hierarchy to fill in. Allowing both would
        // make "posting X in department Y" a third, ambiguous scope with no defined
        // precedence, and the resolution order above would quietly pick one.
        if (request.DepartmentId is not null && request.JobPostingId is not null)
            throw new InvalidOperationException(
                "A template belongs to a department or a single posting, not both. "
                + "Leave both empty for the company-wide default.");

        if (request.Criteria.Count == 0)
            throw new InvalidOperationException("A template needs at least one criterion.");

        foreach (var criterion in request.Criteria)
        {
            if (string.IsNullOrWhiteSpace(criterion.Label))
                throw new InvalidOperationException("Every criterion needs a label.");

            if (!Enum.TryParse<CriterionType>(criterion.Type, ignoreCase: false, out _))
                throw new InvalidOperationException(
                    $"'{criterion.Type}' is not a criterion type.");
        }

        if (request.DepartmentId is not null)
        {
            var exists = await _db.Departments.AsNoTracking()
                .AnyAsync(d => d.Id == request.DepartmentId.Value && d.IsActive, ct);
            if (!exists)
                throw new InvalidOperationException("That department does not exist or is inactive.");

            // Authoring a template for a department you cannot see is authoring for someone
            // else's hiring process. Checked here rather than only at the controller,
            // because the controller allows recruitment staff across all departments and
            // this method is also reachable by a future admin path.
            if (!await _departments.CanAccessAsync(request.DepartmentId.Value, ct))
                throw new InvalidOperationException("That department is outside your access.");
        }

        if (request.JobPostingId is not null)
        {
            var posting = await _db.JobPostings.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == request.JobPostingId.Value, ct);
            if (posting is null)
                throw new InvalidOperationException("That job posting does not exist.");

            if (!await _departments.CanAccessAsync(posting.DepartmentId, ct))
                throw new InvalidOperationException("That posting is outside your access.");
        }

        // One active template per scope, or resolution becomes "whichever row came back
        // first" — which is stable in testing and arbitrary in production.
        if (request.IsActive)
        {
            var clash = await _db.ScorecardTemplates.AsNoTracking()
                .Where(t => t.IsActive
                            && t.DepartmentId == request.DepartmentId
                            && t.JobPostingId == request.JobPostingId
                            && (excludeTemplateId == null || t.Id != excludeTemplateId.Value))
                .AnyAsync(ct);

            if (clash)
                throw new InvalidOperationException(
                    "There is already an active template for that scope. Deactivate it first.");
        }
    }

    private void AddCriteria(ScorecardTemplate template, SaveScorecardTemplateRequest request)
    {
        // Sequence derived from list order, so gaps and duplicates are unrepresentable —
        // the same trick ApprovalChainStep uses.
        var sequence = 1;
        foreach (var input in request.Criteria)
        {
            _db.ScorecardCriteria.Add(new ScorecardCriterion
            {
                TenantId = template.TenantId,
                ScorecardTemplateId = template.Id,
                Sequence = sequence++,
                Label = input.Label.Trim(),
                Guidance = input.Guidance,
                Type = Enum.Parse<CriterionType>(input.Type),
                IsRequired = input.IsRequired,
            });
        }
    }

    private async Task<IReadOnlyList<ScorecardTemplateDto>> MapManyAsync(
        IReadOnlyCollection<ScorecardTemplate> templates, CancellationToken ct)
    {
        if (templates.Count == 0) return Array.Empty<ScorecardTemplateDto>();

        var ids = templates.Select(t => t.Id).ToList();

        var criteria = await _db.ScorecardCriteria.AsNoTracking()
            .Where(c => ids.Contains(c.ScorecardTemplateId))
            .OrderBy(c => c.Sequence)
            .ToListAsync(ct);

        var departmentIds = templates
            .Where(t => t.DepartmentId is not null)
            .Select(t => t.DepartmentId!.Value)
            .Distinct()
            .ToList();

        var departmentNames = await _db.Departments.AsNoTracking()
            .Where(d => departmentIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, d => d.Name, ct);

        return templates.Select(t => new ScorecardTemplateDto(
            t.Id,
            t.Name,
            t.Description,
            t.DepartmentId,
            t.DepartmentId is null ? null : departmentNames.GetValueOrDefault(t.DepartmentId.Value),
            t.JobPostingId,
            t.IsActive,
            criteria
                .Where(c => c.ScorecardTemplateId == t.Id)
                .Select(c => new ScorecardCriterionDto(
                    c.Id, c.Sequence, c.Label, c.Guidance, c.Type.ToString(), c.IsRequired))
                .ToList()
        )).ToList();
    }
}
