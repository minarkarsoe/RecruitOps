using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecruitOps.Application.Common;
using RecruitOps.Infrastructure.Persistence;

namespace RecruitOps.Api.Controllers;

/// <summary>The companies a super-admin may switch between (`X-Tenant-Id`).
///
/// <para><b>Super-admins only, checked here rather than by a policy.</b> There is no
/// <c>SuperAdminOnly</c> policy today — <c>Roles.SuperAdmin</c> is not among the role constants
/// and the permission handler treats super-admin as a bypass rather than a role to require. So
/// the check is explicit, from the same <see cref="ICurrentUser.IsSuperAdmin"/> that
/// <c>CurrentTenant</c> uses to decide whether the header is honoured at all: one predicate, two
/// callers, no chance of the list and the switch disagreeing about who may use them.</para>
///
/// <para><b>404, not 403.</b> To anyone who is not a super-admin this endpoint does not exist —
/// a 403 would confirm that a company list is there to be read.</para>
///
/// <para>⚠️ Per ADR-0004 the normal deployment is <b>one company per database</b>, so this usually
/// returns a single row and the switcher is a dormant capability. It is not dead code: the tenant
/// filters exist for the shared-instance case, and this is what makes them steerable when it
/// happens.</para>
/// </summary>
[ApiController]
[Route("api/tenants")]
[Authorize]
public class TenantsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _user;

    public TenantsController(AppDbContext db, ICurrentUser user)
    {
        _db = db;
        _user = user;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TenantInfoDto>>> Get(CancellationToken ct)
    {
        if (!_user.IsSuperAdmin) return NotFound();

        // Companies carry no tenant filter (one row per deployment), so this reads every company
        // in the database regardless of which one the caller is currently viewing — which is the
        // whole point of a switcher.
        var companies = await _db.Companies.AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new TenantInfoDto(c.Id, c.Name, c.Slug, c.IsActive))
            .ToListAsync(ct);

        return Ok(companies);
    }
}

/// <summary>One switchable company. Deliberately thin — a switcher needs a label and an id, and
/// nothing else about a company is any of the switcher's business.</summary>
public sealed record TenantInfoDto(Guid Id, string Name, string Code, bool IsActive);
