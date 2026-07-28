using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RecruitOps.Application.Common;
using RecruitOps.Application.Interfaces;
using RecruitOps.Domain.Entities;
using RecruitOps.Infrastructure.Persistence;
using RecruitOps.Infrastructure.Services;

namespace RecruitOps.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<AppDbContext>(opt =>
            opt.UseNpgsql(config.GetConnectionString("Default")));

        services.AddSingleton(TimeProvider.System);

        // Auth
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();

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

        // TODO: module services as they are built (offers, analytics).
        return services;
    }
}
