using Amazon.S3;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RecruitOps.Application.Common;
using RecruitOps.Application.Interfaces;
using RecruitOps.Domain.Entities;
using RecruitOps.Infrastructure.Options;
using RecruitOps.Infrastructure.Persistence;
using RecruitOps.Infrastructure.Services;
using RecruitOps.Infrastructure.Services.Delivery;
using RecruitOps.Infrastructure.Services.Delivery.Handlers;
using RecruitOps.Infrastructure.Services.FileStorage;
using RecruitOps.Infrastructure.Tenancy;
using RecruitOps.Infrastructure.Services.MyanmarScript;

using RecruitOps.Infrastructure.Services.DocumentExtraction;

namespace RecruitOps.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration config)
    {
        var connStr = config.GetConnectionString("Default");
        if (string.Equals(connStr, "InMemory", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(connStr, "UseInMemoryDatabase", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrEmpty(connStr))
        {
            services.AddDbContext<AppDbContext>(opt => opt.UseInMemoryDatabase("RecruitOpsDev"));
        }
        else
        {
            services.AddDbContext<AppDbContext>(opt => opt.UseNpgsql(connStr));
        }

        services.AddSingleton(TimeProvider.System);
        services.AddMemoryCache();

        // Auth & Dynamic RBAC
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IPermissionEvaluator, PermissionEvaluator>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IFeatureFlagService, FeatureFlagService>();

        // Singleton: the failure counters have to outlive the request that recorded them,
        // which is the entire point.
        services.AddSingleton<ILoginThrottle, LoginThrottle>();

        services.AddScoped<IDepartmentService, DepartmentService>();

        // Outbound delivery & background jobs (ADR-0026).
        // Scoped, and unset in a request scope: CurrentTenant reads the JWT claim first and only
        // consults this when there is none, so entering a tenant mid-request is inert. The
        // delivery worker is what actually uses it, one scope per message.
        services.AddScoped<IAmbientTenantScope, AmbientTenantScope>();
        services.Configure<OutboundDeliveryOptions>(config.GetSection(OutboundDeliveryOptions.SectionName));

        // SMTP is the floor, not a fallback (ADR-0026 §1): it is the only transport that works in
        // every deployment we sell, including an on-premise install with no outbound internet.
        // Any provider adapter added later is registered alongside, never instead.
        services.Configure<SmtpOptions>(config.GetSection(SmtpOptions.SectionName));
        services.AddScoped<IEmailSender, SmtpEmailSender>();

        // The second queue on the same mechanism (Module 2.3). Its own options because extracting
        // text from a scanned PDF and sending an email have opposite characters — see the class.
        services.Configure<BulkResumeOptions>(config.GetSection(BulkResumeOptions.SectionName));

        // One handler per OutboundMessageKind, resolved by the worker inside the message's own
        // tenant scope. Registered as IOutboundMessageHandler (plural resolution) — a Kind with no
        // handler retries rather than failing, so a missing line here is loud but not destructive.
        services.AddScoped<IOutboundMessageHandler, InterviewInvitationHandler>();

        // Module 1 + department scoping (ADR-0003)
        services.AddScoped<IDepartmentAccess, DepartmentAccess>();
        services.AddScoped<IRequisitionService, RequisitionService>();
        services.AddScoped<IApprovalChainService, ApprovalChainService>();
        services.AddScoped<IJdTemplateService, JdTemplateService>();

        // Module 2 — ATS & sourcing
        services.AddScoped<IJobPostingService, JobPostingService>();
        services.AddScoped<IPipelineService, PipelineService>();
        // Anonymous surface: takes no ICurrentUser/IDepartmentAccess by design — there is
        // no user on those requests, and the token is what establishes the tenant.
        services.AddScoped<IPublicJobService, PublicJobService>();

        // Module 3 — interview & assessment.
        // IApplicationAccess is scoped and caches per request: within one request the
        // controller, the service and mention resolution all ask about the same application.
        services.AddScoped<IApplicationAccess, ApplicationAccess>();
        services.AddScoped<IScorecardTemplateService, ScorecardTemplateService>();
        services.AddScoped<IInterviewService, InterviewService>();
        services.AddScoped<IScorecardService, ScorecardService>();
        services.AddScoped<INoteService, NoteService>();

        // Module 4 — Hybrid AI Engine (Claude & Gemini)
        services.Configure<ClaudeOptions>(config.GetSection(ClaudeOptions.SectionName));
        services.Configure<GeminiOptions>(config.GetSection(GeminiOptions.SectionName));

        services.AddHttpClient<IClaudeService, ClaudeApiClient>();
        services.AddHttpClient<IGeminiService, GeminiApiClient>();
        services.AddScoped<IAiIntegrationService, AiIntegrationService>();

        // Per-request flag: set when a provider client serves a development stub, read by
        // AiController to stamp X-Ai-Simulated so sample data is never mistaken for an analysis.
        services.AddScoped<IAiSimulationScope, AiSimulationScope>();

        // Object Storage (ADR-0013)
        services.Configure<FileStorageOptions>(config.GetSection(FileStorageOptions.SectionName));
        services.AddSingleton<IAmazonS3>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<FileStorageOptions>>().Value;
            var s3Config = new AmazonS3Config
            {
                ServiceURL = options.ServiceUrl,
                ForcePathStyle = options.ForcePathStyle,
                AuthenticationRegion = options.Region
            };
            return new AmazonS3Client(options.AccessKey, options.SecretKey, s3Config);
        });
        services.AddScoped<IFileStorage, S3FileStorage>();

        // Myanmar Script Normalization (ADR-0009 / Requirement R2)
        services.AddSingleton<IMyanmarScriptNormalizer, MyanmarScriptNormalizer>();

        // Document Text Extraction & Resume Storage (Module 2 / Milestone 1 & 2)
        services.AddScoped<IDocumentTextExtractor, DocumentTextExtractor>();
        services.AddScoped<IResumeService, ResumeService>();
        // Module 2.3 — bulk CV upload, on ADR-0026's durable queue rather than a static
        // dictionary. There used to be a second, identical IBulkResumeService interface in
        // Application.Common.Interfaces registered alongside this one; nothing consumed it, so it
        // was deleted with the rewrite rather than carried forward.
        services.AddScoped<IBulkResumeService, BulkResumeService>();

        // Module 5 — Reporting & Analytics
        services.AddScoped<IAnalyticsService, AnalyticsService>();

        // Module 2 / Milestone 1 — Full-text Search Service
        services.AddScoped<ISearchService, SearchService>();

        // ADR-0026 — the read side of the outbox. Scoped, and it takes ICurrentUser +
        // IDepartmentAccess for the reason spelled out in DeliveryLogService: this table reaches
        // a department only through SubjectType/SubjectId, so ADR-0003 has to be applied by hand.
        services.AddScoped<IDeliveryLogService, DeliveryLogService>();

        return services;
    }
}

