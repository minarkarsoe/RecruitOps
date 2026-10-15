namespace RecruitOps.Api.Middleware;

public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        Apply(context, overwrite: true);

        // The exception handler clears the response — headers included — before it writes a
        // 500, so the headers above would be gone from exactly the responses most likely to leak
        // something. Re-apply whatever is missing as the response starts; anything an endpoint
        // set itself is left alone.
        context.Response.OnStarting(() =>
        {
            Apply(context, overwrite: false);
            return Task.CompletedTask;
        });

        await _next(context);
    }

    private static void Apply(HttpContext context, bool overwrite)
    {
        var headers = context.Response.Headers;
        void Set(string name, string value)
        {
            if (overwrite || !headers.ContainsKey(name)) headers[name] = value;
        }

        Set("X-Content-Type-Options", "nosniff");
        Set("X-Frame-Options", "DENY");
        Set("Referrer-Policy", "strict-origin-when-cross-origin");
        Set("Content-Security-Policy", "default-src 'self'; frame-ancestors 'none'; object-src 'none';");

        // Responses carry salaries, interview scores and candidate contact details: no browser or
        // intermediary cache keeps a copy (2026-10-15). An endpoint that sets its own caching
        // overwrites this when it writes its response.
        if (context.Request.Path.StartsWithSegments("/api"))
            Set("Cache-Control", "no-store");
    }
}

public static class SecurityHeadersMiddlewareExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
    {
        return app.UseMiddleware<SecurityHeadersMiddleware>();
    }
}
