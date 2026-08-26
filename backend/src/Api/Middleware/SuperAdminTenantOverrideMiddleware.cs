using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecruitOps.Api.Auth;
using RecruitOps.Application.Common;
using RecruitOps.Infrastructure.Persistence;

namespace RecruitOps.Api.Middleware;

/// <summary>Validates an <c>X-Tenant-Id</c> override once per request, and rejects one that names
/// a company that does not exist or is not active.
///
/// <para><b>This is a guard, not a grant.</b> It is deliberately separate from
/// <see cref="CurrentTenant"/>, which decides who may impersonate and does so from the signed
/// token with no database call. The worst a bug in this file can do is turn a request into a 400;
/// it has no path to widening anyone's access. Keeping those two jobs apart is the point —
/// validation runs once per request, resolution runs on every query.</para>
///
/// <para>Without it, a mistyped tenant id gives a super-admin an app that is silently and
/// completely empty, which reads exactly like data loss. A 400 that names the id is the honest
/// answer to "I sent you a company that isn't here".</para>
/// </summary>
public class SuperAdminTenantOverrideMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SuperAdminTenantOverrideMiddleware> _logger;

    public SuperAdminTenantOverrideMiddleware(
        RequestDelegate next,
        ILogger<SuperAdminTenantOverrideMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ICurrentUser user, AppDbContext db)
    {
        if (!CurrentTenant.TryReadOverride(context, out var requested))
        {
            await _next(context);
            return;
        }

        if (!user.IsSuperAdmin)
        {
            // Ignored, not refused. The SPA has historically attached this header for every
            // signed-in user with their *own* tenant id, so refusing would break ordinary
            // requests that are asking for nothing they do not already have. CurrentTenant never
            // reads it for them, so the request proceeds on their own claim either way.
            //
            // Logged only when it disagrees with their claim: that is somebody asking for another
            // company's data, which is worth a line in the log even though it does nothing.
            var own = context.User.FindFirst(AppClaims.TenantId)?.Value;
            if (!string.Equals(own, requested.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Ignored an {Header} of {Requested} from a non-super-admin whose own tenant is {Own}.",
                    CurrentTenant.TenantOverrideHeader, requested, own);
            }

            await _next(context);
            return;
        }

        // Companies carry no tenant filter of their own (one row per deployment, ADR-0004), so
        // this reads correctly regardless of which tenant is currently resolved — and it must,
        // because the tenant currently resolved is the very thing being validated.
        var exists = await db.Companies.AsNoTracking()
            .AnyAsync(c => c.Id == requested && c.IsActive, context.RequestAborted);

        if (!exists)
        {
            _logger.LogWarning(
                "Super-admin requested tenant {Requested}, which does not exist or is not active.",
                requested);

            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Unknown tenant",
                Detail = $"No active company with id {requested}. The tenant switcher is showing a "
                         + "company that is not in this database.",
            }, context.RequestAborted);
            return;
        }

        await _next(context);
    }
}

public static class SuperAdminTenantOverrideMiddlewareExtensions
{
    /// <summary>⚠️ Must be registered <b>after</b> <c>UseAuthentication()</c>. Before it,
    /// <c>context.User</c> is anonymous, <c>ICurrentUser.IsSuperAdmin</c> is false for everyone,
    /// and every override would be logged as an ignored non-super-admin attempt while
    /// <see cref="CurrentTenant"/> — which runs later, at query time, with the real principal —
    /// honoured it unvalidated.</summary>
    public static IApplicationBuilder UseSuperAdminTenantOverride(this IApplicationBuilder app)
        => app.UseMiddleware<SuperAdminTenantOverrideMiddleware>();
}
