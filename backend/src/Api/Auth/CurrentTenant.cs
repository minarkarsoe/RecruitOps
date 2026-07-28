using System.Security.Claims;
using RecruitOps.Application.Common;

namespace RecruitOps.Api.Auth;

/// <summary>Resolves the current tenant from the authenticated principal's
/// <see cref="AppClaims.TenantId"/> claim. Consumed by AppDbContext's global
/// query filters to enforce isolation (Module 1).</summary>
public sealed class CurrentTenant : ICurrentTenant
{
    private readonly IHttpContextAccessor _http;

    public CurrentTenant(IHttpContextAccessor http) => _http = http;

    public Guid TenantId
    {
        get
        {
            var value = _http.HttpContext?.User.FindFirstValue(AppClaims.TenantId);
            return Guid.TryParse(value, out var id) ? id : Guid.Empty;
        }
    }
}
