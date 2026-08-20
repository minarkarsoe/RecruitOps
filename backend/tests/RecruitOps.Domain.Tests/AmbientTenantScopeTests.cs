using RecruitOps.Infrastructure.Tenancy;
using Xunit;

namespace RecruitOps.Domain.Tests;

/// <summary>The seam ADR-0026 §4 introduces, on its own.
///
/// <para>Making <c>ICurrentTenant</c> settable is a change to a security-critical boundary, so
/// its rules get tests of their own rather than being covered incidentally by the worker's.</para>
/// </summary>
public class AmbientTenantScopeTests
{
    [Fact]
    public void Starts_Unset_So_A_Request_Scope_Is_Unaffected()
    {
        var scope = new AmbientTenantScope();

        // Null, not Guid.Empty: "nobody entered a tenant here" has to be distinguishable from
        // "somebody entered an empty one", or CurrentTenant cannot tell whether to fall through.
        Assert.Null(scope.TenantId);
    }

    [Fact]
    public void EnterTenant_Binds_The_Scope()
    {
        var tenant = Guid.NewGuid();
        var scope = new AmbientTenantScope();

        scope.EnterTenant(tenant);

        Assert.Equal(tenant, scope.TenantId);
    }

    /// <summary>The reuse guard. A worker that recycled one scope across two messages would run
    /// the second as the first one's tenant — reading its data, writing under its id, and looking
    /// entirely successful. This turns that into a crash.</summary>
    [Fact]
    public void EnterTenant_Refuses_A_Second_Tenant()
    {
        var scope = new AmbientTenantScope();
        scope.EnterTenant(Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(() => scope.EnterTenant(Guid.NewGuid()));
    }

    /// <summary>Re-entering the *same* tenant is refused too. It is always a bug in the caller —
    /// a scope is entered once, by the worker, at a known point — and allowing it would make the
    /// guard above depend on which tenant happened to come second.</summary>
    [Fact]
    public void EnterTenant_Refuses_Even_The_Same_Tenant_Twice()
    {
        var tenant = Guid.NewGuid();
        var scope = new AmbientTenantScope();
        scope.EnterTenant(tenant);

        Assert.Throws<InvalidOperationException>(() => scope.EnterTenant(tenant));
    }

    /// <summary>An empty tenant is indistinguishable from "unset", so accepting it would leave the
    /// caller believing the scope was bound when it behaves exactly as if it were not.</summary>
    [Fact]
    public void EnterTenant_Rejects_An_Empty_Tenant()
    {
        var scope = new AmbientTenantScope();

        Assert.Throws<ArgumentException>(() => scope.EnterTenant(Guid.Empty));
        Assert.Null(scope.TenantId);
    }
}
