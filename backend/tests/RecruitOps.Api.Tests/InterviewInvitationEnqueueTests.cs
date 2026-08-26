using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RecruitOps.Application.DTOs;
using RecruitOps.Domain.Entities;
using RecruitOps.Domain.Enums;
using RecruitOps.Infrastructure.Persistence;
using RecruitOps.Infrastructure.Services.Delivery;
using Xunit;

namespace RecruitOps.Api.Tests;

/// <summary>The enqueue half of Module 3.2, through the real API (ADR-0026 §2).
///
/// <para><b>What makes this the transactional outbox rather than a send.</b> Scheduling a round
/// writes the interview, the stage move and the candidate's invitation in one
/// <c>SaveChangesAsync</c>. There is no window in which the round exists and the intention to tell
/// the candidate does not, and no request waits on a mail server.</para>
/// </summary>
public class InterviewInvitationEnqueueTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;
    private readonly Module3Scenario _scenario;

    public InterviewInvitationEnqueueTests(CustomWebAppFactory factory)
    {
        _factory = factory;
        _scenario = new Module3Scenario(factory);
    }

    private async Task<InterviewDto> ScheduleAsync(Guid applicationId, DateTimeOffset start)
    {
        var res = await _scenario.Recruiter().PostAsJsonAsync(
            $"/api/applications/{applicationId}/interviews",
            new ScheduleInterviewRequest
            {
                ScheduledStart = start,
                DurationMinutes = 60,
                Mode = "Video",
                Location = "https://meet.example.test/first",
                ParticipantUserIds = new[] { _factory.HiringManagerUserId },
                LeadUserId = _factory.HiringManagerUserId,
            });

        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<InterviewDto>())!;
    }

    private async Task RescheduleAsync(Guid interviewId, DateTimeOffset start)
    {
        var res = await _scenario.Recruiter().PutAsJsonAsync(
            $"/api/interviews/{interviewId}",
            new RescheduleInterviewRequest
            {
                ScheduledStart = start,
                DurationMinutes = 60,
                Mode = "Video",
                Location = "https://meet.example.test/moved",
            });

        res.EnsureSuccessStatusCode();
    }

    private List<OutboundMessage> InvitationsFor(Guid interviewId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // IgnoreQueryFilters because this scope has no request and therefore no tenant — the
        // worker's situation, and the reason IAmbientTenantScope exists.
        return db.OutboundMessages.IgnoreQueryFilters()
            .Where(m => m.SubjectId == interviewId)
            .OrderBy(m => m.CreatedAt)
            .ToList();
    }

    private static InterviewInvitationPayload PayloadOf(OutboundMessage message) =>
        JsonSerializer.Deserialize<InterviewInvitationPayload>(message.PayloadJson)!;

    private string CandidateEmailFor(Guid applicationId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var candidateId = db.JobApplications.IgnoreQueryFilters()
            .Where(a => a.Id == applicationId).Select(a => a.CandidateId).Single();

        return db.Candidates.IgnoreQueryFilters()
            .Where(c => c.Id == candidateId).Select(c => c.Email).Single()!;
    }

    // ---------------------------------------------------------------- enqueue

    [Fact]
    public async Task Scheduling_A_Round_Queues_The_Candidates_Invitation()
    {
        var (_, applicationId) = await _scenario.ApplicationAsync("Queues an invitation");
        var interview = await ScheduleAsync(applicationId, DateTimeOffset.UtcNow.AddDays(7));

        var message = Assert.Single(InvitationsFor(interview.Id));

        Assert.Equal(OutboundMessageKind.InterviewInvitation, message.Kind);
        Assert.Equal(nameof(Interview), message.SubjectType);
        Assert.Equal(_factory.TenantA, message.TenantId);
        Assert.Equal(CandidateEmailFor(applicationId), message.Recipient);
        Assert.Equal(OutboundMessageStatus.Pending, message.Status);

        // The company's own zone, frozen at enqueue — the one thing that cannot be re-read later
        // because Postgres normalises the stored instant to UTC.
        Assert.Equal("Asia/Yangon", PayloadOf(message).TimeZoneId);
        Assert.False(PayloadOf(message).IsReschedule);
    }

    /// <summary>The stage move and the invitation ride in the same save. If the outbox write had
    /// been a second transaction, one of these could exist without the other.</summary>
    [Fact]
    public async Task The_Invitation_And_The_Stage_Move_Are_Written_Together()
    {
        var (_, applicationId) = await _scenario.ApplicationAsync("One transaction");
        var interview = await ScheduleAsync(applicationId, DateTimeOffset.UtcNow.AddDays(7));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var status = db.JobApplications.IgnoreQueryFilters()
            .Where(a => a.Id == applicationId).Select(a => a.Status).Single();

        Assert.Equal(PipelineStatus.Interview, status);
        Assert.Single(InvitationsFor(interview.Id));
    }

    // ---------------------------------------------------------------- reschedule

    /// <summary>The duplicate rule. The first invitation has not gone out yet, and it renders the
    /// slot live — so it will carry the new time by itself. A second row would send the candidate
    /// an invitation and, seconds later, a notice that their time had "changed" from one they were
    /// never given.</summary>
    [Fact]
    public async Task Rescheduling_Before_The_Invitation_Goes_Does_Not_Queue_A_Second_One()
    {
        var (_, applicationId) = await _scenario.ApplicationAsync("Moved before sending");
        var interview = await ScheduleAsync(applicationId, DateTimeOffset.UtcNow.AddDays(7));

        await RescheduleAsync(interview.Id, DateTimeOffset.UtcNow.AddDays(9));

        var message = Assert.Single(InvitationsFor(interview.Id));

        // Still the original invitation, unchanged — including its wording.
        Assert.False(PayloadOf(message).IsReschedule);
        Assert.Equal(OutboundMessageStatus.Pending, message.Status);
    }

    /// <summary>Once the candidate has actually been told a time, moving it owes them a second
    /// message — and it has to read as a change, not as a fresh invitation.</summary>
    [Fact]
    public async Task Rescheduling_After_The_Invitation_Has_Gone_Tells_The_Candidate_It_Moved()
    {
        var (_, applicationId) = await _scenario.ApplicationAsync("Moved after sending");
        var interview = await ScheduleAsync(applicationId, DateTimeOffset.UtcNow.AddDays(7));

        MarkSent(InvitationsFor(interview.Id).Single().Id);

        await RescheduleAsync(interview.Id, DateTimeOffset.UtcNow.AddDays(9));

        var messages = InvitationsFor(interview.Id);
        Assert.Equal(2, messages.Count);
        Assert.Equal(OutboundMessageStatus.Sent, messages[0].Status);
        Assert.True(PayloadOf(messages[1]).IsReschedule);
    }

    private void MarkSent(Guid messageId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var message = db.OutboundMessages.IgnoreQueryFilters().Single(m => m.Id == messageId);
        message.Status = OutboundMessageStatus.Sent;
        message.SentAt = DateTimeOffset.UtcNow;
        db.SaveChanges();
    }

    // ---------------------------------------------------------------- end to end

    /// <summary>Enqueue to transport, through the real worker, with nothing stubbed but the mail
    /// server itself — which this deployment does not have.
    ///
    /// <para>The assertion is deliberately the <b>transport's</b> complaint. Reaching it at all
    /// means the worker claimed a row across tenants, established this tenant for the handler's
    /// scope, and the handler then found the interview, the application, the candidate and the
    /// company through ordinary filtered queries. Any break in that chain shows up here as
    /// "no longer exists" instead — a different message, and a different bug.</para></summary>
    [Fact]
    public async Task A_Queued_Invitation_Reaches_The_Transport_And_An_Unconfigured_Install_Says_So()
    {
        var (_, applicationId) = await _scenario.ApplicationAsync("All the way to the transport");
        var interview = await ScheduleAsync(applicationId, DateTimeOffset.UtcNow.AddDays(7));

        await _factory.Services.GetRequiredService<OutboundMessageWorker>().RunOnceAsync();

        var message = Assert.Single(InvitationsFor(interview.Id));

        Assert.Equal(OutboundMessageStatus.Pending, message.Status);   // retryable, not given up on
        Assert.Equal(1, message.Attempts);
        Assert.Contains("no mail server configured", message.LastError);
    }
}
