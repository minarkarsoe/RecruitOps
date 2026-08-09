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
using RecruitOps.Infrastructure.Services.FileStorage;
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

        // Singleton: the failure counters have to outlive the request that recorded them,
        // which is the entire point.
        services.AddSingleton<ILoginThrottle, LoginThrottle>();

        services.AddScoped<IDepartmentService, DepartmentService>();

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
        services.AddScoped<IBulkResumeService, BulkResumeService>();
        services.AddScoped<Application.Common.Interfaces.IBulkResumeService, BulkResumeService>();

        // TODO: module services as they are built (offers, analytics).
        return services;
    }
}

