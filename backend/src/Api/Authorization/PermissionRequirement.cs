using Microsoft.AspNetCore.Authorization;

namespace RecruitOps.Api.Authorization;

/// <summary>
/// Represents the authorization requirement for a dynamic permission code.
/// </summary>
public sealed class PermissionRequirement : IAuthorizationRequirement
{
    public string PermissionCode { get; }

    public PermissionRequirement(string permissionCode)
    {
        PermissionCode = permissionCode ?? throw new ArgumentNullException(nameof(permissionCode));
    }
}
