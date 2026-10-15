using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RecruitOps.Api.Auth;
using RecruitOps.Application.Common;
using RecruitOps.Application.Interfaces;
using RecruitOps.Domain.Entities;
using RecruitOps.Domain.Enums;
using RecruitOps.Infrastructure.Persistence;
using RecruitOps.Infrastructure.Services.Delivery;
using RecruitOps.Infrastructure.Tenancy;
using Xunit;

namespace RecruitOps.Api.Tests;

/// <summary>The delivery worker from ADR-0026 §3, wired against the <b>real</b>
/// <see cref="CurrentTenant"/> rather than a stand-in.
///
/// <para>That matters: the thing most likely to break is tenant resolution in a scope that has no
/// HTTP request, and a test double for <c>ICurrentTenant</c> would quietly make that work.</para>
/// </summary>
public class OutboundMessageWorkerTests
{
    /// <summary>Minimal controllable clock. Deliberately not a new package — ADR-0026 chose to add
    /// none, and this needs eight lines.</summary>
    private sealed class TestClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 8, 20, 9, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan by) => _now = _now.Add(by);
    }

    /// <summary>Records what each handler run could actually see from inside its tenant scope.</summary>
    private sealed record HandlerObservation(Guid MessageTenantId, Guid ResolvedTenantId, int VisibleMessages);

    private sealed class RecordingHandler : IOutboundMessageHandler
    {
        private readonly AppDbContext _db;
        private readonly ICurrentTenant _tenant;
        private readonly Recorder _recorder;

        public RecordingHandler(AppDbContext db, ICurrentTenant tenant, Recorder recorder)
        {
            _db = db;
            _tenant = tenant;
            _recorder = recorder;
        }

        public OutboundMessageKind Kind => OutboundMessageKind.OfferSent;

        public Task<DeliveryOutcome> HandleAsync(OutboundMessage message, CancellationToken ct = default)
        {
            // No IgnoreQueryFilters: a handler is supposed to be able to query normally.
            var visible = _db.OutboundMessages.Count();
            _recorder.Observations.Add(new HandlerObservation(message.TenantId, _tenant.TenantId, visible));
            return Task.FromResult(_recorder.NextOutcome(message));
        }
    }

    private sealed class Recorder
    {
        public List<HandlerObservation> Observations { get; } = new();
        public Func<OutboundMessage, DeliveryOutcome> NextOutcome { get; set; } = _ => DeliveryOutcome.Sent();
    }

    private static (ServiceProvider Provider, Recorder Recorder, TestClock Clock) BuildHost(
        string databaseName, Action<OutboundDeliveryOptions>? configure = null)
    {
        var recorder = new Recorder();
        var clock = new TestClock();
        var options = new OutboundDeliveryOptions();
        configure?.Invoke(options);

        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.None));

        // No HttpContext is ever set on this accessor — exactly the worker's situation.
        services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        services.AddScoped<IAmbientTenantScope, AmbientTenantScope>();
        services.AddScoped<ICurrentTenant, CurrentTenant>();

        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(databaseName));

        services.AddSingleton(recorder);
        services.AddScoped<IOutboundMessageHandler, RecordingHandler>();
        services.AddSingleton<TimeProvider>(clock);
        services.AddSingleton<IOptions<OutboundDeliveryOptions>>(Options.Create(options));
        services.AddSingleton<OutboundMessageWorker>();

        return (services.BuildServiceProvider(), recorder, clock);
    }

    private static OutboundMessage Message(Guid tenantId, string recipient, DateTimeOffset due) => new()
    {
        TenantId = tenantId,
        Kind = OutboundMessageKind.OfferSent,
        Recipient = recipient,
        NextAttemptAt = due,
    };

    private static void Seed(ServiceProvider provider, params OutboundMessage[] messages)
    {
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.OutboundMessages.AddRange(messages);
        db.SaveChanges();
    }

    private static List<OutboundMessage> AllMessages(ServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return db.OutboundMessages.IgnoreQueryFilters().ToList();
    }

    // ---------------------------------------------------------------- tenancy

    /// <summary>The test ADR-0026 asks for by name.
    ///
    /// <para>Two messages, two tenants, one worker pass. Each handler run must resolve its own
    /// message's tenant and see only that tenant's rows. If the scope leaked, the second run would
    /// see two messages instead of one — which is what a cross-tenant read looks like from inside.</para>
    /// </summary>
    [Fact]
    public async Task Each_Message_Is_Handled_In_Its_Own_Tenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var (provider, recorder, clock) = BuildHost(Guid.NewGuid().ToString());

        Seed(provider,
            Message(tenantA, "a@alpha.test", clock.GetUtcNow()),
            Message(tenantB, "b@bravo.test", clock.GetUtcNow()));

        var handled = await provider.GetRequiredService<OutboundMessageWorker>().RunOnceAsync();

        Assert.Equal(2, handled);
        Assert.Equal(2, recorder.Observations.Count);

        foreach (var observation in recorder.Observations)
        {
            // The scope resolved the message's own tenant...
            Assert.Equal(observation.MessageTenantId, observation.ResolvedTenantId);
            // ...and the query filter confined the handler to it.
            Assert.Equal(1, observation.VisibleMessages);
        }

        Assert.Contains(recorder.Observations, o => o.MessageTenantId == tenantA);
        Assert.Contains(recorder.Observations, o => o.MessageTenantId == tenantB);
    }

    /// <summary>Both rows must actually be delivered, not just observed. A worker that resolved
    /// tenancy correctly but wrote the outcome outside the scope would leave rows Pending.</summary>
    [Fact]
    public async Task Outcomes_Are_Written_For_Every_Tenant()
    {
        var (provider, _, clock) = BuildHost(Guid.NewGuid().ToString());
        Seed(provider,
            Message(Guid.NewGuid(), "a@alpha.test", clock.GetUtcNow()),
            Message(Guid.NewGuid(), "b@bravo.test", clock.GetUtcNow()));

        await provider.GetRequiredService<OutboundMessageWorker>().RunOnceAsync();

        Assert.All(AllMessages(provider), m =>
        {
            Assert.Equal(OutboundMessageStatus.Sent, m.Status);
            Assert.NotNull(m.SentAt);
        });
    }

    // ---------------------------------------------------------------- claiming

    [Fact]
    public async Task A_Message_Not_Yet_Due_Is_Left_Alone()
    {
        var (provider, recorder, clock) = BuildHost(Guid.NewGuid().ToString());
        Seed(provider, Message(Guid.NewGuid(), "later@alpha.test", clock.GetUtcNow().AddMinutes(30)));

        var handled = await provider.GetRequiredService<OutboundMessageWorker>().RunOnceAsync();

        Assert.Equal(0, handled);
        Assert.Empty(recorder.Observations);
        Assert.Equal(OutboundMessageStatus.Pending, AllMessages(provider).Single().Status);
    }

    /// <summary>The visibility timeout. A second pass immediately afterwards must not re-send a
    /// message the first pass is still working on — this is what replaces an in-flight state.</summary>
    [Fact]
    public async Task Claiming_Hides_A_Message_From_The_Next_Pass()
    {
        var (provider, recorder, clock) = BuildHost(Guid.NewGuid().ToString());
        recorder.NextOutcome = _ => DeliveryOutcome.Retry("transport busy");

        Seed(provider, Message(Guid.NewGuid(), "busy@alpha.test", clock.GetUtcNow()));

        var worker = provider.GetRequiredService<OutboundMessageWorker>();
        Assert.Equal(1, await worker.RunOnceAsync());

        // Nothing has moved the clock, so the backoff has not elapsed.
        Assert.Equal(0, await worker.RunOnceAsync());
        Assert.Single(recorder.Observations);
    }

    // ---------------------------------------------------------------- outcomes

    [Fact]
    public async Task Suppressed_Is_Terminal_And_Is_Not_A_Failure()
    {
        var (provider, recorder, clock) = BuildHost(Guid.NewGuid().ToString());
        recorder.NextOutcome = _ => DeliveryOutcome.Suppressed("Candidate opted out");

        Seed(provider, Message(Guid.NewGuid(), "optedout@alpha.test", clock.GetUtcNow()));

        var worker = provider.GetRequiredService<OutboundMessageWorker>();
        await worker.RunOnceAsync();

        var message = AllMessages(provider).Single();
        Assert.Equal(OutboundMessageStatus.Suppressed, message.Status);
        Assert.Equal("Candidate opted out", message.LastError);
        Assert.Null(message.SentAt);

        // Terminal: it is never claimed again, however far the clock moves.
        clock.Advance(TimeSpan.FromDays(7));
        Assert.Equal(0, await worker.RunOnceAsync());
    }

    [Fact]
    public async Task Failed_Is_Terminal_And_Is_Not_Retried()
    {
        var (provider, recorder, clock) = BuildHost(Guid.NewGuid().ToString());
        recorder.NextOutcome = _ => DeliveryOutcome.Failed("Malformed address");

        Seed(provider, Message(Guid.NewGuid(), "bad@@alpha.test", clock.GetUtcNow()));

        var worker = provider.GetRequiredService<OutboundMessageWorker>();
        await worker.RunOnceAsync();
        clock.Advance(TimeSpan.FromDays(7));
        Assert.Equal(0, await worker.RunOnceAsync());

        var message = AllMessages(provider).Single();
        Assert.Equal(OutboundMessageStatus.Failed, message.Status);
        Assert.Equal("Malformed address", message.LastError);
    }

    [Fact]
    public async Task Retry_Backs_Off_And_Comes_Back()
    {
        var (provider, recorder, clock) = BuildHost(Guid.NewGuid().ToString(),
            o => o.BaseBackoff = TimeSpan.FromMinutes(1));
        recorder.NextOutcome = _ => DeliveryOutcome.Retry("timeout");

        Seed(provider, Message(Guid.NewGuid(), "slow@alpha.test", clock.GetUtcNow()));

        var worker = provider.GetRequiredService<OutboundMessageWorker>();
        await worker.RunOnceAsync();

        var afterFirst = AllMessages(provider).Single();
        Assert.Equal(OutboundMessageStatus.Pending, afterFirst.Status);
        Assert.Equal(1, afterFirst.Attempts);
        Assert.Equal("timeout", afterFirst.LastError);

        clock.Advance(TimeSpan.FromMinutes(2));
        Assert.Equal(1, await worker.RunOnceAsync());
        Assert.Equal(2, AllMessages(provider).Single().Attempts);
    }

    /// <summary>A poison message must stop. Retrying forever occupies the queue and buries real
    /// traffic in the delivery log.</summary>
    [Fact]
    public async Task Retrying_Stops_At_The_Attempt_Cap()
    {
        var (provider, recorder, clock) = BuildHost(Guid.NewGuid().ToString(), o =>
        {
            o.MaxAttempts = 3;
            o.BaseBackoff = TimeSpan.FromMinutes(1);
        });
        recorder.NextOutcome = _ => DeliveryOutcome.Retry("still broken");

        Seed(provider, Message(Guid.NewGuid(), "poison@alpha.test", clock.GetUtcNow()));

        var worker = provider.GetRequiredService<OutboundMessageWorker>();
        for (var i = 0; i < 5; i++)
        {
            await worker.RunOnceAsync();
            clock.Advance(TimeSpan.FromHours(2));
        }

        var message = AllMessages(provider).Single();
        Assert.Equal(OutboundMessageStatus.Failed, message.Status);
        Assert.Equal(3, message.Attempts);
        Assert.Contains("Gave up after 3 attempts", message.LastError);
    }

    /// <summary>A missing handler is a wiring mistake, not a bad message. Marking it Failed would
    /// burn the queue for a deployment that simply has not registered the handler yet.</summary>
    [Fact]
    public async Task An_Unhandled_Kind_Retries_Rather_Than_Failing_Immediately()
    {
        var (provider, _, clock) = BuildHost(Guid.NewGuid().ToString());

        // No handler is registered for ScheduledReport — only OfferSent.
        Seed(provider, new OutboundMessage
        {
            TenantId = Guid.NewGuid(),
            Kind = OutboundMessageKind.ScheduledReport,
            Recipient = "director@alpha.test",
            NextAttemptAt = clock.GetUtcNow(),
        });

        await provider.GetRequiredService<OutboundMessageWorker>().RunOnceAsync();

        var message = AllMessages(provider).Single();
        Assert.Equal(OutboundMessageStatus.Pending, message.Status);
        Assert.Contains("No handler is registered", message.LastError);
    }

    // -------------------------------------------------- security review follow-up
    // Raised by the security-reviewer pass on a2de09c. A failure BETWEEN claiming and recording
    // used to escape to the pass-level catch, which skipped Record() entirely — so the row's
    // attempt cap was never checked and it could be reclaimed forever, dodging the very cap that
    // exists to stop poison messages. It also abandoned the rest of the batch.

    /// <summary>A row whose own tenant is unusable must still be counted, and must still reach the
    /// cap. Before the fix this row was immortal.</summary>
    [Fact]
    public async Task A_Message_With_An_Unusable_Tenant_Still_Reaches_The_Attempt_Cap()
    {
        var (provider, _, clock) = BuildHost(Guid.NewGuid().ToString(), o =>
        {
            o.MaxAttempts = 2;
            o.BaseBackoff = TimeSpan.FromMinutes(1);
        });

        // TenantId stays empty: the stamp only fills from the ambient tenant, which is also empty
        // outside a request. EnterTenant rejects it, so the tenant scope can never be established.
        Seed(provider, Message(Guid.Empty, "malformed@alpha.test", clock.GetUtcNow()));

        var worker = provider.GetRequiredService<OutboundMessageWorker>();
        for (var i = 0; i < 4; i++)
        {
            await worker.RunOnceAsync();
            clock.Advance(TimeSpan.FromHours(2));
        }

        var message = AllMessages(provider).Single();
        Assert.Equal(OutboundMessageStatus.Failed, message.Status);
        Assert.Equal(2, message.Attempts);
    }

    /// <summary>One bad row must not abandon the rest of the batch. Before the fix the exception
    /// escaped the foreach, so every message ordered after it was skipped for that pass.</summary>
    [Fact]
    public async Task One_Unusable_Message_Does_Not_Abandon_The_Rest_Of_The_Batch()
    {
        var (provider, recorder, clock) = BuildHost(Guid.NewGuid().ToString());
        var healthyTenant = Guid.NewGuid();

        // The malformed row is claimed first — same due time, but seeded first.
        Seed(provider,
            Message(Guid.Empty, "malformed@alpha.test", clock.GetUtcNow()),
            Message(healthyTenant, "healthy@alpha.test", clock.GetUtcNow()));

        await provider.GetRequiredService<OutboundMessageWorker>().RunOnceAsync();

        // The healthy message was still delivered.
        Assert.Single(recorder.Observations);
        Assert.Equal(healthyTenant, recorder.Observations[0].MessageTenantId);

        var all = AllMessages(provider);
        Assert.Equal(OutboundMessageStatus.Sent, all.Single(m => m.TenantId == healthyTenant).Status);
        Assert.Equal(OutboundMessageStatus.Pending, all.Single(m => m.TenantId == Guid.Empty).Status);
    }

    /// <summary>A handler that throws must not take the worker down, and must not lose the
    /// message. Silence is this feature's worst failure mode.</summary>
    [Fact]
    public async Task A_Throwing_Handler_Is_Treated_As_Retryable()
    {
        var (provider, recorder, clock) = BuildHost(Guid.NewGuid().ToString());
        recorder.NextOutcome = _ => throw new InvalidOperationException("SMTP exploded");

        Seed(provider, Message(Guid.NewGuid(), "boom@alpha.test", clock.GetUtcNow()));

        await provider.GetRequiredService<OutboundMessageWorker>().RunOnceAsync();

        var message = AllMessages(provider).Single();
        Assert.Equal(OutboundMessageStatus.Pending, message.Status);
        Assert.Equal("SMTP exploded", message.LastError);
    }
}
