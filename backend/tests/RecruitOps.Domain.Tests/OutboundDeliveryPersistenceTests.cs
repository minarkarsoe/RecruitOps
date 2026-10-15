using Microsoft.EntityFrameworkCore;
using RecruitOps.Application.Common;
using RecruitOps.Domain.Entities;
using RecruitOps.Domain.Enums;
using RecruitOps.Infrastructure.Persistence;
using Xunit;

namespace RecruitOps.Domain.Tests;

/// <summary>Persistence contract for ADR-0026's two new tables.
///
/// <para>These entities have no behaviour to unit-test — they are rows. What is worth pinning is
/// the seam the ADR is about: <b>the queue is tenant-filtered, and a background worker sees an
/// empty queue unless it deliberately bypasses that filter.</b> That is a trap with no error
/// message — the worker would simply never send anything, and every test that runs inside a
/// request would keep passing.</para>
/// </summary>
public class OutboundDeliveryPersistenceTests
{
    /// <summary>Stands in for the ambient tenant. <see cref="Guid.Empty"/> is not a placeholder
    /// here — it is exactly what <c>CurrentTenant</c> returns when there is no HTTP request,
    /// which is the situation the background worker runs in.</summary>
    private sealed class TestTenant : ICurrentTenant
    {
        public Guid TenantId { get; set; }
    }

    private static AppDbContext CreateDbContext(TestTenant tenant, string databaseName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new AppDbContext(options, tenant);
    }

    private static OutboundMessage MessageFor(Guid tenantId, string recipient) => new()
    {
        TenantId = tenantId,
        Kind = OutboundMessageKind.OfferSent,
        Recipient = recipient,
        SubjectType = "Offer",
        SubjectId = Guid.NewGuid(),
        NextAttemptAt = DateTimeOffset.UtcNow,
    };

    [Fact]
    public void OutboundMessage_Is_Tenant_Filtered()
    {
        var db = Guid.NewGuid().ToString();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var seeder = new TestTenant { TenantId = tenantA };
        using (var ctx = CreateDbContext(seeder, db))
        {
            ctx.OutboundMessages.Add(MessageFor(tenantA, "a@alpha.test"));
            ctx.OutboundMessages.Add(MessageFor(tenantB, "b@bravo.test"));
            ctx.SaveChanges();
        }

        using (var ctx = CreateDbContext(new TestTenant { TenantId = tenantB }, db))
        {
            var visible = ctx.OutboundMessages.ToList();
            Assert.Single(visible);
            Assert.Equal("b@bravo.test", visible[0].Recipient);
        }
    }

    [Fact]
    public void ScheduledJob_Is_Tenant_Filtered()
    {
        var db = Guid.NewGuid().ToString();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        using (var ctx = CreateDbContext(new TestTenant { TenantId = tenantA }, db))
        {
            ctx.ScheduledJobs.Add(new ScheduledJob
            {
                TenantId = tenantA,
                Kind = ScheduledJobKind.ScheduledReport,
                Recurrence = ScheduledJobRecurrence.Weekly,
                DayOfWeek = 1,
                TimeOfDayMinutes = 9 * 60,
                TimeZoneId = "Asia/Yangon",
                NextRunAt = DateTimeOffset.UtcNow,
            });
            ctx.ScheduledJobs.Add(new ScheduledJob
            {
                TenantId = tenantB,
                Kind = ScheduledJobKind.ScheduledReport,
                Recurrence = ScheduledJobRecurrence.Daily,
                TimeOfDayMinutes = 8 * 60,
                TimeZoneId = "Asia/Yangon",
                NextRunAt = DateTimeOffset.UtcNow,
            });
            ctx.SaveChanges();
        }

        using (var ctx = CreateDbContext(new TestTenant { TenantId = tenantA }, db))
        {
            var visible = ctx.ScheduledJobs.ToList();
            Assert.Single(visible);
            Assert.Equal(ScheduledJobRecurrence.Weekly, visible[0].Recurrence);
        }
    }

    /// <summary>The reason ADR-0026 §4 exists, written as a test rather than as a comment.
    ///
    /// <para>A background worker has no HTTP request, so the ambient tenant is
    /// <see cref="Guid.Empty"/> and the queue looks empty however full it is. Nothing throws;
    /// the product just silently stops sending. This asserts both halves — that the plain query
    /// finds nothing, and that <c>IgnoreQueryFilters()</c> is what a worker must use to claim.</para>
    ///
    /// <para>If this test ever fails because the plain query started returning rows, the tenant
    /// filter has been dropped from these tables and the worker is no longer the only thing that
    /// can read across tenants. That is a finding, not a test to update.</para>
    /// </summary>
    [Fact]
    public void Worker_Without_A_Request_Sees_An_Empty_Queue_Until_It_Ignores_The_Filter()
    {
        var db = Guid.NewGuid().ToString();
        var tenantA = Guid.NewGuid();

        using (var ctx = CreateDbContext(new TestTenant { TenantId = tenantA }, db))
        {
            ctx.OutboundMessages.Add(MessageFor(tenantA, "candidate@alpha.test"));
            ctx.SaveChanges();
        }

        // Guid.Empty — no HTTP request, exactly as the worker runs.
        using var workerCtx = CreateDbContext(new TestTenant { TenantId = Guid.Empty }, db);

        Assert.Empty(workerCtx.OutboundMessages.ToList());

        var claimable = workerCtx.OutboundMessages
            .IgnoreQueryFilters()
            .Where(m => m.Status == OutboundMessageStatus.Pending)
            .ToList();

        Assert.Single(claimable);
        // And the claimed row carries the tenant the worker must then establish for the
        // handler's scope, so the handler itself never needs IgnoreQueryFilters().
        Assert.Equal(tenantA, claimable[0].TenantId);
    }

    /// <summary>An enqueue must be able to name a tenant other than the ambient one — the worker
    /// enqueues follow-ups, and a scheduled job produces messages for the tenant that owns it.
    /// <c>StampTenantAndTimestamps</c> only fills <c>TenantId</c> when it is empty, and this pins
    /// that: a stamp that overwrote a deliberate assignment would silently re-home the message.</summary>
    [Fact]
    public void Explicit_TenantId_Survives_The_Stamp()
    {
        var db = Guid.NewGuid().ToString();
        var ambient = Guid.NewGuid();
        var owner = Guid.NewGuid();

        using (var ctx = CreateDbContext(new TestTenant { TenantId = ambient }, db))
        {
            ctx.OutboundMessages.Add(MessageFor(owner, "owner@bravo.test"));
            ctx.SaveChanges();
        }

        using var check = CreateDbContext(new TestTenant { TenantId = Guid.Empty }, db);
        var row = check.OutboundMessages.IgnoreQueryFilters().Single();
        Assert.Equal(owner, row.TenantId);
    }

    /// <summary>Defaults matter here because the worker's claim query keys on them: a message
    /// that did not default to Pending would never be picked up, and one defaulting to something
    /// else would be sent by surprise.</summary>
    [Fact]
    public void OutboundMessage_Defaults_Are_Claimable()
    {
        var message = new OutboundMessage();

        Assert.Equal(OutboundMessageStatus.Pending, message.Status);
        Assert.Equal(0, message.Attempts);
        Assert.Null(message.SentAt);
        Assert.Null(message.LastError);
        Assert.Equal("{}", message.PayloadJson);
    }

    [Fact]
    public void ScheduledJob_Defaults_To_Active_With_No_Assumed_TimeZone()
    {
        var job = new ScheduledJob();

        Assert.True(job.IsActive);
        // Deliberately empty: guessing a company's timezone is the bug this avoids, so the
        // caller must supply one. See the remarks on ScheduledJob.TimeZoneId.
        Assert.Equal(string.Empty, job.TimeZoneId);
    }
}
