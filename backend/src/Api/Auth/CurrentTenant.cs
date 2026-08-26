using System.Security.Claims;
using RecruitOps.Application.Common;

namespace RecruitOps.Api.Auth;

/// <summary>Resolves the current tenant for the request. Consumed by AppDbContext's global query
/// filters, so this is the single point where a request's data isolation is decided.
///
/// <para><b>Three sources, in a fixed order, and the order is the security property.</b></para>
///
/// <list type="number">
/// <item><description><b>The <c>X-Tenant-Id</c> header — super-admins only.</b> This is the
/// deliberate exception, added 2026-08-26. Everything about it is in the guard: the header is read
/// <i>only</i> when the principal's own signed token says <c>is_super_admin</c>. For every other
/// caller the header is not merely rejected, it is never looked at.</description></item>
/// <item><description><b>The request's <c>tenant_id</c> claim.</b> The normal path, and it is final
/// for anyone who is not a super-admin.</description></item>
/// <item><description><b>The ambient tenant.</b> Consulted only when there is no request at all —
/// a background worker, which has no <c>HttpContext</c> and would otherwise see
/// <see cref="Guid.Empty"/> and read nothing (ADR-0026 §4).</description></item>
/// </list>
///
/// <para>⚠️ <b>Until 2026-08-26 the claim was read first and won unconditionally</b>, and the
/// comment here said that was what stopped an authenticated request being redirected at another
/// company. That protection has now been deliberately relaxed for one role, so the sentence that
/// replaces it is: <b>a request can be redirected at another company's data if and only if the
/// caller's token carries <c>is_super_admin</c>.</b> If that check is ever weakened, removed, or
/// evaluated against anything other than the signed token, every authenticated user gains every
/// tenant. <c>CurrentTenantResolutionTests</c> pins each clause; a failure there is a security
/// finding, not a test to update.</para>
///
/// <para>Note what is <i>not</i> here: no database call. This getter runs on every query, and
/// whether the target company exists is a separate question answered once per request by
/// <c>SuperAdminTenantOverrideMiddleware</c>. Keeping validation out of the resolution path means
/// a bug in the validator can only turn a request into a 400 — it can never widen access.</para>
/// </summary>
public sealed class CurrentTenant : ICurrentTenant
{
    /// <summary>The impersonation header. Named here so the middleware, the tests and this class
    /// share one symbol rather than three string literals that can drift apart.</summary>
    public const string TenantOverrideHeader = "X-Tenant-Id";

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
            var context = _http.HttpContext;

            // 1. Super-admin impersonation. The predicate is CurrentUser's, called statically —
            //    not a second copy written here. This repo's recurring bug is one rule expressed
            //    twice and the copy not following when the original is corrected, and a stale
            //    copy of *this* rule would hand every tenant to everybody.
            //
            //    Static rather than an injected ICurrentUser because CurrentTenant is also
            //    resolved in background scopes, which have no user and should not have to
            //    register one to answer a question they never ask.
            if (context is not null
                && CurrentUser.IsSuperAdminPrincipal(context.User)
                && TryReadOverride(context, out var impersonated))
            {
                return impersonated;
            }

            // 2. The request's own claim.
            var value = context?.User.FindFirstValue(AppClaims.TenantId);
            if (Guid.TryParse(value, out var fromRequest))
            {
                return fromRequest;
            }

            // 3. No request: a background scope may have entered one.
            return _ambient.TenantId ?? Guid.Empty;
        }
    }

    /// <summary>Reads and parses the override header. Shared with the middleware so the two cannot
    /// disagree about what counts as "an override was requested".
    ///
    /// <para><see cref="Guid.Empty"/> is refused: it is what "no tenant" already looks like, so
    /// honouring it would silently empty the app rather than switch it.</para></summary>
    public static bool TryReadOverride(HttpContext context, out Guid tenantId)
    {
        tenantId = Guid.Empty;

        if (!context.Request.Headers.TryGetValue(TenantOverrideHeader, out var raw))
            return false;

        // A repeated header is ambiguous, and picking one of two answers about which company's
        // data to serve is not a decision to make silently.
        if (raw.Count != 1)
            return false;

        return Guid.TryParse(raw[0], out tenantId) && tenantId != Guid.Empty;
    }
}
