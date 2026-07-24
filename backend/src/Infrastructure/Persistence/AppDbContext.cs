using Microsoft.EntityFrameworkCore;
using RecruitOps.Application.Common;
using RecruitOps.Domain.Common;
using RecruitOps.Domain.Entities;

namespace RecruitOps.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    private readonly ICurrentTenant _tenant;

    public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentTenant tenant)
        : base(options) => _tenant = tenant;

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<JobChannelPost> JobChannelPosts => Set<JobChannelPost>();
    public DbSet<Candidate> Candidates => Set<Candidate>();
    public DbSet<Application> Applications => Set<Application>();
    public DbSet<PortalLink> PortalLinks => Set<PortalLink>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Tenant isolation (Module 1): global query filter on every ITenantScoped entity.
        builder.Entity<User>().HasQueryFilter(e => e.TenantId == _tenant.TenantId);
        builder.Entity<Client>().HasQueryFilter(e => e.TenantId == _tenant.TenantId);
        builder.Entity<Contract>().HasQueryFilter(e => e.TenantId == _tenant.TenantId);
        builder.Entity<Job>().HasQueryFilter(e => e.TenantId == _tenant.TenantId);
        builder.Entity<JobChannelPost>().HasQueryFilter(e => e.TenantId == _tenant.TenantId);
        builder.Entity<Candidate>().HasQueryFilter(e => e.TenantId == _tenant.TenantId);
        builder.Entity<Application>().HasQueryFilter(e => e.TenantId == _tenant.TenantId);
        builder.Entity<PortalLink>().HasQueryFilter(e => e.TenantId == _tenant.TenantId);

        // TODO: configure relationships, indexes (Candidate Email/Phone for dedup — Module 4),
        // and enum-to-string conversions.
        base.OnModelCreating(builder);
    }
}
