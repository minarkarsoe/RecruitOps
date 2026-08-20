using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using RecruitOps.Api.Auth;
using RecruitOps.Infrastructure.Tenancy;
using Xunit;

namespace RecruitOps.Api.Tests;

/// <summary>Tenant resolution order (ADR-0026 §4).
///
/// <para>ADR-0026 made <c>ICurrentTenant</c> settable so a background worker can establish a
/// tenant with no HTTP request behind it. That is only safe because of the order
/// <see cref="CurrentTenant"/> reads its two sources in: <b>the request claim first, and it wins
/// whenever it is present.</b></para>
///
/// <para>Reverse those two lines and every authenticated request becomes redirectable at another
/// company's data by anything that can reach the ambient scope. <b>A failure in this file is a
/// security finding, not a test to update.</b></para>
/// </summary>
public class CurrentTenantResolutionTests
{
    private static IHttpContextAccessor AccessorWithTenant(Guid tenantId)
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(AppClaims.TenantId, tenantId.ToString()) },
                authenticationType: "Test")),
        };
        return new HttpContextAccessor { HttpContext = context };
    }

    private static IHttpContextAccessor AccessorWithNoRequest() => new HttpContextAccessor();

    [Fact]
    public void Uses_The_Request_Claim_When_There_Is_A_Request()
    {
        var tenant = Guid.NewGuid();
        var subject = new CurrentTenant(AccessorWithTenant(tenant), new AmbientTenantScope());

        Assert.Equal(tenant, subject.TenantId);
    }

    /// <summary>The security property, stated directly: entering an ambient tenant during a
    /// request changes nothing.</summary>
    [Fact]
    public void The_Request_Claim_Beats_An_Ambient_Tenant()
    {
        var requestTenant = Guid.NewGuid();
        var otherTenant = Guid.NewGuid();

        var ambient = new AmbientTenantScope();
        ambient.EnterTenant(otherTenant);

        var subject = new CurrentTenant(AccessorWithTenant(requestTenant), ambient);

        Assert.Equal(requestTenant, subject.TenantId);
        Assert.NotEqual(otherTenant, subject.TenantId);
    }

    /// <summary>The case the seam exists for: no request at all, so the ambient tenant is the only
    /// source and the worker's handler can query normally.</summary>
    [Fact]
    public void Falls_Back_To_The_Ambient_Tenant_When_There_Is_No_Request()
    {
        var workerTenant = Guid.NewGuid();
        var ambient = new AmbientTenantScope();
        ambient.EnterTenant(workerTenant);

        var subject = new CurrentTenant(AccessorWithNoRequest(), ambient);

        Assert.Equal(workerTenant, subject.TenantId);
    }

    /// <summary>No request and no ambient tenant is the pre-ADR-0026 behaviour, and it must not
    /// have changed: Guid.Empty, which the query filters then match nothing against. That is what
    /// makes an un-scoped background read return nothing instead of everything.</summary>
    [Fact]
    public void No_Request_And_No_Ambient_Tenant_Is_Still_Empty()
    {
        var subject = new CurrentTenant(AccessorWithNoRequest(), new AmbientTenantScope());

        Assert.Equal(Guid.Empty, subject.TenantId);
    }

    /// <summary>An unauthenticated request — the public job pages — must also stay as it was.
    /// <c>PublicJobService</c> depends on getting Guid.Empty here and re-applying the tenant from
    /// the link token itself.</summary>
    [Fact]
    public void An_Anonymous_Request_Is_Still_Empty()
    {
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        var subject = new CurrentTenant(accessor, new AmbientTenantScope());

        Assert.Equal(Guid.Empty, subject.TenantId);
    }
}
