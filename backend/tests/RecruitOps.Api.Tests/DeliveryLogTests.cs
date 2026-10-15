using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RecruitOps.Api.Auth;
using RecruitOps.Application.DTOs;
using RecruitOps.Domain.Entities;
using RecruitOps.Domain.Enums;
using RecruitOps.Infrastructure.Persistence;
using Xunit;

namespace RecruitOps.Api.Tests;

/// <summary>The read side of ADR-0026 — `GET /api/delivery`.
///
/// <para>The outbox has recorded every send since 2026-08-20 and nothing rendered it, so a failed
/// invitation was written down faithfully and shown to nobody. These tests are mostly about who
/// may see what, because that is where a read endpoint over candidate data goes wrong: the table
/// has no department of its own and reaches one only through <c>SubjectType</c>/<c>SubjectId</c>,
/// which is a filter somebody has to remember to write (ADR-0003).</para>
/// </summary>
public class DeliveryLogTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;
    private readonly Module3Scenario _scenario;

    public DeliveryLogTests(CustomWebAppFactory factory)
    {
        _factory = factory;
        _scenario = new Module3Scenario(factory);
    }

    private async Task<PagedResult<DeliveryLogEntryDto>> LogAsync(
        HttpClient client, string query = "")
    {
        var res = await client.GetAsync($"/api/delivery{query}");
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<PagedResult<DeliveryLogEntryDto>>())!;
    }

    /// <summary>Forces one queued message into a terminal state without running the worker, so a
    /// test can assert on what a recruiter sees rather than on how it got there.</summary>
    private void MarkFailed(Guid interviewId, string reason)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // IgnoreQueryFilters: this scope has no request, so no tenant — the same reason the
        // worker's claim query needs it (ADR-0026 §4).
        var message = db.OutboundMessages.IgnoreQueryFilters()
            .First(m => m.SubjectId == interviewId);

        message.Status = OutboundMessageStatus.Failed;
        message.LastError = reason;
        message.Attempts = 3;
        db.SaveChanges();
    }

    [Fact]
    public async Task A_Queued_Invitation_Appears_In_The_Log()
    {
        var (_, applicationId) = await _scenario.ApplicationAsync("Delivery log — queued");
        var interview = await _scenario.ScheduleAsync(applicationId);

        var log = await LogAsync(_scenario.Recruiter());

        var row = log.Items.Single(r => r.SubjectId == interview.Id);
        Assert.Equal(OutboundMessageKind.InterviewInvitation, row.Kind);
        Assert.Equal("Interview invitation", row.KindLabel);
        Assert.Equal("Email", row.Channel);
        Assert.Equal("Interview", row.SubjectType);
        Assert.Equal(OutboundMessageStatus.Pending, row.Status);

        // The candidate is resolved through the subject, which is the entire reason the log is
        // readable — "was this candidate told?" needs the candidate's name, not a message id.
        Assert.Equal("Applicant for Delivery log — queued", row.CandidateName);
    }

    [Fact]
    public async Task A_Failure_Carries_The_Reason_A_Recruiter_Has_To_Act_On()
    {
        var (_, applicationId) = await _scenario.ApplicationAsync("Delivery log — failure");
        var interview = await _scenario.ScheduleAsync(applicationId);

        MarkFailed(interview.Id,
            "There is no email address on record for this candidate, so no invitation could be sent.");

        var row = (await LogAsync(_scenario.Recruiter()))
            .Items.Single(r => r.SubjectId == interview.Id);

        Assert.Equal(OutboundMessageStatus.Failed, row.Status);
        Assert.Contains("no email address on record", row.LastError);

        // A terminal row has no next attempt. NextAttemptAt is still populated in the database —
        // it is a leftover from the last claim — and forwarding it would let the screen promise a
        // retry that is never coming.
        Assert.Null(row.NextAttemptAt);
    }

    [Fact]
    public async Task A_Hiring_Manager_Sees_Their_Own_Department_And_Not_Another()
    {
        // The scenario's applications are all in Sales. The Finance manager reaches Finance.
        var (_, applicationId) = await _scenario.ApplicationAsync("Delivery log — scoping");
        var interview = await _scenario.ScheduleAsync(applicationId);

        var sales = await LogAsync(_scenario.SalesManager());
        Assert.Contains(sales.Items, r => r.SubjectId == interview.Id);

        var finance = await LogAsync(_scenario.FinanceManager());
        Assert.DoesNotContain(finance.Items, r => r.SubjectId == interview.Id);
    }

    [Fact]
    public async Task Being_On_The_Panel_Does_Not_Open_The_Whole_Log()
    {
        // ScheduleAsync puts the FINANCE manager on the panel of a SALES application (ADR-0017 §4).
        // That earns them the one application; the delivery log is a company-wide list, and a
        // per-application exception must not widen into one.
        var (_, applicationId) = await _scenario.ApplicationAsync("Delivery log — panel");
        var interview = await _scenario.ScheduleAsync(applicationId);

        var finance = await LogAsync(_scenario.FinanceManager());

        Assert.DoesNotContain(finance.Items, r => r.SubjectId == interview.Id);
    }

    /// <summary>Writes a message whose subject the log does not know how to resolve — the shape
    /// every future kind arrives in before somebody adds its join. Returns its id.</summary>
    private Guid EnqueueUnresolvableMessage()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var message = new OutboundMessage
        {
            TenantId = _factory.TenantA,
            Kind = OutboundMessageKind.OfferSent,
            Recipient = "candidate@example.test",
            // "Offer" is a real subject type from ADR-0026's enum and there is no Offer table
            // yet, so nothing here can resolve it to a department. That is exactly the state
            // Module 4 will ship in.
            SubjectType = "Offer",
            SubjectId = Guid.NewGuid(),
            Status = OutboundMessageStatus.Pending,
            NextAttemptAt = DateTimeOffset.UtcNow,
        };

        db.OutboundMessages.Add(message);
        db.SaveChanges();
        return message.Id;
    }

    [Fact]
    public async Task A_Message_Whose_Subject_Cannot_Be_Resolved_Is_Hidden_From_A_Scoped_User()
    {
        // ⚠️ This test exists because a mutation survived without it. Flipping the department
        // filter from fail-closed to fail-open — `DepartmentId == null || allowed.Contains(...)`
        // — passed all ten of the other tests, because none of them ever produced a row the log
        // could not resolve to a department. The fail-closed comment in DeliveryLogService was,
        // until this test, an unverified claim.
        var id = EnqueueUnresolvableMessage();

        var recruiter = await LogAsync(_scenario.Recruiter());
        Assert.Contains(recruiter.Items, r => r.Id == id);

        // The Sales manager reaches Sales. This row reaches nothing, so it is not theirs — and
        // will not become theirs by default the day an Offer table exists and nobody adds the
        // join to DeliveryLogService.
        var sales = await LogAsync(_scenario.SalesManager());
        Assert.DoesNotContain(sales.Items, r => r.Id == id);
    }

    [Fact]
    public async Task An_Approver_Sees_Nothing()
    {
        var (_, applicationId) = await _scenario.ApplicationAsync("Delivery log — approver");
        await _scenario.ScheduleAsync(applicationId);

        var log = await LogAsync(_scenario.FinanceApprover());

        // ADR-0018: company-wide on the requisition axis, no standing reach into candidate data.
        // A list of what we said to candidates is candidate data.
        Assert.Empty(log.Items);
        Assert.Equal(0, log.TotalCount);
    }

    [Fact]
    public async Task An_Interviewer_Cannot_Reach_The_Endpoint_At_All()
    {
        // The literal, not `Roles.Interviewer` — that constant does not exist. `Roles` claims it
        // "must match RecruitOps.Domain.Enums.UserRole" and is missing this one member, which is
        // a pre-existing inconsistency worth knowing about but not worth fixing from here.
        var client = _scenario.Client("Interviewer", _factory.HiringManagerUserId);

        var res = await client.GetAsync("/api/delivery");

        // Turned away by the policy rather than by the service: an interviewer's legitimate reach
        // is one application they sit on, so there is no version of this list that is theirs.
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task The_Payload_Never_Crosses_The_Wire()
    {
        var (_, applicationId) = await _scenario.ApplicationAsync("Delivery log — payload");
        await _scenario.ScheduleAsync(applicationId);

        var res = await _scenario.Recruiter().GetAsync("/api/delivery");
        var body = await res.Content.ReadAsStringAsync();

        // PayloadJson holds render inputs and, for an offer, a salary. It has no business in a
        // list a Hiring Manager reads. Asserted on the raw body so that adding the property to
        // the DTO later fails here rather than shipping.
        Assert.DoesNotContain("payloadJson", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("timeZoneId", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Filtering_By_Subject_Answers_What_Did_We_Send_About_This_Round()
    {
        var (_, firstApplication) = await _scenario.ApplicationAsync("Delivery log — subject A");
        var first = await _scenario.ScheduleAsync(firstApplication);

        var (_, secondApplication) = await _scenario.ApplicationAsync("Delivery log — subject B");
        await _scenario.ScheduleAsync(secondApplication);

        var log = await LogAsync(
            _scenario.Recruiter(), $"?subjectType=Interview&subjectId={first.Id}");

        Assert.NotEmpty(log.Items);
        Assert.All(log.Items, r => Assert.Equal(first.Id, r.SubjectId));
    }

    [Fact]
    public async Task Filtering_By_Status_Separates_The_Failures_From_The_Noise()
    {
        var (_, applicationId) = await _scenario.ApplicationAsync("Delivery log — status filter");
        var interview = await _scenario.ScheduleAsync(applicationId);
        MarkFailed(interview.Id, "The relay refused the message.");

        var failed = await LogAsync(_scenario.Recruiter(), "?status=Failed");

        Assert.Contains(failed.Items, r => r.SubjectId == interview.Id);
        Assert.All(failed.Items, r => Assert.Equal(OutboundMessageStatus.Failed, r.Status));
    }

    [Fact]
    public async Task Paging_Is_Bounded_And_Newest_First()
    {
        var (_, applicationId) = await _scenario.ApplicationAsync("Delivery log — paging");
        await _scenario.ScheduleAsync(applicationId);

        // An unbounded pageSize is a way to ask the server for the entire outbox in one request.
        var log = await LogAsync(_scenario.Recruiter(), "?pageSize=100000");
        Assert.True(log.PageSize <= 100);

        var dates = log.Items.Select(r => r.CreatedAt).ToList();
        var firstBreak = Enumerable.Range(1, Math.Max(dates.Count - 1, 0))
            .Where(i => dates[i] > dates[i - 1])
            .Select(i => (int?)i)
            .FirstOrDefault();

        Assert.True(
            firstBreak is null,
            firstBreak is null
                ? string.Empty
                : $"The delivery log must be newest-first, but row {firstBreak} "
                  + $"({dates[firstBreak.Value]:O}) is newer than row {firstBreak - 1} "
                  + $"({dates[firstBreak.Value - 1]:O}).");
    }
}
