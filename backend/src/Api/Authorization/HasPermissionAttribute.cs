using Microsoft.AspNetCore.Authorization;

namespace RecruitOps.Api.Authorization;

/// <summary>
/// Authorizes access to an action based on a required dynamic permission string.
/// Format: "permission:module:feature:action" (or shorthand "module:feature:action").
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class HasPermissionAttribute : AuthorizeAttribute
{
    public const string PolicyPrefix = "Permission:";

    public string Permission { get; }

    public HasPermissionAttribute(string permission)
    {
        if (string.IsNullOrWhiteSpace(permission))
            throw new ArgumentNullException(nameof(permission));

        Permission = NormalizePermissionCode(permission);
        Policy = $"{PolicyPrefix}{Permission}";
    }

    /// <summary>
    /// Normalizes shorthand "requisitions:requisitions:approve" to standard "permission:requisitions:requisitions:approve".
    /// </summary>
    public static string NormalizePermissionCode(string code)
    {
        var trimmed = code.Trim();
        if (trimmed.StartsWith("permission:", StringComparison.OrdinalIgnoreCase))
            return trimmed.ToLowerInvariant();

        return $"permission:{trimmed.ToLowerInvariant()}";
    }
}
