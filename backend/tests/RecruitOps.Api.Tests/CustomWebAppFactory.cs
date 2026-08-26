using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost; // ConfigureTestServices lives here
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Identity;
using RecruitOps.Domain.Entities;
using RecruitOps.Domain.Enums;
using RecruitOps.Infrastructure.Persistence;
using RecruitOps.Infrastructure.Services.Delivery;

namespace RecruitOps.Api.Tests;

/// <summary>Boots the API with an in-memory DB and the Test auth scheme, seeded
/// with two tenants so isolation can be asserted.</summary>
public class CustomWebAppFactory : WebApplicationFactory<Program>
{
    // Root registry so the seeding scope and request scopes resolve the same store.
    private static readonly InMemoryDatabaseRoot Root = new();

    // A UNIQUE database per factory instance. xUnit creates one factory per test class
    // (IClassFixture), and the tenant GUIDs below are per-instance. With a shared store
    // name, the first class to run seeded the data and every later class skipped seeding
    // (guarded by "if not Any"), leaving its own TenantA GUID absent from the database —
    // so token/tenant assertions compared two different GUIDs.
    // Isolating the store removes that ordering dependency and any parallel-run race.
    private readonly string _databaseName = $"recruitops-tests-{Guid.NewGuid():N}";

    public const string AdminEmail = "admin@alpha.test";
    public const string AdminPassword = "P@ssw0rd-Test-123";

    // Two departments inside TenantA, plus a hiring manager who owns only the first —
    // the fixture for department scoping (ADR-0003).
    public readonly Guid SalesDepartmentId = Guid.NewGuid();
    public readonly Guid FinanceDepartmentId = Guid.NewGuid();
    public readonly Guid HiringManagerUserId = Guid.NewGuid();
    public const string HiringManagerEmail = "sales.manager@alpha.test";

    // A SECOND hiring manager, owning Finance only. Exists so cross-department panel access
    // (ADR-0017 §4) can be asserted: without a manager who legitimately cannot see the Sales
    // pipeline, "participation is what granted the access" is unprovable.
    public readonly Guid FinanceManagerUserId = Guid.NewGuid();
    public const string FinanceManagerEmail = "finance.manager@alpha.test";

    // Approvers for the seeded 2-step chain (HR then Finance), so sequencing is provable.
    public readonly Guid AdminUserId = Guid.NewGuid();
    public readonly Guid FinanceApproverUserId = Guid.NewGuid();
    public readonly Guid RecruiterUserId = Guid.NewGuid();

    public readonly Guid TenantA = Guid.NewGuid();
    public readonly Guid TenantB = Guid.NewGuid();

    /// <summary>
    /// Whether the AI endpoints may answer from development stubs when no key is configured.
    /// True here so the AI integration tests have something to assert against; overridden to
    /// false by <see cref="NoAiFallbackWebAppFactory"/>, which is the value that ships.
    /// </summary>
    protected virtual bool EnableAiFallback => true;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, cfg) =>
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "test",
                ["Jwt:Audience"] = "test",
                ["Jwt:Key"] = "test-signing-key-that-is-long-enough-0123456789",

                // TestServer has no remote address, so every request in the run shares one
                // rate-limit partition. Left at the production default, the whole suite
                // would trip the per-IP limiter and fail in a way that depends on test
                // ordering. Raised here so the per-ACCOUNT throttle can be tested on its own
                // — LoginThrottleTests is what proves brute-force protection works.
                ["RateLimit:Login:PermitLimit"] = "10000",
                ["RateLimit:PublicApply:PermitLimit"] = "10000",
                // No AI keys in tests, so the endpoints answer from the development stubs. That
                // mirrors appsettings.Development.json, NOT the shipped default — which is
                // EnableFallback: false, i.e. 402. NoAiFallbackWebAppFactory flips this back to
                // the shipped value so the default is covered too.
                ["AI:Claude:EnableFallback"] = EnableAiFallback ? "true" : "false",
                ["AI:Gemini:EnableFallback"] = EnableAiFallback ? "true" : "false",

                // No transport, deliberately. The test host runs as Development, so without
                // these it would inherit appsettings.Development.json's pickup directory and
                // start writing .eml files into the test output. Empty is also the state worth
                // exercising: an install with no mail server must fail visibly, not silently.
                ["Smtp:Host"] = "",
                ["Smtp:PickupDirectory"] = "",
                ["Smtp:FromAddress"] = "",
            }));

        builder.ConfigureTestServices(services =>
        {
            // Swap Npgsql for a shared in-memory database.
            //
            // EF Core 9+ applies provider configuration through
            // IDbContextOptionsConfiguration<TContext> registrations, not just through
            // DbContextOptions<TContext>. Removing only DbContextOptions<AppDbContext>
            // leaves the Npgsql configuration registered, so the options object ends up
            // with BOTH providers and EF throws:
            //   "Services for database providers 'Npgsql...', 'InMemory' have been
            //    registered ... Only a single database provider can be registered."
            //
            // Matching on "DbContextOptions" covers DbContextOptions,
            // DbContextOptions<AppDbContext> and IDbContextOptionsConfiguration<AppDbContext>.
            var toRemove = services
                .Where(d => (d.ServiceType.FullName?.Contains("DbContextOptions") ?? false)
                            || d.ServiceType == typeof(AppDbContext))
                .ToList();
            foreach (var d in toRemove) services.Remove(d);

            services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(_databaseName, Root));

            // Take both background workers off their timers (ADR-0026 §3).
            //
            // WebApplicationFactory starts hosted services, so these would be polling every few
            // seconds throughout the suite — claiming rows out from under the test that just wrote
            // them, and turning any assertion on a queued row into a race that fails occasionally
            // on a slow machine. They stay registered as plain singletons so a test can drive a
            // pass deliberately with RunOnceAsync(). See BulkResumeQueue for the helper.
            var hostedWorkers = services
                .Where(d => d.ImplementationType == typeof(OutboundMessageWorker)
                            || d.ImplementationType == typeof(BulkResumeWorker))
                .ToList();
            foreach (var d in hostedWorkers) services.Remove(d);
            services.AddSingleton<OutboundMessageWorker>();
            services.AddSingleton<BulkResumeWorker>();

            // Replace IFileStorage with InMemoryFileStorage so integration tests run completely offline without S3/MinIO
            var storageDescriptors = services.Where(d => d.ServiceType == typeof(RecruitOps.Application.Interfaces.IFileStorage)).ToList();
            foreach (var d in storageDescriptors) services.Remove(d);
            services.AddSingleton<RecruitOps.Application.Interfaces.IFileStorage, InMemoryFileStorage>();

            // Force the Test auth scheme as the default.
            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();

        // IgnoreQueryFilters: seeding scope has no tenant claim, so reads would be filtered out.
        if (!db.Departments.IgnoreQueryFilters().Any())
        {
            // The public job page shows the company name, and it resolves the company from
            // the posting's tenant id — so the tenant must be a real Company row, not just
            // a GUID that appears in claims.
            // TimeZoneId is set because candidate-facing mail renders the interview slot in it
            // (ADR-0026). A null here would let an invitation go out in UTC and the tests would
            // still pass — which is the exact failure the field exists to prevent.
            db.Companies.Add(new Company
            {
                Id = TenantA, Name = "Alpha Company", Slug = "alpha", TimeZoneId = "Asia/Yangon",
            });
            db.Companies.Add(new Company
            {
                Id = TenantB, Name = "Bravo Company", Slug = "bravo", TimeZoneId = "Asia/Yangon",
            });

            db.Departments.Add(new Department { Id = SalesDepartmentId, TenantId = TenantA, Name = "Alpha Sales", Code = "ALPHA-SALES" });
            db.Departments.Add(new Department { Id = FinanceDepartmentId, TenantId = TenantA, Name = "Alpha Finance", Code = "ALPHA-FIN" });
            db.Departments.Add(new Department { TenantId = TenantB, Name = "Bravo Finance", Code = "BRAVO-FIN" });

            // A hiring manager who owns Sales only.
            var manager = new User
            {
                Id = HiringManagerUserId,
                TenantId = TenantA,
                Email = HiringManagerEmail,
                DisplayName = "Sales Manager",
                Role = UserRole.HiringManager,
                IsActive = true,
            };
            manager.PasswordHash = "not-used-in-these-tests";
            db.Users.Add(manager);
            db.UserDepartments.Add(new UserDepartment
            {
                TenantId = TenantA, UserId = HiringManagerUserId, DepartmentId = SalesDepartmentId,
            });

            var financeManager = new User
            {
                Id = FinanceManagerUserId,
                TenantId = TenantA,
                Email = FinanceManagerEmail,
                DisplayName = "Finance Manager",
                Role = UserRole.HiringManager,
                IsActive = true,
                PasswordHash = "not-used-in-these-tests",
            };
            db.Users.Add(financeManager);
            db.UserDepartments.Add(new UserDepartment
            {
                TenantId = TenantA, UserId = FinanceManagerUserId, DepartmentId = FinanceDepartmentId,
            });

            // One requisition in each department, so scoping is observable.
            db.Requisitions.Add(new Requisition
            {
                TenantId = TenantA, DepartmentId = SalesDepartmentId,
                RequestedByUserId = HiringManagerUserId,
                Title = "Sales Executive", JobDescription = "Sell things.", Headcount = 2,
            });
            db.Requisitions.Add(new Requisition
            {
                TenantId = TenantA, DepartmentId = FinanceDepartmentId,
                RequestedByUserId = HiringManagerUserId,
                Title = "Financial Analyst", JobDescription = "Count things.", Headcount = 1,
            });

            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
            var admin = new User
            {
                Id = AdminUserId,
                TenantId = TenantA,
                Email = AdminEmail,
                DisplayName = "Alpha Admin",
                Role = UserRole.Admin,
                IsActive = true,
            };
            admin.PasswordHash = hasher.HashPassword(admin, AdminPassword);
            db.Users.Add(admin);

            // A second approver, so the seeded chain has two distinct steps.
            var financeApprover = new User
            {
                Id = FinanceApproverUserId,
                TenantId = TenantA,
                Email = "finance.approver@alpha.test",
                DisplayName = "Finance Approver",
                Role = UserRole.Approver,
                IsActive = true,
                PasswordHash = "not-used-in-these-tests",
            };
            db.Users.Add(financeApprover);

            // A recruiter who can raise requisitions in their own name (ADR-0022). Needed as a
            // real user rather than a bare role header because the update and submit paths check
            // that the caller is the requester — without a seeded identity those return 404 by
            // ADR-0003's 404-not-403 rule, which reads as "the permission failed" and is not why.
            // Deliberately not a department member: recruiters are cross-department staff.
            var recruiter = new User
            {
                Id = RecruiterUserId,
                TenantId = TenantA,
                Email = "recruiter@alpha.test",
                DisplayName = "Alpha Recruiter",
                Role = UserRole.Recruiter,
                IsActive = true,
                PasswordHash = "not-used-in-these-tests",
            };
            db.Users.Add(recruiter);

            // Company-wide default chain (DepartmentId null): HR then Finance.
            var chain = new ApprovalChain
            {
                TenantId = TenantA,
                Name = "Default headcount approval",
                DepartmentId = null,
                IsActive = true,
            };
            db.ApprovalChains.Add(chain);
            db.ApprovalChainSteps.Add(new ApprovalChainStep
            {
                TenantId = TenantA, ApprovalChainId = chain.Id,
                Sequence = 1, ApproverUserId = AdminUserId, Label = "HR",
            });
            db.ApprovalChainSteps.Add(new ApprovalChainStep
            {
                TenantId = TenantA, ApprovalChainId = chain.Id,
                Sequence = 2, ApproverUserId = FinanceApproverUserId, Label = "Finance",
            });

            db.SaveChanges();
        }
        return host;
    }
}
