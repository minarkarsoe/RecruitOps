using Microsoft.EntityFrameworkCore;
using RecruitOps.Application.Interfaces;
using RecruitOps.Infrastructure.Persistence;

namespace RecruitOps.Infrastructure.Services;

/// <summary>One primary-key read per authenticated request, uncached on purpose: a cache would
/// reintroduce exactly the window this exists to close, and a per-company install's request rate
/// does not need one (ADR-0004). Revisit with an evicting cache if that ever changes.</summary>
public sealed class UserSessionValidator : IUserSessionValidator
{
    private readonly AppDbContext _db;

    public UserSessionValidator(AppDbContext db) => _db = db;

    public async Task<bool> IsCurrentAsync(
        Guid userId, Guid tenantId, string? roleClaim, bool superAdminClaim, CancellationToken ct = default)
    {
        // No tenant claim exists in the query filter's context yet — the principal is still being
        // built — so the tenant is matched explicitly instead.
        var user = await _db.Users
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(u => u.Id == userId && u.TenantId == tenantId)
            .Select(u => new { u.IsActive, u.Role, u.IsSuperAdmin })
            .FirstOrDefaultAsync(ct);

        return user is not null
            && user.IsActive
            && string.Equals(user.Role.ToString(), roleClaim, StringComparison.Ordinal)
            && user.IsSuperAdmin == superAdminClaim;
    }
}
