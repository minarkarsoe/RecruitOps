using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace RecruitOps.Infrastructure.Persistence;

/// <summary>Applies pending EF migrations at startup.
/// <para>Required by ADR-0004: installs run on customer servers, sometimes on-premise,
/// and cannot be upgraded by asking someone to run EF tooling by hand.</para></summary>
public static class DatabaseStartup
{
    public const string AutoMigrateKey = "Database:AutoMigrateOnStartup";

    public static async Task MigrateAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("Database");
        var db = sp.GetRequiredService<AppDbContext>();

        // Tests use the in-memory provider, which has no migrations.
        if (!db.Database.IsRelational())
        {
            logger.LogDebug("Non-relational provider in use; skipping migrations.");
            return;
        }

        // Read via the indexer rather than GetValue<T>: that extension lives in
        // Microsoft.Extensions.Configuration.Binder, which this project does not
        // reference (only ...Configuration.Abstractions). Defaults to enabled —
        // only an explicit "false" turns migrations off.
        var config = sp.GetRequiredService<IConfiguration>();
        if (string.Equals(config[AutoMigrateKey], "false", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("{Key} is false — skipping migrations. Apply them manually.", AutoMigrateKey);
            return;
        }

        var pending = (await db.Database.GetPendingMigrationsAsync(ct)).ToList();
        if (pending.Count == 0)
        {
            logger.LogInformation("Database schema is up to date.");
            return;
        }

        logger.LogInformation("Applying {Count} pending migration(s): {Migrations}",
            pending.Count, string.Join(", ", pending));

        // Deliberately not wrapped in try/catch: a half-migrated database must not
        // start serving traffic. Fail fast and loudly.
        await db.Database.MigrateAsync(ct);

        logger.LogInformation("Database migrated successfully.");
    }
}
