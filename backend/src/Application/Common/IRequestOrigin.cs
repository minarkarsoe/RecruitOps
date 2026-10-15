namespace RecruitOps.Application.Common;

/// <summary>Where the current request came from, for the audit log (ADR-0027). Every member is
/// null outside an HTTP request — a background job or the startup seeder.</summary>
public interface IRequestOrigin
{
    /// <summary>The client address, after forwarded headers when the API is behind a proxy.</summary>
    string? IpAddress { get; }

    /// <summary>The same id a problem-details error response carries as <c>traceId</c>.</summary>
    string? TraceId { get; }

    /// <summary><c>"PUT /api/users/…"</c> — what was being attempted, for a refusal.</summary>
    string? Endpoint { get; }
}
