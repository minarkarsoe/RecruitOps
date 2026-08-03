namespace RecruitOps.Application.DTOs;

/// <summary>
/// The login payload the internal SPA builds its session from.
///
/// <para>
/// <c>Permissions</c> is the user's resolved permission code set, and it is **not optional**.
/// The SPA's <c>hasPermission()</c> helper gates every action button, sidebar link and route
/// guard on it; when this field was absent the helper fell through to a "permissions unknown"
/// branch that returned <c>true</c>, so every user saw the full admin UI. An empty array means
/// "this user has no granted permissions" and must render as such — never as "unknown, allow".
/// </para>
///
/// <para>
/// This is render-gating only. The API re-answers every one of these checks server-side via
/// <c>[HasPermission(...)]</c>; shipping the set to the client is a UX affordance, not the
/// authorization boundary.
/// </para>
/// </summary>
public record LoginResponse(
    string AccessToken,
    DateTimeOffset ExpiresAtUtc,
    string Role,
    string DisplayName,
    Guid UserId,
    IReadOnlyCollection<string> Permissions);
