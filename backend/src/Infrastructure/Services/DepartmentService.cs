using Microsoft.EntityFrameworkCore;
using RecruitOps.Application.Common;
using RecruitOps.Application.DTOs;
using RecruitOps.Application.Interfaces;
using RecruitOps.Domain.Entities;
using RecruitOps.Domain.Enums;
using RecruitOps.Infrastructure.Persistence;

namespace RecruitOps.Infrastructure.Services;

public class DepartmentService : IDepartmentService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IDepartmentAccess _access;
    private readonly TimeProvider _clock;

    public DepartmentService(
        AppDbContext db, ICurrentUser user, IDepartmentAccess access, TimeProvider clock)
    {
        _db = db;
        _user = user;
        _access = access;
        _clock = clock;
    }

    // ── Reads ────────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<DepartmentListItemDto>> GetDepartmentsAsync(CancellationToken ct = default)
    {
        var query = _db.Departments.AsNoTracking();

        // ADR-0003: explicit predicate, never a global filter. This list is the department
        // picker on the new-requisition form — offering a Hiring Manager a department the
        // API will then refuse is worse than not offering it.
        if (_user.IsDepartmentScoped)
        {
            var allowed = await _access.AccessibleDepartmentIdsAsync(ct);
            if (allowed.Count == 0) return Array.Empty<DepartmentListItemDto>();
            query = query.Where(d => allowed.Contains(d.Id));
        }

        // Inactive departments are hidden from the picker — nothing new should be raised in
        // one — but stay visible in the admin list below, where the point is to manage them.
        return await query
            .Where(d => d.IsActive)
            .OrderBy(d => d.Name)
            .Select(d => new DepartmentListItemDto(d.Id, d.Name, d.Code, d.IsActive))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<DepartmentDetailDto>> GetForAdminAsync(CancellationToken ct = default)
    {
        var departments = await _db.Departments.AsNoTracking()
            .OrderBy(d => d.Name)
            .ToListAsync(ct);

        if (departments.Count == 0) return Array.Empty<DepartmentDetailDto>();

        var ids = departments.Select(d => d.Id).ToList();
        var memberCounts = await CountMembersAsync(ids, ct);
        var openCounts = await CountOpenRequisitionsAsync(ids, ct);

        return departments.Select(d => new DepartmentDetailDto(
            d.Id, d.Name, d.Code, d.IsActive,
            memberCounts.GetValueOrDefault(d.Id),
            openCounts.GetValueOrDefault(d.Id)
        )).ToList();
    }

    // ── Mutations ────────────────────────────────────────────────────────────

    public async Task<DepartmentDetailDto> CreateAsync(
        CreateDepartmentRequest request, CancellationToken ct = default)
    {
        var name = request.Name.Trim();

        // The schema has a unique index on (TenantId, Name); checking first turns a
        // constraint violation into a message an admin can act on. The index is still the
        // authority — this check is a race away from being wrong, and that is fine.
        if (await _db.Departments.AnyAsync(d => d.Name == name, ct))
            throw new InvalidOperationException($"A department called '{name}' already exists.");

        var department = new Department
        {
            Name = name,
            Code = string.IsNullOrWhiteSpace(request.Code) ? null : request.Code.Trim(),
            IsActive = true,
        };

        _db.Departments.Add(department);
        await _db.SaveChangesAsync(ct);

        return new DepartmentDetailDto(department.Id, department.Name, department.Code, true, 0, 0);
    }

    public async Task<DepartmentDetailDto?> UpdateAsync(
        Guid id, UpdateDepartmentRequest request, CancellationToken ct = default)
    {
        var department = await _db.Departments.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (department is null) return null;

        var name = request.Name.Trim();
        if (await _db.Departments.AnyAsync(d => d.Name == name && d.Id != id, ct))
            throw new InvalidOperationException($"A department called '{name}' already exists.");

        department.Name = name;
        department.Code = string.IsNullOrWhiteSpace(request.Code) ? null : request.Code.Trim();
        department.UpdatedAt = _clock.GetUtcNow();

        await _db.SaveChangesAsync(ct);
        return await DetailAsync(department, ct);
    }

    public async Task<DepartmentDetailDto?> SetActiveAsync(
        Guid id, bool isActive, CancellationToken ct = default)
    {
        var department = await _db.Departments.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (department is null) return null;

        if (department.IsActive == isActive)
            throw new InvalidOperationException(
                isActive ? "This department is already active." : "This department is already inactive.");

        if (!isActive)
        {
            // Deactivating with work in flight would strand those requisitions: nobody can
            // finish an approval chain in a department that no longer accepts work, and the
            // requester would have no route forward. Better to say so than to half-do it.
            var open = await OpenRequisitionQuery(id).CountAsync(ct);
            if (open > 0)
                throw new InvalidOperationException(
                    $"This department still has {open} requisition(s) in progress. " +
                    "Approve, reject or cancel them first.");
        }

        department.IsActive = isActive;
        department.UpdatedAt = _clock.GetUtcNow();

        // Membership is deliberately left alone. Reactivating a department later should not
        // silently come back with nobody in it, and the member list is also a record of who
        // was responsible for what.
        await _db.SaveChangesAsync(ct);
        return await DetailAsync(department, ct);
    }

    // ── Membership (the ADR-0003 access-control axis) ─────────────────────────

    public async Task<IReadOnlyList<DepartmentMemberDto>?> GetMembersAsync(
        Guid id, CancellationToken ct = default)
    {
        if (!await _db.Departments.AnyAsync(d => d.Id == id, ct)) return null;
        return await RosterAsync(id, ct);
    }

    public async Task<IReadOnlyList<DepartmentMemberDto>?> SetMembersAsync(
        Guid id, SetDepartmentMembersRequest request, CancellationToken ct = default)
    {
        if (!await _db.Departments.AnyAsync(d => d.Id == id, ct)) return null;

        var requested = request.UserIds.Distinct().ToList();

        // Validate every id before writing anything. A silently-skipped unknown id here
        // means an admin believes they granted access that nobody has — the failure mode is
        // an invisible one, so it has to be loud.
        var known = await _db.Users
            .Where(u => requested.Contains(u.Id) && u.IsActive)
            .Select(u => u.Id)
            .ToListAsync(ct);

        var unknown = requested.Except(known).ToList();
        if (unknown.Count > 0)
            throw new InvalidOperationException(
                $"{unknown.Count} of the selected users do not exist or are inactive.");

        var existing = await _db.UserDepartments.Where(ud => ud.DepartmentId == id).ToListAsync(ct);

        // Rows for users who are staying are left untouched rather than deleted and
        // recreated — their CreatedAt is the record of when access was granted.
        var toRemove = existing.Where(ud => !known.Contains(ud.UserId)).ToList();
        var toAdd = known.Except(existing.Select(ud => ud.UserId)).ToList();

        _db.UserDepartments.RemoveRange(toRemove);
        foreach (var userId in toAdd)
            _db.UserDepartments.Add(new UserDepartment { UserId = userId, DepartmentId = id });

        await _db.SaveChangesAsync(ct);
        return await RosterAsync(id, ct);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Requisitions that are not yet finished. Approved counts as finished: the
    /// department's decision is made, and the posting lives on its own after that.</summary>
    private IQueryable<Requisition> OpenRequisitionQuery(Guid departmentId) =>
        _db.Requisitions.AsNoTracking().Where(r =>
            r.DepartmentId == departmentId &&
            (r.Status == RequisitionStatus.Draft || r.Status == RequisitionStatus.PendingApproval));

    private async Task<IReadOnlyList<DepartmentMemberDto>> RosterAsync(Guid id, CancellationToken ct)
    {
        var memberIds = await _db.UserDepartments.AsNoTracking()
            .Where(ud => ud.DepartmentId == id)
            .Select(ud => ud.UserId)
            .ToListAsync(ct);

        var users = await _db.Users.AsNoTracking()
            .Where(u => u.IsActive)
            .OrderBy(u => u.DisplayName)
            .ToListAsync(ct);

        // Projected in memory: Role.ToString() does not translate to SQL in EF Core 10.
        return users.Select(u => new DepartmentMemberDto(
            u.Id, u.DisplayName, u.Email, u.Role.ToString(), memberIds.Contains(u.Id)
        )).ToList();
    }

    private async Task<DepartmentDetailDto> DetailAsync(Department d, CancellationToken ct)
    {
        var members = await _db.UserDepartments.CountAsync(ud => ud.DepartmentId == d.Id, ct);
        var open = await OpenRequisitionQuery(d.Id).CountAsync(ct);
        return new DepartmentDetailDto(d.Id, d.Name, d.Code, d.IsActive, members, open);
    }

    private async Task<Dictionary<Guid, int>> CountMembersAsync(List<Guid> ids, CancellationToken ct)
    {
        var rows = await _db.UserDepartments.AsNoTracking()
            .Where(ud => ids.Contains(ud.DepartmentId))
            .Select(ud => ud.DepartmentId)
            .ToListAsync(ct);
        return rows.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
    }

    private async Task<Dictionary<Guid, int>> CountOpenRequisitionsAsync(List<Guid> ids, CancellationToken ct)
    {
        var rows = await _db.Requisitions.AsNoTracking()
            .Where(r => ids.Contains(r.DepartmentId) &&
                        (r.Status == RequisitionStatus.Draft || r.Status == RequisitionStatus.PendingApproval))
            .Select(r => r.DepartmentId)
            .ToListAsync(ct);
        return rows.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
    }
}
