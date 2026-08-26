using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using RecruitOps.Api.Auth;
using RecruitOps.Application.Common;
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
///
/// <para><b>2026-08-26 — a third source was added above both: the <c>X-Tenant-Id</c> header, for
/// super-admins only.</b> That is a deliberate hole in the sentence above, and the whole of its
/// safety is the <c>is_super_admin</c> check on the signed token. The tests below therefore pin
/// two things with equal weight: that a super-admin CAN steer a request, and that nobody
/// else can.</para>
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

    /// <summary>A request carrying a tenant claim, optionally the super-admin flag, and
    /// optionally an X-Tenant-Id override header.</summary>
    private static IHttpContextAccessor Accessor(
        Guid claimTenant, bool superAdmin = false, string? overrideHeader = null)
    {
        var claims = new List<Claim> { new(AppClaims.TenantId, claimTenant.ToString()) };
        if (superAdmin) claims.Add(new Claim(AppClaims.IsSuperAdmin, "true"));

        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Test")),
        };

        if (overrideHeader is not null)
            context.Request.Headers[CurrentTenant.TenantOverrideHeader] = overrideHeader;

        return new HttpContextAccessor { HttpContext = context };
    }

    private static CurrentTenant Subject(IHttpContextAccessor accessor, IAmbientTenantScope? ambient = null)
        => new(accessor, ambient ?? new AmbientTenantScope());

    [Fact]
    public void Uses_The_Request_Claim_When_There_Is_A_Request()
    {
        var tenant = Guid.NewGuid();
        var subject = Subject(AccessorWithTenant(tenant));

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

        var subject = Subject(AccessorWithTenant(requestTenant), ambient);

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

        var subject = Subject(AccessorWithNoRequest(), ambient);

        Assert.Equal(workerTenant, subject.TenantId);
    }

    /// <summary>No request and no ambient tenant is the pre-ADR-0026 behaviour, and it must not
    /// have changed: Guid.Empty, which the query filters then match nothing against. That is what
    /// makes an un-scoped background read return nothing instead of everything.</summary>
    [Fact]
    public void No_Request_And_No_Ambient_Tenant_Is_Still_Empty()
    {
        var subject = Subject(AccessorWithNoRequest());

        Assert.Equal(Guid.Empty, subject.TenantId);
    }

    /// <summary>An unauthenticated request — the public job pages — must also stay as it was.
    /// <c>PublicJobService</c> depends on getting Guid.Empty here and re-applying the tenant from
    /// the link token itself.</summary>
    [Fact]
    public void An_Anonymous_Request_Is_Still_Empty()
    {
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        var subject = Subject(accessor);

        Assert.Equal(Guid.Empty, subject.TenantId);
    }

    // ------------------------------------------------------------------ X-Tenant-Id override

    /// <summary>The capability, stated plainly: a super-admin steers the request with a header.</summary>
    [Fact]
    public void A_Super_Admin_Can_Steer_The_Request_With_The_Header()
    {
        var own = Guid.NewGuid();
        var other = Guid.NewGuid();

        var subject = Subject(Accessor(own, superAdmin: true, overrideHeader: other.ToString()));

        Assert.Equal(other, subject.TenantId);
    }

    /// <summary>⚠️ <b>The one that matters.</b> Same header, same value, no super-admin claim —
    /// and the request stays on the caller's own tenant. If this ever fails, every authenticated
    /// user can read every company's data by setting one header.</summary>
    [Fact]
    public void An_Ordinary_User_Sending_The_Header_Is_Not_Steered()
    {
        var own = Guid.NewGuid();
        var other = Guid.NewGuid();

        var subject = Subject(Accessor(own, superAdmin: false, overrideHeader: other.ToString()));

        Assert.Equal(own, subject.TenantId);
        Assert.NotEqual(other, subject.TenantId);
    }

    /// <summary>The role spelling of the same claim. `ICurrentUser.IsSuperAdmin` accepts either
    /// the flag or the SuperAdmin role, and this asserts the header follows that one predicate
    /// rather than a second copy of it living in CurrentTenant.</summary>
    [Fact]
    public void The_SuperAdmin_Role_Claim_Also_Opens_The_Header()
    {
        var own = Guid.NewGuid();
        var other = Guid.NewGuid();

        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[]
                {
                    new Claim(AppClaims.TenantId, own.ToString()),
                    new Claim(ClaimTypes.Role, "SuperAdmin"),
                },
                authenticationType: "Test")),
        };
        context.Request.Headers[CurrentTenant.TenantOverrideHeader] = other.ToString();
        var accessor = new HttpContextAccessor { HttpContext = context };

        Assert.Equal(other, Subject(accessor).TenantId);
    }

    /// <summary>A super-admin who sends no header stays where they are. The override is opt-in per
    /// request, not a standing state.</summary>
    [Fact]
    public void A_Super_Admin_Without_The_Header_Stays_On_Their_Own_Tenant()
    {
        var own = Guid.NewGuid();

        Assert.Equal(own, Subject(Accessor(own, superAdmin: true)).TenantId);
    }

    /// <summary>Garbage in the header is not an override. It falls through to the claim rather
    /// than resolving to Guid.Empty, which would silently empty the app instead of ignoring a
    /// malformed value.</summary>
    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("tenant-acme")]
    public void An_Unparseable_Header_Falls_Through_To_The_Claim(string raw)
    {
        var own = Guid.NewGuid();

        Assert.Equal(own, Subject(Accessor(own, superAdmin: true, overrideHeader: raw)).TenantId);
    }

    /// <summary>Guid.Empty is refused specifically: it is what "no tenant" already looks like, so
    /// honouring it would blank the app rather than switch it.</summary>
    [Fact]
    public void An_Empty_Guid_Header_Is_Not_An_Override()
    {
        var own = Guid.NewGuid();

        var subject = Subject(Accessor(own, superAdmin: true, overrideHeader: Guid.Empty.ToString()));

        Assert.Equal(own, subject.TenantId);
    }

    /// <summary>Two values for one header is an ambiguous request, and choosing one of two
    /// companies to serve is not a coin toss worth making silently.</summary>
    [Fact]
    public void A_Repeated_Header_Is_Not_An_Override()
    {
        var own = Guid.NewGuid();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[]
                {
                    new Claim(AppClaims.TenantId, own.ToString()),
                    new Claim(AppClaims.IsSuperAdmin, "true"),
                },
                authenticationType: "Test")),
        };
        context.Request.Headers[CurrentTenant.TenantOverrideHeader] =
            new Microsoft.Extensions.Primitives.StringValues(new[] { a.ToString(), b.ToString() });

        Assert.Equal(own, Subject(new HttpContextAccessor { HttpContext = context }).TenantId);
    }

    /// <summary>An anonymous caller has no super-admin claim, so the public job pages cannot be
    /// steered either — the header is invisible to them, exactly as it is to any other
    /// non-super-admin.</summary>
    [Fact]
    public void An_Anonymous_Request_With_The_Header_Is_Still_Empty()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[CurrentTenant.TenantOverrideHeader] = Guid.NewGuid().ToString();

        var subject = Subject(new HttpContextAccessor { HttpContext = context });

        Assert.Equal(Guid.Empty, subject.TenantId);
    }
}
