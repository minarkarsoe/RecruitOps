using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RecruitOps.Api.Auth;
using RecruitOps.Application.Common;
using RecruitOps.Application.DTOs;
using RecruitOps.Application.Interfaces;
using RecruitOps.Domain.Entities;
using RecruitOps.Domain.Enums;
using RecruitOps.Infrastructure.Persistence;
using RecruitOps.Infrastructure.Services.Delivery.Handlers;
using RecruitOps.Infrastructure.Tenancy;
using Xunit;

namespace RecruitOps.Api.Tests;

/// <summary>Module 3.2 — the first real handler behind ADR-0026.
///
/// <para>Run inside a tenant scope entered the way the worker enters it, against the real
/// <see cref="CurrentTenant"/>. The transport is a capturing double because what is under test is
/// what the candidate is <b>told</b> and when they are deliberately <b>not</b> told — SMTP itself
/// is pinned separately in <c>SmtpEmailSenderTests</c>.</para>
/// </summary>
public class InterviewInvitationHandlerTests
{
    /// <summary>Fixed at a Monday morning in Yangon: 09:00 on 3 August 2026 is 02:30 UTC.</summary>
    private static readonly DateTimeOffset Slot = new(2026, 8, 3, 2, 30, 0, TimeSpan.Zero);

    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 7, 30, 9, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class CapturingEmailSender : IEmailSender
    {
        public List<EmailMessage> Sent { get; } = new();

        /// <summary>Set to make the transport refuse. Null means it accepts.</summary>
        public Func<EmailMessage, EmailDeliveryException?> Refuse { get; set; } = _ => null;

        public Task SendAsync(EmailMessage message, CancellationToken ct = default)
        {
            var refusal = Refuse(message);
            if (refusal is not null) throw refusal;

            Sent.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class Fixture
    {
        public required ServiceProvider Provider { get; init; }
        public required CapturingEmailSender Email { get; init; }
        public required TestClock Clock { get; init; }
        public required Guid TenantId { get; init; }

        /// <summary>Runs the handler the way the worker does: a fresh scope, the tenant entered
        /// from the claimed row, then resolve. Nothing here calls IgnoreQueryFilters.</summary>
        public async Task<DeliveryOutcome> HandleAsync(OutboundMessage message)
        {
            using var scope = Provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<IAmbientTenantScope>().EnterTenant(message.TenantId);
            return await scope.ServiceProvider
                .GetRequiredService<InterviewInvitationHandler>()
                .HandleAsync(message);
        }
    }

    private static Fixture BuildFixture()
    {
        var email = new CapturingEmailSender();
        var clock = new TestClock();

        // Named once and captured. AddDbContext builds its options per scope, so calling
        // Guid.NewGuid() inside the lambda would give every scope its own database — the seed
        // would land somewhere the handler never looks.
        var databaseName = Guid.NewGuid().ToString();

        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.None));
        services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        services.AddScoped<IAmbientTenantScope, AmbientTenantScope>();
        services.AddScoped<ICurrentTenant, CurrentTenant>();
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(databaseName));
        services.AddSingleton<IEmailSender>(email);
        services.AddSingleton<TimeProvider>(clock);
        services.AddScoped<InterviewInvitationHandler>();

        return new Fixture
        {
            Provider = services.BuildServiceProvider(),
            Email = email,
            Clock = clock,
            TenantId = Guid.NewGuid(),
        };
    }

    /// <summary>A whole scheduled round: company, posting, candidate, application, interview, and
    /// the queued invitation that points at it.</summary>
    private static OutboundMessage Seed(
        Fixture fixture,
        string? candidateEmail = "aye.aye@example.test",
        string? candidateName = "Aye Aye Mon",
        string? companyTimeZone = "Asia/Yangon",
        InterviewStatus status = InterviewStatus.Scheduled,
        InterviewMode mode = InterviewMode.Video,
        string? location = "https://meet.example.test/abc",
        DateTimeOffset? scheduledStart = null,
        bool isReschedule = false,
        Guid? subjectId = null)
    {
        using var scope = fixture.Provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tenantId = fixture.TenantId;

        // Company is keyed by the tenant id — one row per deployment (ADR-0004).
        var company = new Company
        {
            Id = tenantId,
            Name = "Yoma Bank",
            Slug = "yoma",
            TimeZoneId = companyTimeZone,
        };

        var candidate = new Candidate
        {
            TenantId = tenantId,
            FullName = candidateName ?? string.Empty,
            Email = candidateEmail,
        };

        var posting = new JobPosting
        {
            TenantId = tenantId,
            DepartmentId = Guid.NewGuid(),
            RequisitionId = Guid.NewGuid(),
            Title = "Collections Officer (Field)",
            Description = "Internal JD.",
        };

        var application = new JobApplication
        {
            TenantId = tenantId,
            JobPostingId = posting.Id,
            CandidateId = candidate.Id,
            Status = PipelineStatus.Interview,
            AppliedAt = new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero),
        };

        var interview = new Interview
        {
            TenantId = tenantId,
            JobApplicationId = application.Id,
            Round = 1,
            ScheduledStart = scheduledStart ?? Slot,
            DurationMinutes = 60,
            Mode = mode,
            Location = location,
            Status = status,
        };

        var message = new OutboundMessage
        {
            TenantId = tenantId,
            Kind = OutboundMessageKind.InterviewInvitation,
            Recipient = candidateEmail ?? string.Empty,
            SubjectType = nameof(Interview),
            SubjectId = subjectId ?? interview.Id,
            PayloadJson = JsonSerializer.Serialize(
                new InterviewInvitationPayload(companyTimeZone ?? TimeZoneInfo.Utc.Id, isReschedule)),
            NextAttemptAt = fixture.Clock.GetUtcNow(),
        };

        db.Companies.Add(company);
        db.Candidates.Add(candidate);
        db.JobPostings.Add(posting);
        db.JobApplications.Add(application);
        db.Interviews.Add(interview);
        db.OutboundMessages.Add(message);
        db.SaveChanges();

        return message;
    }

    // ---------------------------------------------------------------- the happy path

    /// <summary>The one thing the candidate acts on is the time, and it has to be their time.
    ///
    /// <para>UTC+06:30 is far enough from UTC that getting this wrong is not a cosmetic error: the
    /// same instant rendered in UTC reads as 02:30 — the wrong half of the day.</para></summary>
    [Fact]
    public async Task The_Slot_Is_Written_In_The_Companys_Own_Time_Zone()
    {
        var fixture = BuildFixture();
        var message = Seed(fixture);

        var outcome = await fixture.HandleAsync(message);

        Assert.Equal(DeliveryOutcomeKind.Sent, outcome.Kind);
        var sent = Assert.Single(fixture.Email.Sent);

        Assert.Equal("aye.aye@example.test", sent.To);
        Assert.Contains("Monday, 3 August 2026", sent.PlainTextBody);
        Assert.Contains("9:00 AM to 10:00 AM (UTC+06:30)", sent.PlainTextBody);
    }

    [Fact]
    public async Task The_Invitation_Names_The_Candidate_The_Job_And_The_Company()
    {
        var fixture = BuildFixture();

        var outcome = await fixture.HandleAsync(Seed(fixture));

        Assert.Equal(DeliveryOutcomeKind.Sent, outcome.Kind);
        var sent = Assert.Single(fixture.Email.Sent);

        Assert.Contains("Dear Aye Aye Mon", sent.PlainTextBody);
        Assert.Contains("Collections Officer (Field)", sent.Subject);
        Assert.Contains("Yoma Bank Recruitment", sent.PlainTextBody);
        Assert.Null(sent.HtmlBody); // plain text only: a candidate's own name cannot inject markup
    }

    /// <summary>What <c>Interview.Location</c> means depends on the mode — that is the whole reason
    /// the mode is an enum. "Where: https://meet…" reads as a bug to the person receiving it.</summary>
    [Theory]
    [InlineData(InterviewMode.Video, "https://meet.example.test/abc", "Join:")]
    [InlineData(InterviewMode.OnSite, "Level 12, Sule Square", "Where:")]
    [InlineData(InterviewMode.Phone, "09 250 111 222", "Call:")]
    public async Task The_Mode_Decides_What_The_Location_Line_Says(
        InterviewMode mode, string location, string expectedLabel)
    {
        var fixture = BuildFixture();
        var message = Seed(fixture, mode: mode, location: location);

        await fixture.HandleAsync(message);

        var body = Assert.Single(fixture.Email.Sent).PlainTextBody;
        Assert.Contains(expectedLabel, body);
        Assert.Contains(location, body);
    }

    /// <summary>A video round with no link yet still goes out. Holding the invitation until
    /// somebody pastes a URL means the candidate learns the date late, which is the worse of the
    /// two failures by a distance.</summary>
    [Fact]
    public async Task A_Video_Round_With_No_Link_Still_Tells_The_Candidate_When()
    {
        var fixture = BuildFixture();
        var message = Seed(fixture, mode: InterviewMode.Video, location: null);

        var outcome = await fixture.HandleAsync(message);

        Assert.Equal(DeliveryOutcomeKind.Sent, outcome.Kind);
        var body = Assert.Single(fixture.Email.Sent).PlainTextBody;
        Assert.Contains("Monday, 3 August 2026", body);
        Assert.Contains("we will send you a link", body);
    }

    [Fact]
    public async Task A_Reschedule_Says_The_Time_Moved_Rather_Than_Inviting_Again()
    {
        var fixture = BuildFixture();
        var message = Seed(fixture, isReschedule: true);

        await fixture.HandleAsync(message);

        var sent = Assert.Single(fixture.Email.Sent);
        Assert.Contains("changed", sent.Subject);
        Assert.Contains("has been moved", sent.PlainTextBody);
    }

    // ---------------------------------------------------------------- suppression

    /// <summary>The case ADR-0026 makes <c>Suppressed</c> a first-class status for: the system did
    /// the right thing, and the delivery log must not colour it red.
    ///
    /// <para>It also shows why nothing is rendered at enqueue time. <c>CancelAsync</c> writes one
    /// row and does nothing to the queue; the invitation stops itself.</para></summary>
    [Theory]
    [InlineData(InterviewStatus.Cancelled)]
    [InlineData(InterviewStatus.Completed)]
    [InlineData(InterviewStatus.NoShow)]
    public async Task An_Invitation_To_A_Round_That_Is_No_Longer_Scheduled_Is_Suppressed(
        InterviewStatus status)
    {
        var fixture = BuildFixture();
        var message = Seed(fixture, status: status);

        var outcome = await fixture.HandleAsync(message);

        Assert.Equal(DeliveryOutcomeKind.Suppressed, outcome.Kind);
        Assert.Empty(fixture.Email.Sent);
        Assert.Contains("was not emailed", outcome.Detail);
    }

    /// <summary>A back-filled round, or a queue that was stuck while the slot came and went.
    /// Inviting somebody to an interview that already happened is worse than saying nothing.</summary>
    [Fact]
    public async Task An_Invitation_To_A_Slot_That_Has_Already_Passed_Is_Suppressed()
    {
        var fixture = BuildFixture();
        var message = Seed(fixture);

        fixture.Clock.Now = Slot.AddHours(1);

        var outcome = await fixture.HandleAsync(message);

        Assert.Equal(DeliveryOutcomeKind.Suppressed, outcome.Kind);
        Assert.Empty(fixture.Email.Sent);
        Assert.Contains("already passed", outcome.Detail);
    }

    // ---------------------------------------------------------------- visible failures

    /// <summary>The row exists so that "was this candidate told?" has an answer. This is one:
    /// no — and here is why, where a recruiter will read it, rather than nowhere.</summary>
    [Fact]
    public async Task A_Candidate_With_No_Email_Address_Is_A_Visible_Failure()
    {
        var fixture = BuildFixture();
        var message = Seed(fixture, candidateEmail: null);

        var outcome = await fixture.HandleAsync(message);

        Assert.Equal(DeliveryOutcomeKind.Failed, outcome.Kind);
        Assert.Empty(fixture.Email.Sent);
        Assert.Contains("no email address on record", outcome.Detail);
        Assert.Contains("Contact them another way", outcome.Detail);
    }

    [Fact]
    public async Task An_Invitation_Pointing_At_Nothing_Is_Failed_Not_Retried()
    {
        var fixture = BuildFixture();
        var message = Seed(fixture, subjectId: Guid.NewGuid());

        var outcome = await fixture.HandleAsync(message);

        Assert.Equal(DeliveryOutcomeKind.Failed, outcome.Kind);
        Assert.Empty(fixture.Email.Sent);
    }

    // ---------------------------------------------------------------- transport outcomes

    /// <summary>The handler reports, the worker decides how often. A transport refusal is passed
    /// through with the transport's own reading of whether it is worth trying again.</summary>
    [Fact]
    public async Task A_Rejected_Address_Becomes_Failed()
    {
        var fixture = BuildFixture();
        fixture.Email.Refuse = _ => new EmailDeliveryException(
            "The mail server rejected 'aye.aye@example.test' as undeliverable (550).",
            isPermanent: true);

        var outcome = await fixture.HandleAsync(Seed(fixture));

        Assert.Equal(DeliveryOutcomeKind.Failed, outcome.Kind);
        Assert.Contains("550", outcome.Detail);
    }

    /// <summary>A relay that was restarting must not cost the candidate their invitation.</summary>
    [Fact]
    public async Task A_Busy_Server_Becomes_A_Retry()
    {
        var fixture = BuildFixture();
        fixture.Email.Refuse = _ => new EmailDeliveryException(
            "The mail server did not accept the message (421).", isPermanent: false);

        var outcome = await fixture.HandleAsync(Seed(fixture));

        Assert.Equal(DeliveryOutcomeKind.Retry, outcome.Kind);
    }

    // ---------------------------------------------------------------- degraded, not blocked

    /// <summary>A company with no time zone set, or a bad one. The invitation still goes — in UTC,
    /// labelled UTC. Refusing to send over a configuration field would be the worse outcome, and
    /// the label is what stops it being a silent lie.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("Mars/Olympus_Mons")]
    public async Task An_Unusable_Time_Zone_Sends_In_Utc_Rather_Than_Not_Sending(string? timeZone)
    {
        var fixture = BuildFixture();
        var message = Seed(fixture, companyTimeZone: timeZone);

        var outcome = await fixture.HandleAsync(message);

        Assert.Equal(DeliveryOutcomeKind.Sent, outcome.Kind);
        var body = Assert.Single(fixture.Email.Sent).PlainTextBody;
        Assert.Contains("2:30 AM to 3:30 AM (UTC)", body);
    }

    /// <summary>A payload that cannot be read is not worth losing an invitation over — it only
    /// chooses which clock the time is written in.</summary>
    [Fact]
    public async Task An_Unreadable_Payload_Sends_In_Utc_Rather_Than_Failing()
    {
        var fixture = BuildFixture();
        var message = Seed(fixture);

        using (var scope = fixture.Provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = db.OutboundMessages.IgnoreQueryFilters().Single(m => m.Id == message.Id);
            row.PayloadJson = "{ this is not json";
            db.SaveChanges();
            message.PayloadJson = row.PayloadJson;
        }

        var outcome = await fixture.HandleAsync(message);

        Assert.Equal(DeliveryOutcomeKind.Sent, outcome.Kind);
        Assert.Contains("(UTC)", Assert.Single(fixture.Email.Sent).PlainTextBody);
    }
}
