namespace RecruitOps.Api.Security;

/// <summary>Response headers for an API that serves only JSON — mostly HR data and candidate PII.
/// <para>TLS and HSTS belong to whatever terminates TLS in front of the install (nginx in the
/// reference topology); the API itself speaks plain HTTP on the internal network, so it cannot
/// tell an HTTPS client from an HTTP one and must not claim to.</para></summary>
public static class SecurityHeaders
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use((context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers.XContentTypeOptions = "nosniff";
                headers.XFrameOptions = "DENY";
                headers["Referrer-Policy"] = "no-referrer";

                // Nothing here is a document a browser should render or run. Swagger UI is the one
                // exception — it is HTML and script, and only mapped in Development.
                if (!context.Request.Path.StartsWithSegments("/swagger"))
                    headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";

                // Responses carry salaries, interview scores and candidate contact details: no
                // browser or intermediary cache keeps a copy. An endpoint that sets its own
                // caching keeps it.
                if (context.Request.Path.StartsWithSegments("/api") && !headers.ContainsKey("Cache-Control"))
                    headers.CacheControl = "no-store";

                return Task.CompletedTask;
            });
            return next(context);
        });
}
