using System.Diagnostics;
using RecruitOps.Application.Common;

namespace RecruitOps.Api.Auth;

/// <summary>Where the request came from, for the audit log (ADR-0027).</summary>
public sealed class HttpRequestOrigin : IRequestOrigin
{
    private readonly IHttpContextAccessor _http;

    public HttpRequestOrigin(IHttpContextAccessor http) => _http = http;

    /// <summary>After <c>UseForwardedHeaders</c> when the API is behind a proxy, so it is the client's.</summary>
    public string? IpAddress => _http.HttpContext?.Connection.RemoteIpAddress?.ToString();

    /// <summary>The value problem-details responses use for <c>traceId</c>, so the two match.</summary>
    public string? TraceId => _http.HttpContext is { } context ? Activity.Current?.Id ?? context.TraceIdentifier : null;

    public string? Endpoint => _http.HttpContext is { } context ? $"{context.Request.Method} {context.Request.Path}" : null;
}
