using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using RecruitOps.Api.Auth;
using RecruitOps.Api.Middleware;
using RecruitOps.Application.Common;
using RecruitOps.Infrastructure;
using RecruitOps.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers(options =>
{
    options.Filters.Add<RecruitOps.Api.Filters.FeatureGateFilter>();
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpContextAccessor();

// Infrastructure (EF Core + Postgres).
builder.Services.AddInfrastructure(builder.Configuration);

// Tenant resolved from the JWT's tenant_id claim (Module 1).
builder.Services.AddScoped<ICurrentTenant, CurrentTenant>();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

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

    options.AddPolicy(Policies.AdminOnly, p => p.RequireRole(Roles.Admin));

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

const string DevCors = "DevCors";
builder.Services.AddCors(o => o.AddPolicy(DevCors, p =>
    p.WithOrigins(
        "http://localhost:3000",  // public Next.js app / docker web service
        "http://localhost:5173"   // internal SPA (Vite dev server)
    ).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Must run before anything reads the client address — the rate limiter does.
if (behindProxy) app.UseForwardedHeaders();

app.UseSecurityHeaders();
app.UseCors(DevCors);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Apply pending migrations before serving traffic (ADR-0004: unattended installs).
// No-ops on the in-memory provider used by tests.
await DatabaseStartup.MigrateAsync(app.Services);
await DbInitializer.SeedPermissionsAndRolesAsync(app.Services);

if (app.Environment.IsDevelopment())
    await DbInitializer.SeedAsync(app.Services);

app.Run();

// Exposed so the integration test project (WebApplicationFactory<Program>) can boot the app.
public partial class Program { }
