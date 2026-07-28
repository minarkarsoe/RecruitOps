using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using RecruitOps.Application.Common;

namespace RecruitOps.Infrastructure.Persistence;

/// <summary>Design-time factory used by `dotnet ef` when generating migrations.
/// <para>Without this, EF would have to boot the API host to construct AppDbContext —
/// which would also run startup migration logic and try to reach a real database while
/// merely scaffolding a migration. This keeps migration generation self-contained.</para>
/// <para>The connection string here is only used to determine the provider and generate
/// SQL; `dotnet ef migrations add` does not connect to the database.</para></summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Host=localhost;Port=5432;Database=recruitops;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new AppDbContext(options, new DesignTimeTenant());
    }

    /// <summary>No tenant context at design time; query filters are not evaluated
    /// while scaffolding.</summary>
    private sealed class DesignTimeTenant : ICurrentTenant
    {
        public Guid TenantId => Guid.Empty;
    }
}
