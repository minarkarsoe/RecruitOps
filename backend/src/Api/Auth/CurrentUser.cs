using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using RecruitOps.Application.Common;
using RecruitOps.Domain;
using RecruitOps.Domain.Enums;

namespace RecruitOps.Api.Auth;

/// <summary>Reads the authenticated principal from the request's claims.</summary>
public sealed class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _http;

    public CurrentUser(IHttpContextAccessor http) => _http = http;

    private ClaimsPrincipal? Principal => _http.HttpContext?.User;

    public Guid? UserId
    {
        get
        {
            // JwtSecurityTokenHandler maps "sub" to ClaimTypes.NameIdentifier by default,
            // so accept either spelling rather than depending on mapping behaviour.
            var raw = Principal?.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub);
            return Guid.TryParse(raw, out var id) ? id : null;
        }
    }

    public string? Role => Principal?.FindFirstValue(ClaimTypes.Role);

    /// <summary>The role claim as the domain enum, or null if absent/unrecognised.</summary>
    private UserRole? ParsedRole => RoleScope.Parse(Role);

    // Both predicates below fail CLOSED on an unparseable role: unscoped and un-excluded are
    // the permissive answers, and a misconfigured or unknown role should not read like an
    // Admin. Unreachable through the policies as configured today, which is the point of
    // putting it here rather than relying on that staying true.

    public bool IsDepartmentScoped =>
        ParsedRole is not { } role || RoleScope.IsDepartmentScoped(role);

    public bool IsExcludedFromCandidateData =>
        ParsedRole is not { } role || RoleScope.IsExcludedFromCandidateData(role);

    public bool IsSuperAdmin => IsSuperAdminPrincipal(Principal);

    /// <summary>Is this principal a super-admin? <b>The one definition</b>, static so that
    /// <see cref="CurrentTenant"/> can ask the same question without taking a dependency on
    /// <see cref="ICurrentUser"/>.
    ///
    /// <para>Why it is not simply injected there: <c>CurrentTenant</c> is resolved in background
    /// scopes too, where there is no request and no user, and the worker tests deliberately use
    /// the real implementation rather than a stand-in. Making tenant resolution require an
    /// HTTP-shaped service would have meant every composition root that runs a job registering
    /// one for the sake of a question it never asks.</para>
    ///
    /// <para>⚠️ This predicate now decides whether a request may be steered at another company's
    /// data (<c>X-Tenant-Id</c>). Keep it here, in one place — a second copy that does not follow
    /// when this one is corrected is this repo's recurring bug, and the blast radius of that
    /// particular copy would be every tenant.</para></summary>
    public static bool IsSuperAdminPrincipal(ClaimsPrincipal? principal) =>
        string.Equals(principal?.FindFirstValue(AppClaims.IsSuperAdmin), "true", StringComparison.OrdinalIgnoreCase)
        || RoleScope.Parse(principal?.FindFirstValue(ClaimTypes.Role)) == UserRole.SuperAdmin;
}
