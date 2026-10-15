namespace RecruitOps.Application.DTOs;

/// <param name="Permissions">The effective permission set the API enforces for this user
/// (<c>IPermissionEvaluator.GetEffectivePermissionsAsync</c>), sorted. The client uses it to
/// decide what to <i>render</i>; the API re-checks every request regardless. It reflects the
/// moment of login — role edits take effect server-side on the next request, and in the UI on
/// the next sign-in.</param>
/// <param name="IsSuperAdmin">Mirrors the handler's bypass, so the client can mirror it too.</param>
public record LoginResponse(
    string AccessToken,
    DateTimeOffset ExpiresAtUtc,
    string Role,
    string DisplayName,
    Guid UserId,
    IReadOnlyList<string> Permissions,
    bool IsSuperAdmin);
