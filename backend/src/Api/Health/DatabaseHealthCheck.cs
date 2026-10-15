using Microsoft.Extensions.Diagnostics.HealthChecks;
using RecruitOps.Infrastructure.Persistence;

namespace RecruitOps.Api.Health;

/// <summary>Readiness: can this instance reach its database? (ADR-0004 — one instance per company,
/// often on a customer's own server, where "is it up" is the first support question.)
/// <para>The result carries no exception text. The endpoints are anonymous, so a connection
/// string fragment or a host name in an error message would be published to anyone who asks;
/// the detail goes to the log instead.</para></summary>
public sealed class DatabaseHealthCheck : IHealthCheck
{
    private readonly AppDbContext _db;
    private readonly ILogger<DatabaseHealthCheck> _logger;

    public DatabaseHealthCheck(AppDbContext db, ILogger<DatabaseHealthCheck> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            if (await _db.Database.CanConnectAsync(cancellationToken))
                return HealthCheckResult.Healthy();

            // CanConnectAsync swallows the provider's exception and answers false, so this is
            // the only trace an operator gets of why readiness failed.
            _logger.LogWarning("Readiness check could not reach the database");
            return HealthCheckResult.Unhealthy("Database unreachable.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Readiness check could not reach the database");
            return HealthCheckResult.Unhealthy("Database unreachable.");
        }
    }
}
