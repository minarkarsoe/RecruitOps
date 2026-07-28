using Microsoft.EntityFrameworkCore;
using RecruitOps.Application.Common;
using RecruitOps.Application.DTOs;
using RecruitOps.Application.Interfaces;
using RecruitOps.Domain.Entities;
using RecruitOps.Infrastructure.Persistence;

namespace RecruitOps.Infrastructure.Services;

public class JdTemplateService : IJdTemplateService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IDepartmentAccess _access;

    public JdTemplateService(AppDbContext db, ICurrentUser user, IDepartmentAccess access)
    {
        _db = db;
        _user = user;
        _access = access;
    }

    public async Task<IReadOnlyList<JdTemplateDto>> GetTemplatesAsync(CancellationToken ct = default)
    {
        var query = _db.JdTemplates.AsNoTracking().Where(t => t.IsActive);

        // Department-scoped users still see company-wide templates (DepartmentId null).
        if (_user.IsDepartmentScoped)
        {
            var allowed = await _access.AccessibleDepartmentIdsAsync(ct);
            query = query.Where(t => t.DepartmentId == null || allowed.Contains(t.DepartmentId.Value));
        }

        return await query
            .OrderBy(t => t.Title)
            .Select(t => new JdTemplateDto(t.Id, t.Title, t.Content, t.DepartmentId, t.IsActive))
            .ToListAsync(ct);
    }

    public async Task<JdTemplateDto?> CreateAsync(CreateJdTemplateRequest request, CancellationToken ct = default)
    {
        if (request.DepartmentId is Guid deptId)
        {
            if (!await _db.Departments.AnyAsync(d => d.Id == deptId, ct)) return null;
            if (!await _access.CanAccessAsync(deptId, ct)) return null;
        }

        var template = new JdTemplate
        {
            Title = request.Title,
            Content = request.Content,
            DepartmentId = request.DepartmentId,
            IsActive = true,
        };

        _db.JdTemplates.Add(template);
        await _db.SaveChangesAsync(ct);

        return new JdTemplateDto(template.Id, template.Title, template.Content,
                                 template.DepartmentId, template.IsActive);
    }
}
