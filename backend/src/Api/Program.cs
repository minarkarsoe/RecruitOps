using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using RecruitOps.Api.Auth;
using RecruitOps.Api.Middleware;
using RecruitOps.Infrastructure.Services.Delivery;
using RecruitOps.Application.Common;
using RecruitOps.Application.Interfaces;
using RecruitOps.Infrastructure;
using RecruitOps.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// --- Logging ---
// Outside Development, one JSON object per line: an on-prem install's logs end up in whatever
// the customer runs (journald, a file, a log shipper), and every one of those can parse JSON.
// Scopes carry the request's TraceId, which problem details also return to the caller — so
// "it failed, here is the trace id" leads straight to the log lines.
if (!builder.Environment.IsDevelopment())
{
    builder.Logging.ClearProviders();
    builder.Logging.AddJsonConsole(o =>
    {
        o.IncludeScopes = true;
        o.UseUtcTimestamp = true;
        o.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
    });
}

// Access log: one line per request. Method, path, status and duration only — no query string
// (a candidate search would put names in the log) and no headers (the bearer token).
builder.Services.AddHttpLogging(o =>
{
    o.LoggingFields = HttpLoggingFields.RequestMethod | HttpLoggingFields.RequestPath
                    | HttpLoggingFields.ResponseStatusCode | HttpLoggingFields.Duration;
    o.CombineLogs = true;
});

// Unhandled exceptions become RFC 9457 problem details with a traceId, never the exception text.
builder.Services.AddProblemDetails();

builder.Services.AddControllers(options =>
{
    options.Filters.Add<RecruitOps.Api.Filters.FeatureGateFilter>();
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpContextAccessor();

// Infrastructure (EF Core + Postgres).
builder.Services.AddInfrastructure(builder.Configuration);

// Tenant resolved from the JWT's tenant_id claim (Module 1), falling back to an ambient tenant
// when there is no request at all — the delivery worker. The claim wins whenever it is present;
// see CurrentTenant for why that order is a security property (ADR-0026 §4).
builder.Services.AddScoped<ICurrentTenant, CurrentTenant>();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<IRequestOrigin, HttpRequestOrigin>();

// Background queues (ADR-0026). In-process, because ADR-0004 ships one instance per company —
// which removes the problem a distributed scheduler exists to solve.
builder.Services.AddHostedService<OutboundMessageWorker>();
builder.Services.AddHostedService<BulkResumeWorker>();

// --- Authentication: self-issued JWT bearer ---
var jwt = builder.Configuration.GetSection("Jwt");
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt["Issuer"],
            ValidAudience = jwt["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwt["Key"] ?? string.Empty)),
            RoleClaimType = ClaimTypes.Role,
        };

        // A valid signature proves who the user WAS at sign-in. Until 2026-10-15 that was all the
        // API checked, for the token's full 8 hours — so a deactivated Admin could still create a
        // fresh Admin account. Now the account must still exist, be active, and hold the role and
        // SuperAdmin flag the token claims; anything else is a 401 and a fresh sign-in.
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var principal = context.Principal!; // set by the handler before this event fires
                var sub = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                          ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
                var tenant = principal.FindFirstValue(AppClaims.TenantId);
                var superAdmin = string.Equals(
                    principal.FindFirstValue(AppClaims.IsSuperAdmin), "true", StringComparison.OrdinalIgnoreCase);

                var validator = context.HttpContext.RequestServices.GetRequiredService<IUserSessionValidator>();
                var current = Guid.TryParse(sub, out var userId)
                              && Guid.TryParse(tenant, out var tenantId)
                              && await validator.IsCurrentAsync(
                                  userId, tenantId, principal.FindFirstValue(ClaimTypes.Role), superAdmin,
                                  context.HttpContext.RequestAborted);

                if (!current)
                {
                    // The trace an operator needs to see a deactivated account's token being
                    // replayed. The user id only — never the token.
                    context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                        .CreateLogger("RecruitOps.Auth.SessionValidation")
                        .LogWarning("Rejected a token for user {UserId}: the account no longer matches it.", sub);
                    context.Fail("This session is no longer valid. Sign in again.");
                }
            },
        };
    });

// --- Authorization: Dynamic RBAC & policies ---
builder.Services.AddSingleton<IAuthorizationPolicyProvider, RecruitOps.Api.Authorization.PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, RecruitOps.Api.Authorization.PermissionAuthorizationHandler>();

builder.Services.AddAuthorization(options =>
{
    // Cross-department staff.
    options.AddPolicy(Policies.RecruitmentStaff, p =>
        p.RequireRole(Roles.Admin, Roles.HrDirector, Roles.Recruiter));

    // Any internal user, including department-scoped roles. Endpoints using this
    // MUST apply a department predicate themselves (ADR-0003).
    options.AddPolicy(Policies.InternalUser, p =>
        p.RequireRole(Roles.Admin, Roles.HrDirector, Roles.Recruiter,
                      Roles.HiringManager, Roles.Approver));

    // InternalUser plus Interviewer, for the panel's own screens only (ADR-0021 amendment,
    // 2026-10-19). Those services reach an application through IApplicationAccess, which gives
    // Interviewer nothing but the panels it sits on.
    options.AddPolicy(Policies.PanelAccess, p =>
        p.RequireRole(Roles.Admin, Roles.HrDirector, Roles.Recruiter,
                      Roles.HiringManager, Roles.Approver, Roles.Interviewer));

    options.AddPolicy(Policies.AdminOnly, p => p.RequireRole(Roles.Admin));

    // Fails closed: no role claim, or one that is not exactly a role, is not company-wide.
    options.AddPolicy(Policies.CompanyWide, p => p.RequireAssertion(ctx =>
        RecruitOps.Domain.RoleScope.Parse(ctx.User.FindFirstValue(ClaimTypes.Role)) is { } role
        && !RecruitOps.Domain.RoleScope.IsDepartmentScoped(role)));

    // Secure-by-default: every endpoint requires an authenticated user unless it
    // opts out with [AllowAnonymous] (e.g. login, the public job page).
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// --- Rate limiting: brute-force protection on credential submission ---
//
// This is the per-IP half of the control. The per-account half is ILoginThrottle: one IP
// guessing many accounts and many IPs guessing one account are different attacks, and
// neither control can see the other's.
//
// Limits are configurable because the right number depends on the deployment — an office
// behind a single NAT address legitimately produces many logins from one IP, and a limit
// tuned for the public internet would lock that office out at 09:00.
builder.Services.Configure<LoginRateLimitOptions>(builder.Configuration.GetSection("RateLimit:Login"));
builder.Services.Configure<PublicApplyRateLimitOptions>(builder.Configuration.GetSection("RateLimit:PublicApply"));
builder.Services.Configure<RefreshRateLimitOptions>(builder.Configuration.GetSection("RateLimit:Refresh"));

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy(RateLimitPolicies.Login, httpContext =>
    {
        // Resolved per request, NOT captured at startup: see LoginRateLimitOptions.
        var limits = httpContext.RequestServices
            .GetRequiredService<IOptions<LoginRateLimitOptions>>().Value;

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ClientPartitionKey(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = limits.PermitLimit,
                Window = TimeSpan.FromSeconds(limits.WindowSeconds),
                // Queueing a login attempt would just delay the same guess. Reject outright.
                QueueLimit = 0,
            });
    });

    // Module 2.1 — the public job page and application form. Anonymous and writing.
    options.AddPolicy(RateLimitPolicies.PublicApply, httpContext =>
    {
        var limits = httpContext.RequestServices
            .GetRequiredService<IOptions<PublicApplyRateLimitOptions>>().Value;

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ClientPartitionKey(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = limits.PermitLimit,
                Window = TimeSpan.FromSeconds(limits.WindowSeconds),
                QueueLimit = 0,
            });
    });

    // ADR-0028 — token refresh and revoke. Anonymous, and each call is a guess at a session.
    options.AddPolicy(RateLimitPolicies.Refresh, httpContext =>
    {
        var limits = httpContext.RequestServices
            .GetRequiredService<IOptions<RefreshRateLimitOptions>>().Value;

        // A zero or negative setting would throw on every request or refuse every refresh —
        // fall back to the defaults rather than log everyone out.
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ClientPartitionKey(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = limits.PermitLimit > 0 ? limits.PermitLimit : 60,
                Window = TimeSpan.FromSeconds(limits.WindowSeconds > 0 ? limits.WindowSeconds : 60),
                QueueLimit = 0,
            });
    });

    options.OnRejected = (context, _) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        return ValueTask.CompletedTask;
    };
});

// --- Reverse proxy ---
//
// Behind nginx (docker compose) every request arrives from the proxy's address, so the
// per-IP limiter above would put the whole world in one bucket — either useless or a
// self-inflicted outage. X-Forwarded-For fixes that, but it is client-supplied: trusting it
// on a directly-reachable API lets an attacker mint a fresh partition key per request and
// bypass the limit entirely. So it is OFF unless the operator states that the API sits
// behind a proxy and is not reachable directly. See docs/architecture/local-development.md.
var behindProxy = builder.Configuration.GetValue("ReverseProxy:TrustForwardedHeaders", false);
if (behindProxy)
{
    builder.Services.Configure<ForwardedHeadersOptions>(o =>
    {
        o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        // The proxy's address is assigned by Docker and not knowable ahead of time; the
        // security boundary here is the network (the API port is not published), not an
        // address allowlist. This is exactly why the flag has to be set deliberately.
        o.KnownIPNetworks.Clear();
        o.KnownProxies.Clear();
        // ⚠️ LOAD-BEARING: ForwardLimit = 1 means only the LAST X-Forwarded-For entry is
        // consumed — the one our own nginx appended via $proxy_add_x_forwarded_for. Raising
        // it would start honouring client-supplied entries, letting anyone spoof the
        // rate-limit partition key. Do not "fix" this to a larger number.
        o.ForwardLimit = 1;
    });
}

// Partition key shared by both anonymous-endpoint limiters.
//
// IPv6 is grouped by /64 rather than by address. A single residential or VPS IPv6
// allocation is a /64 or larger, so partitioning on the full address would give one
// attacker 2^64 free buckets — the per-IP limit would exist on paper only.
static string ClientPartitionKey(HttpContext context)
{
    var ip = context.Connection.RemoteIpAddress;

    // No remote address: the in-process test server, or a Unix socket. One shared bucket is
    // the safe reading — we cannot tell these callers apart.
    if (ip is null) return "unknown";

    if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
    if (ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetworkV6) return ip.ToString();

    var bytes = ip.GetAddressBytes();
    return $"v6:{Convert.ToHexString(bytes, 0, 8)}";
}

// --- CORS: off unless configured ---
// Both frontends reach the API same-origin: nginx proxies /api for the SPA, Next.js rewrites it
// for the public site, and its server components call in-network. So the default is no CORS
// policy at all — until 2026-10-15 the localhost dev origins were allowed in every environment.
// Development lists the dev servers (appsettings.Development.json); an install that serves a
// frontend from another origin lists it in Cors:AllowedOrigins.
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
if (corsOrigins.Length > 0)
{
    builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
        p.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod()));
}

var app = builder.Build();

// Outside Development, refuse to start with a signing key anyone could know. .env.example ships
// one, and an install that copied it would accept tokens minted by anyone who has read the
// repository (second security review, 2026-10-15). Checked after Build() so every
// configuration source — environment, files, a test host's settings — has been applied.
if (!app.Environment.IsDevelopment())
{
    // Every key this repository has ever printed. The second and third were placeholders in
    // backend/src/Api/.env.example and docs/architecture/vps-deployment-guide.md — long enough to
    // pass the length check, so an install that copied one started happily (security review of
    // ADR-0029, 2026-10-16). Add to this list, never remove from it.
    string[] publishedKeys =
    [
        "dev-only-signing-key-change-me-at-least-32-chars",
        "CHANGE_ME_use_a_long_random_secret_at_least_32_bytes",
        "SuperSecretProductionJwtSigningKey2026_AtLeast32CharsLong!",
    ];
    var signingKey = app.Configuration["Jwt:Key"] ?? string.Empty;
    if (Encoding.UTF8.GetByteCount(signingKey) < 32 || publishedKeys.Contains(signingKey))
        throw new InvalidOperationException(
            "Jwt:Key must be a secret of at least 32 bytes, not the example value. Generate one per install.");
}

// Outermost, so it records the status the client finally received — including a 500 produced
// by the exception handler below.
app.UseHttpLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Must run before anything reads the client address — the rate limiter does.
if (behindProxy) app.UseForwardedHeaders();

app.UseSecurityHeaders();

// Development keeps the developer exception page the host adds by default.
if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler();

if (corsOrigins.Length > 0) app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// Order matters: this reads context.User, so it must sit after UseAuthentication(). It only
// validates a super-admin's X-Tenant-Id override — who may impersonate is decided in
// CurrentTenant, from the signed token, with no database call.
app.UseSuperAdminTenantOverride();

app.MapControllers();

// Apply pending migrations before serving traffic (ADR-0004: unattended installs).
// No-ops on the in-memory provider used by tests.
await DatabaseStartup.MigrateAsync(app.Services);
await DbInitializer.SeedPermissionsAndRolesAsync(app.Services);

// The first Admin from configuration: Development whenever that email is absent; elsewhere only
// on an empty install (see SeedAdminAsync).
await DbInitializer.SeedAdminAsync(app.Services, firstRunOnly: !app.Environment.IsDevelopment());

app.Run();

// Exposed so the integration test project (WebApplicationFactory<Program>) can boot the app.
public partial class Program { }
