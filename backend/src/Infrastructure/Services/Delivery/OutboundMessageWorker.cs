using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RecruitOps.Application.Common;
using RecruitOps.Application.Interfaces;
using RecruitOps.Domain.Entities;
using RecruitOps.Domain.Enums;
using RecruitOps.Infrastructure.Persistence;

namespace RecruitOps.Infrastructure.Services.Delivery;

/// <summary>Drains the <see cref="OutboundMessage"/> queue (ADR-0026 §3).
///
/// <para>One in-process worker, because ADR-0004 ships one instance per company — which removes
/// the problem a distributed scheduler exists to solve.</para>
///
/// <para><b>Claiming.</b> A due row is claimed by pushing <see cref="OutboundMessage.NextAttemptAt"/>
/// forward by <see cref="OutboundDeliveryOptions.VisibilityTimeout"/> and incrementing
/// <see cref="OutboundMessage.Attempts"/>. It is never marked "in flight", so a process that dies
/// mid-send loses nothing: the row is still Pending and becomes due again by itself.</para>
///
/// <para><b>Tenancy.</b> The claim query is the one place in this system that legitimately reads
/// across tenants — the worker has no request, so it must <c>IgnoreQueryFilters()</c> or the
/// queue looks empty however full it is. Each message is then handled in <b>its own DI scope</b>
/// whose tenant is entered from the claimed row, so handlers query with the filters on. See
/// <see cref="IAmbientTenantScope"/>.</para>
/// </summary>
public sealed class OutboundMessageWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _clock;
    private readonly OutboundDeliveryOptions _options;
    private readonly ILogger<OutboundMessageWorker> _logger;

    public OutboundMessageWorker(
        IServiceScopeFactory scopeFactory,
        TimeProvider clock,
        IOptions<OutboundDeliveryOptions> options,
        ILogger<OutboundMessageWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // A pass that throws must not kill the worker — a dead BackgroundService is
                // silent, and silence is this feature's worst failure mode.
                _logger.LogError(ex, "Outbound delivery pass failed. Continuing.");
            }

            try
            {
                await Task.Delay(_options.PollInterval, _clock, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>One pass: claim what is due, handle each, record what happened.
    /// <para>Public so tests drive it directly. Asserting on a timer is how you get a suite that
    /// fails once a week on a slow machine.</para>
    /// <returns>How many messages were handled.</returns></summary>
    public async Task<int> RunOnceAsync(CancellationToken ct = default)
    {
        var claimed = await ClaimDueAsync(ct);

        foreach (var message in claimed)
        {
            await HandleClaimedAsync(message, ct);
        }

        return claimed.Count;
    }

    /// <summary>Takes due messages and pushes them out of sight for the visibility timeout.
    /// <para>⚠️ <c>IgnoreQueryFilters()</c> is mandatory here and nowhere else in this file: the
    /// worker runs outside any request, so the tenant filter would match nothing.</para></summary>
    private async Task<List<OutboundMessage>> ClaimDueAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var now = _clock.GetUtcNow();

        var due = await db.OutboundMessages
            .IgnoreQueryFilters()
            .Where(m => m.Status == OutboundMessageStatus.Pending && m.NextAttemptAt <= now)
            .OrderBy(m => m.NextAttemptAt)
            .Take(_options.BatchSize)
            .ToListAsync(ct);

        if (due.Count == 0)
        {
            return due;
        }

        foreach (var message in due)
        {
            message.Attempts += 1;
            message.NextAttemptAt = now + _options.VisibilityTimeout;
        }

        await db.SaveChangesAsync(ct);
        return due;
    }

    private async Task HandleClaimedAsync(OutboundMessage claimed, CancellationToken ct)
    {
        // A scope per message. IAmbientTenantScope refuses a second EnterTenant, so reusing one
        // across messages would throw rather than silently run as the previous tenant.
        using var scope = _scopeFactory.CreateScope();
        scope.ServiceProvider.GetRequiredService<IAmbientTenantScope>().EnterTenant(claimed.TenantId);

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Re-read inside the tenant scope. This goes through the query filter on purpose: if the
        // tenant was not established correctly the row is simply not found, and the failure is a
        // logged miss rather than a cross-tenant write.
        var message = await db.OutboundMessages.FirstOrDefaultAsync(m => m.Id == claimed.Id, ct);
        if (message is null)
        {
            _logger.LogError(
                "Claimed message {MessageId} was not readable inside tenant {TenantId}. " +
                "The tenant scope is not being established correctly.",
                claimed.Id, claimed.TenantId);
            return;
        }

        var outcome = await InvokeHandlerAsync(scope.ServiceProvider, message, ct);
        Record(message, outcome);

        await db.SaveChangesAsync(ct);
    }

    private async Task<DeliveryOutcome> InvokeHandlerAsync(
        IServiceProvider provider, OutboundMessage message, CancellationToken ct)
    {
        var handler = provider.GetServices<IOutboundMessageHandler>()
            .FirstOrDefault(h => h.Kind == message.Kind);

        if (handler is null)
        {
            // Retry rather than Failed: the usual cause is a deployment that has not registered
            // the handler yet, and burning the queue for a wiring mistake is worse than waiting.
            // The attempt cap still stops it circulating forever.
            _logger.LogError("No handler registered for {Kind} (message {MessageId}).",
                message.Kind, message.Id);
            return DeliveryOutcome.Retry($"No handler is registered for {message.Kind}.");
        }

        try
        {
            return await handler.HandleAsync(message, ct);
        }
        catch (Exception ex)
        {
            // A handler that throws is treated as retryable. A handler that knows its failure is
            // permanent says so by returning Failed.
            _logger.LogWarning(ex, "Handler for {Kind} threw on message {MessageId}.",
                message.Kind, message.Id);
            return DeliveryOutcome.Retry(ex.Message);
        }
    }

    private void Record(OutboundMessage message, DeliveryOutcome outcome)
    {
        var now = _clock.GetUtcNow();

        switch (outcome.Kind)
        {
            case DeliveryOutcomeKind.Sent:
                message.Status = OutboundMessageStatus.Sent;
                message.SentAt = now;
                message.LastError = null;
                break;

            case DeliveryOutcomeKind.Suppressed:
                message.Status = OutboundMessageStatus.Suppressed;
                // Not an error field for this case — it is the reason it was correctly not sent,
                // and the delivery log renders it neutrally.
                message.LastError = outcome.Detail;
                break;

            case DeliveryOutcomeKind.Failed:
                message.Status = OutboundMessageStatus.Failed;
                message.LastError = outcome.Detail;
                break;

            case DeliveryOutcomeKind.Retry:
                message.LastError = outcome.Detail;
                if (message.Attempts >= _options.MaxAttempts)
                {
                    message.Status = OutboundMessageStatus.Failed;
                    message.LastError =
                        $"Gave up after {message.Attempts} attempts. Last error: {outcome.Detail}";
                }
                else
                {
                    message.Status = OutboundMessageStatus.Pending;
                    message.NextAttemptAt = now + BackoffFor(message.Attempts);
                }
                break;
        }
    }

    /// <summary>Exponential, capped. Attempt 1 waits the base delay, then it doubles.</summary>
    private TimeSpan BackoffFor(int attempts)
    {
        var exponent = Math.Max(0, attempts - 1);
        // Cap the exponent before shifting so a long-lived message cannot overflow the multiply.
        var factor = Math.Pow(2, Math.Min(exponent, 16));
        var delay = _options.BaseBackoff * factor;
        return delay > _options.MaxBackoff ? _options.MaxBackoff : delay;
    }
}
