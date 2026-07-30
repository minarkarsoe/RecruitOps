namespace RecruitOps.Api.Auth;

/// <summary>Custom claim types carried in the JWT.</summary>
public static class AppClaims
{
    /// <summary>The tenant (agency) the user belongs to — drives data isolation.</summary>
    public const string TenantId = "tenant_id";

    /// <summary>Flag indicating whether the principal is a global Super-Admin ("true"/"false").</summary>
    public const string IsSuperAdmin = "is_super_admin";

    /// <summary>Granular permission claim type when carried in token.</summary>
    public const string Permission = "permission";
}

