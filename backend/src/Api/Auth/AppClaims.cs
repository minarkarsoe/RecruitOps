namespace RecruitOps.Api.Auth;

/// <summary>Custom claim types carried in the JWT.</summary>
public static class AppClaims
{
    /// <summary>The tenant (agency) the user belongs to — drives data isolation.</summary>
    public const string TenantId = "tenant_id";
}
