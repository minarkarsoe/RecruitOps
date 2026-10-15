namespace RecruitOps.Application.Common;

/// <summary>Carries a tenant for code that runs with no HTTP request behind it — today, the
/// background delivery worker (ADR-0026 §4).
///
/// <para><b>Why this exists.</b> <c>CurrentTenant</c> reads the tenant from the request's JWT.
/// A <c>BackgroundService</c> has no request, so it sees <see cref="System.Guid.Empty"/>, every
/// global query filter matches nothing, and inserts would be stamped with an empty tenant. The
/// worker therefore reads the tenant off the message row it claimed and enters it here, so the
/// handler that runs next queries with the filters ON and looks exactly like request code.</para>
///
/// <para><b>The safety property, and it is the whole point:</b> an HTTP claim always wins. This
/// scope is consulted only when there is no request. Request-path code can resolve this and call
/// <see cref="EnterTenant"/> and it changes nothing — so this cannot be used to read across
/// tenants from inside an authenticated request.</para>
///
/// <para>Registered <b>scoped</b>. One DI scope carries at most one tenant, for its whole life.</para>
/// </summary>
public interface IAmbientTenantScope
{
    /// <summary>The tenant entered for this scope, or null if none was — which is the normal
    /// state inside a request.</summary>
    Guid? TenantId { get; }

    /// <summary>Binds this scope to a tenant.
    ///
    /// <para><b>Throws if the scope already has one.</b> Reusing a scope across two messages
    /// belonging to different tenants is the failure this guards: without it the second message
    /// would quietly run as the first one's tenant, read its data, and look like it worked. A
    /// loud exception on the second call turns a silent cross-tenant read into a crash, which is
    /// the trade any day of the week.</para></summary>
    void EnterTenant(Guid tenantId);
}
