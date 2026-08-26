using System.Security.Claims;
using RecruitOps.Application.Common;

namespace RecruitOps.Api.Auth;

/// <summary>Resolves the current tenant from the authenticated principal's
/// <see cref="AppClaims.TenantId"/> claim, falling back to an ambient tenant when there is no
/// request at all. Consumed by AppDbContext's global query filters to enforce isolation (Module 1).
///
/// <para><b>Order is the security property, not a detail (ADR-0026 §4).</b> The request claim is
/// read first and wins whenever it is present, so nothing reachable from an authenticated request
/// can redirect that request at another tenant — resolving <see cref="IAmbientTenantScope"/> and
/// entering a tenant mid-request is inert. The ambient value is consulted only in the case it
/// exists for: a background worker, which has no <c>HttpContext</c> and would otherwise see
/// <see cref="Guid.Empty"/> and read nothing.</para>
///
/// <para>If these two are ever reordered, every authenticated request becomes redirectable.
/// <c>AmbientTenantScopeTests</c> asserts the order; treat a failure there as a security finding
/// rather than a test to update.</para></summary>
public sealed class CurrentTenant : ICurrentTenant
{
    private readonly IHttpContextAccessor _http;
    private readonly IAmbientTenantScope _ambient;

    public CurrentTenant(IHttpContextAccessor http, IAmbientTenantScope ambient)
    {
        _http = http;
        _ambient = ambient;
    }

    public Guid TenantId
    {
        get
        {
            // 1. The request's own claim, and it is final when present.
            var value = _http.HttpContext?.User.FindFirstValue(AppClaims.TenantId);
            if (Guid.TryParse(value, out var fromRequest))
            {
                return fromRequest;
            }

            // 2. No request: a background scope may have entered one.
            return _ambient.TenantId ?? Guid.Empty;
        }
    }
}
