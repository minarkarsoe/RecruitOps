using RecruitOps.Application.Common;

namespace RecruitOps.Infrastructure.Tenancy;

/// <inheritdoc cref="IAmbientTenantScope"/>
/// <remarks>Deliberately not thread-safe and deliberately not resettable. A DI scope is used by
/// one logical operation at a time, and "reset" is the operation that would let a scope be
/// recycled across tenants.</remarks>
public sealed class AmbientTenantScope : IAmbientTenantScope
{
    public Guid? TenantId { get; private set; }

    public void EnterTenant(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            // An empty tenant is what "no tenant" already looks like, so accepting it would make
            // this call a silent no-op and the caller would believe it was scoped.
            throw new ArgumentException("Cannot enter an empty tenant.", nameof(tenantId));
        }

        if (TenantId is { } existing)
        {
            throw new InvalidOperationException(
                $"This scope is already bound to tenant {existing}. Create a new DI scope per " +
                "message rather than reusing one — see IAmbientTenantScope.EnterTenant.");
        }

        TenantId = tenantId;
    }
}
