using System.Net;
using System.Net.Http.Json;
using RecruitOps.Api.Auth;
using RecruitOps.Application.DTOs;
using RecruitOps.Domain.Enums;
using RecruitOps.Infrastructure.Services;
using Xunit;

namespace RecruitOps.Api.Tests;

/// <summary>Failed sign-ins for emails that match no account (security review of ADR-0027).
/// <para>M1: the per-account throttle is keyed on the email, so an attacker trying a fresh email
/// each time was never throttled and wrote one permanent, undeletable row per attempt.</para>
/// <para>M2: the field accepts anything with an "@" — and people paste passwords into it — and
/// that text was kept forever and shown to auditors.</para></summary>
public class UnknownSignInAuditGateTests
{
    /// <summary>The same hand-rolled clock the worker tests use — no testing package needed.</summary>
    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.Parse("2026-10-15T09:00:00Z");
        public override DateTimeOffset GetUtcNow() => Now;
        public void Advance(TimeSpan by) => Now += by;
    }

    private static (UnknownSignInAuditGate Gate, TestClock Clock) NewGate()
    {
        var clock = new TestClock();
        return (new UnknownSignInAuditGate(clock), clock);
    }

    [Fact]
    public void One_Entry_Per_Client_Per_Window_Carrying_The_Count_In_Between()
    {
        var (gate, clock) = NewGate();

        Assert.Equal((true, 0), gate.Admit("203.0.113.7"));
        for (var i = 0; i < 3; i++) Assert.False(gate.Admit("203.0.113.7").Record);

        clock.Advance(UnknownSignInAuditGate.Window);
        Assert.Equal((true, 3), gate.Admit("203.0.113.7"));
    }

    [Fact]
    public void Clients_Are_Counted_Apart_And_An_IPv6_64_Is_One_Client()
    {
        var (gate, _) = NewGate();
        Assert.True(gate.Admit("203.0.113.7").Record);
        Assert.True(gate.Admit("203.0.113.8").Record);

        Assert.True(gate.Admit("2001:db8:1:2::1").Record);
        Assert.False(gate.Admit("2001:db8:1:2:ffff::9").Record); // same /64
        Assert.True(gate.Admit("2001:db8:1:3::1").Record);       // the next /64
    }

    [Fact]
    public void Memory_Is_Bounded_However_Many_Clients_Try()
    {
        // Review L4: entries with a suppressed count were never pruned, and pruning only ran on
        // the recording path — which stops once the hourly budget is spent.
        var (gate, clock) = NewGate();
        for (var i = 0; i < UnknownSignInAuditGate.MaxTrackedClients + 5_000; i++)
            gate.Admit($"10.{i / 65536 % 256}.{i / 256 % 256}.{i % 256}");

        Assert.True(gate.TrackedClients <= UnknownSignInAuditGate.MaxTrackedClients);

        clock.Advance(UnknownSignInAuditGate.Window + TimeSpan.FromMinutes(2));
        gate.Admit("192.0.2.200");
        Assert.True(gate.TrackedClients < 10);
    }

    [Fact]
    public void The_Whole_Install_Records_At_Most_So_Many_An_Hour()
    {
        var (gate, clock) = NewGate();
        for (var i = 0; i < UnknownSignInAuditGate.MaxEntriesPerHour; i++)
            Assert.True(gate.Admit($"198.51.100.{i}").Record);

        Assert.False(gate.Admit("192.0.2.1").Record);

        clock.Advance(TimeSpan.FromHours(1));
        Assert.True(gate.Admit("192.0.2.2").Record);
    }

    [Theory]
    [InlineData("aung@company.mm", true)]
    [InlineData("P@ssw0rd", false)]                // a password with an "@" in it
    [InlineData("secret@intranet", false)]         // no dot in the domain
    [InlineData("aung @company.mm", false)]
    [InlineData("aung@company.mm\n", false)]
    [InlineData("aung\u202E@company.mm", false)]   // a bidi override, to reverse what an auditor reads
    [InlineData("\"a<b>c\"@company.mm", false)]     // a quoted local part lets punctuation through
    [InlineData("=cmd@company.mm", false)]          // a formula, waiting for a CSV export
    [InlineData("\u0645\u0633\u062A@company.mm", false)] // letters a reader cannot check at a glance
    [InlineData("tin.tin+hr@company.com.mm", true)]
    public void Only_A_Plain_Email_Address_Is_Kept(string typed, bool kept) =>
        Assert.Equal(kept ? typed : null, UnknownSignInAuditGate.PlainEmailOrNull(typed));
}

/// <summary>The same rules through the API. A factory of its own: the gate is per install, and
/// another class's unknown-email attempt would use up this one's window.</summary>
public class UnknownSignInAuditTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;

    public UnknownSignInAuditTests(CustomWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task What_Was_Typed_Is_Not_Kept_Unless_It_Is_An_Email_And_Repeats_Are_Rolled_Up()
    {
        const string typed = "Correct-Horse@Battery9";
        for (var i = 0; i < 3; i++)
        {
            var res = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login",
                new LoginRequest { Email = typed, Password = "whatever-123" });
            Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        }

        var admin = new Module3Scenario(_factory).Client(Roles.Admin, _factory.AdminUserId);
        var log = await admin.GetFromJsonAsync<PagedResult<AuditLogEntryDto>>("/api/audit-log?category=Refused&pageSize=100");

        var entry = Assert.Single(log!.Items, e => e.Action == AuditAction.SignInFailed);
        Assert.Equal("(not an email address)", entry.ActorName);
        Assert.DoesNotContain(typed, entry.Summary + entry.Details?.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }
}
