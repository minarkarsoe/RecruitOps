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

/// <summary>Module 5.2's Pipeline &amp; source page (ADR-0032). One scenario, seeded once, every expected number
/// worked out by hand below.
///
/// <para>Today is <b>24 October 2026</b> in Yangon (UTC+06:30): the last 90 days are 27 Jul – 24 Oct.</para>
///
/// <code>
///       posting  channel        applied (company day)   statuses held                         offer
/// A0    Teller   Direct         27 Jul (26 Jul 18:00 UTC) Applied
/// A1    Teller   Facebook        1 Aug                  Applied Screening Interview Offer      R, accepted 10 Sep
/// A2    Teller   Facebook        2 Aug                  Applied Rejected
/// A3    Teller   Facebook        3 Aug                  Applied Screening Rejected
/// A4    Teller   LinkedIn        5 Aug                  Applied Interview (skipped screening)
/// A5    Teller   Direct          1 Sep                  Applied
/// A6    Credit   ManualUpload    2 Sep  (R's upload)    Sourced Screening Shortlisted Interview Offer Hired   A, accepted 1 Oct
/// A7    Credit   ManualUpload    3 Sep  (R's upload)    Sourced
/// A8    Credit   Telegram        1 Oct                  Applied Screening
/// A9    Credit   Viber          24 Oct (23 Oct 22:00 UTC) Applied
/// Aold  Teller   Direct         20 Jul — this year only Applied Interview Offer                A, accepted 1 Aug
/// </code>
/// Interviews: I1 A1 by R 20 Aug · I2 A4 by A 5 Sep · I3 A6 by R 20 Sep · I4 A3 by R 15 Aug, cancelled ·
/// I5 Aold by A 1 Jul (before the window).</summary>
public class PipelineAnalyticsTests : IClassFixture<OfferPortalWebAppFactory>
{
    private readonly OfferPortalWebAppFactory _factory;
    private readonly Module3Scenario _scenario;
    private static readonly object Gate = new();
    private static readonly Guid BravoAdminId = new("b7a40000-0000-4000-8000-00000000ad52");

    private Guid _teller;
    private Guid _credit;

    public PipelineAnalyticsTests(OfferPortalWebAppFactory factory)
    {
        _factory = factory;
        _scenario = new Module3Scenario(factory);
        _factory.Clock.Now = new DateTimeOffset(2026, 10, 24, 6, 0, 0, TimeSpan.Zero);
        lock (Gate) Seed();
    }

    private static DateTimeOffset Utc(int month, int day, int hour = 5) => new(2026, month, day, hour, 0, 0, TimeSpan.Zero);

    private void Seed()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var existing = db.JobPostings.IgnoreQueryFilters().Where(p => p.Title.StartsWith("Pipeline ")).ToList();
        if (existing.Count > 0)
        {
            _teller = existing.Single(p => p.Title == "Pipeline Teller").Id;
            _credit = existing.Single(p => p.Title == "Pipeline Credit").Id;
            return;
        }

        var tenant = _factory.TenantA;
        JobPosting Posting(Guid department, string title)
        {
            var r = new Requisition
            {
                TenantId = tenant,
                DepartmentId = department,
                RequestedByUserId = _factory.HiringManagerUserId,
                Title = title,
                JobDescription = "-",
                Headcount = 3,
                Status = RequisitionStatus.Approved,
                DecidedAt = Utc(6, 1),
            };
            db.Requisitions.Add(r);
            var p = new JobPosting { TenantId = tenant, DepartmentId = department, RequisitionId = r.Id, Title = title, Description = "-" };
            db.JobPostings.Add(p);
            return p;
        }
        var teller = Posting(_factory.SalesDepartmentId, "Pipeline Teller");
        var credit = Posting(_factory.FinanceDepartmentId, "Pipeline Credit");
        _teller = teller.Id;
        _credit = credit.Id;

        var r = _factory.RecruiterUserId;
        var a = _factory.AdminUserId;
        var batch = new BulkUploadBatch { TenantId = tenant, JobPostingId = credit.Id, UploadedByUserId = r };
        db.BulkUploadBatches.Add(batch);

        var number = 9500;
        JobApplication Application(JobPosting posting, SourceChannel channel, DateTimeOffset applied, params PipelineStatus[] statuses)
        {
            var candidate = new Candidate { TenantId = tenant, FullName = $"Pipeline candidate {number}", Source = channel };
            var application = new JobApplication
            {
                TenantId = tenant,
                CandidateId = candidate.Id,
                JobPostingId = posting.Id,
                AppliedAt = applied,
                Source = channel,
                Status = statuses[^1],
            };
            db.Candidates.Add(candidate);
            db.JobApplications.Add(application);
            PipelineStatus? from = null;
            foreach (var status in statuses)
            {
                db.ApplicationStageHistories.Add(new ApplicationStageHistory
                {
                    TenantId = tenant,
                    JobApplicationId = application.Id,
                    FromStatus = from,
                    ToStatus = status,
                    ChangedAt = applied,
                });
                from = status;
            }
            if (channel == SourceChannel.ManualUpload)
            {
                db.BulkUploadFiles.Add(new BulkUploadFile
                {
                    TenantId = tenant,
                    BulkUploadBatchId = batch.Id,
                    Ordinal = number,
                    FileName = "cv.pdf",
                    ContentType = "application/pdf",
                    StorageKey = $"k{number}",
                    Status = BulkFileStatus.Success,
                    CandidateId = candidate.Id,
                    JobApplicationId = application.Id,
                });
            }
            number++;
            return application;
        }
        void Accepted(JobApplication application, Guid maker, DateTimeOffset responded) => db.Offers.Add(new Offer
        {
            TenantId = tenant,
            JobApplicationId = application.Id,
            Year = 2026,
            Number = number++,
            Status = OfferStatus.Accepted,
            BaseSalary = 500_000,
            LetterBody = "-",
            CreatedByUserId = maker,
            JoiningDate = new DateOnly(2026, 11, 1),
            ExpiresOn = DateOnly.FromDateTime(responded.UtcDateTime).AddDays(5),
            RespondedAt = responded,
        });
        void Interview(JobApplication application, Guid by, DateTimeOffset start, InterviewStatus status = InterviewStatus.Scheduled) =>
            db.Interviews.Add(new Interview
            {
                TenantId = tenant,
                JobApplicationId = application.Id,
                ScheduledStart = start,
                ScheduledByUserId = by,
                Status = status,
            });

        const PipelineStatus Ap = PipelineStatus.Applied, Sc = PipelineStatus.Screening, Sh = PipelineStatus.Shortlisted,
            In = PipelineStatus.Interview, Of = PipelineStatus.Offer, Hi = PipelineStatus.Hired, Re = PipelineStatus.Rejected,
            So = PipelineStatus.Sourced;

        Application(teller, SourceChannel.Direct, Utc(7, 26, 18), Ap);                                    // A0
        var a1 = Application(teller, SourceChannel.Facebook, Utc(8, 1), Ap, Sc, In, Of);                  // A1
        Application(teller, SourceChannel.Facebook, Utc(8, 2), Ap, Re);                                   // A2
        var a3 = Application(teller, SourceChannel.Facebook, Utc(8, 3), Ap, Sc, Re);                      // A3
        var a4 = Application(teller, SourceChannel.LinkedIn, Utc(8, 5), Ap, In);                          // A4
        Application(teller, SourceChannel.Direct, Utc(9, 1), Ap);                                         // A5
        var a6 = Application(credit, SourceChannel.ManualUpload, Utc(9, 2), So, Sc, Sh, In, Of, Hi);      // A6
        Application(credit, SourceChannel.ManualUpload, Utc(9, 3), So);                                   // A7
        Application(credit, SourceChannel.Telegram, Utc(10, 1), Ap, Sc);                                  // A8
        Application(credit, SourceChannel.Viber, Utc(10, 23, 22), Ap);                                    // A9
        var old = Application(teller, SourceChannel.Direct, Utc(7, 20), Ap, In, Of);                      // Aold

        Accepted(a1, r, Utc(9, 10));
        Accepted(a6, a, Utc(10, 1));
        Accepted(old, a, Utc(8, 1));
        Interview(a1, r, Utc(8, 20), InterviewStatus.Completed);
        Interview(a4, a, Utc(9, 5));
        Interview(a6, r, Utc(9, 20));
        Interview(a3, r, Utc(8, 15), InterviewStatus.Cancelled);
        Interview(old, a, Utc(7, 1), InterviewStatus.Completed);

        db.Users.Add(new User
        {
            Id = BravoAdminId,
            TenantId = _factory.TenantB,
            Email = "admin@bravo-pipeline.test",
            DisplayName = "Bravo Admin",
            Role = UserRole.Admin,
            IsActive = true,
            PasswordHash = "not-used-in-these-tests",
        });
        db.SaveChanges();
    }

    private async Task<PipelineAnalyticsDto> GetAsync(HttpClient client, string query = "")
    {
        var res = await client.GetAsync($"/api/analytics/pipeline{query}");
        Assert.True(res.IsSuccessStatusCode, $"{res.StatusCode}: {await res.Content.ReadAsStringAsync()}");
        return (await res.Content.ReadFromJsonAsync<PipelineAnalyticsDto>())!;
    }

    private HttpClient Admin() => _scenario.Client(Roles.Admin, _factory.AdminUserId);
    private HttpClient Recruiter() => _scenario.Client(Roles.Recruiter, _factory.RecruiterUserId);

    private static ChannelShareDto Channel(PipelineAnalyticsDto dto, string channel) => dto.Sources.Single(s => s.Channel == channel);

    [Fact]
    public async Task Where_Candidates_Come_From_And_Which_Of_Them_Are_Hired()
    {
        var dto = await GetAsync(Admin());

        Assert.Equal("Company", dto.Scope);
        Assert.Equal(("90d", new DateOnly(2026, 7, 27), new DateOnly(2026, 10, 24)), (dto.Window, dto.From, dto.To));
        // A0 arrived on 27 Jul in Yangon and A9 on 24 Oct, though both were the day before in UTC; Aold is outside.
        Assert.Equal(10, dto.Candidates);
        Assert.Equal(new[] { "Facebook", "LinkedIn", "CareerPage", "ManualUpload", "Other" }, dto.Sources.Select(s => s.Channel));

        Assert.Equal((3, 30.0, 1, 33.3), Tuple(Channel(dto, "Facebook")));
        Assert.Equal((1, 10.0, 0, 0.0), Tuple(Channel(dto, "LinkedIn")));
        Assert.Equal((2, 20.0, 0, 0.0), Tuple(Channel(dto, "CareerPage")));
        Assert.Equal((2, 20.0, 1, 50.0), Tuple(Channel(dto, "ManualUpload")));
        Assert.Equal((2, 20.0, 0, 0.0), Tuple(Channel(dto, "Other")));   // Telegram + Viber
    }

    private static (int, double?, int, double?) Tuple(ChannelShareDto c) => (c.Candidates, c.Share, c.Hired, c.HireRate);

    [Fact]
    public async Task The_Funnel_Counts_Everyone_Who_Reached_Each_Band()
    {
        var dto = await GetAsync(Admin());

        Assert.Equal(new[] { "Sourcing", "Screened", "Interview", "Offered" }, dto.Funnel.Select(b => b.Band));
        // Screened: A1 A3 A4 A6 A8 — A4 skipped screening but reached Interview; A3 was rejected after it.
        Assert.Equal(new[] { 10, 5, 3, 2 }, dto.Funnel.Select(b => b.Count));
        Assert.Equal(new double?[] { null, 50.0, 40.0, 33.3 }, dto.Funnel.Select(b => b.DropOff));
    }

    [Fact]
    public async Task The_Leaderboard_Lists_What_Each_Person_Did_In_The_Window_By_Name()
    {
        var dto = await GetAsync(Admin());

        var rows = dto.Leaderboard!;
        Assert.Equal(new[] { "Alpha Admin", "Alpha Recruiter" }, rows.Select(r => r.Name));
        // Admin: I2; A6's offer and Aold's — accepted in the window, though Aold arrived before it.
        Assert.Equal((0, 1, 2), (rows[0].CvsIn, rows[0].Interviews, rows[0].Hires));
        // Recruiter: uploaded A6 A7; I1 I3 (I4 was cancelled); A1's offer.
        Assert.Equal((2, 2, 1), (rows[1].CvsIn, rows[1].Interviews, rows[1].Hires));
    }

    [Fact]
    public async Task A_Recruiter_Gets_The_Charts_But_No_Leaderboard()
    {
        var dto = await GetAsync(Recruiter());

        Assert.Equal("Recruiter", dto.Scope);
        Assert.Null(dto.Leaderboard);
        Assert.Equal(10, dto.Candidates);
        Assert.Equal(new[] { 10, 5, 3, 2 }, dto.Funnel.Select(b => b.Count));
    }

    [Fact]
    public async Task Hiring_Managers_Approvers_And_Interviewers_Have_No_Access()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _scenario.SalesManager().GetAsync("/api/analytics/pipeline")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _scenario.FinanceApprover().GetAsync("/api/analytics/pipeline")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await _scenario.Client(Roles.Interviewer, _factory.HiringManagerUserId).GetAsync("/api/analytics/pipeline")).StatusCode);
    }

    [Fact]
    public async Task A_Position_Narrows_Every_Chart_And_The_Leaderboard_But_Not_Its_Own_Choices()
    {
        var dto = await GetAsync(Admin(), $"?postingId={_credit}");

        Assert.Equal(4, dto.Candidates);                                   // A6 A7 A8 A9
        Assert.Equal((2, 50.0, 1, 50.0), Tuple(Channel(dto, "ManualUpload")));
        Assert.Equal((0, 0.0, 0, null), Tuple(Channel(dto, "Facebook")));  // no candidates, so no rate
        Assert.Equal(new[] { 4, 2, 1, 1 }, dto.Funnel.Select(b => b.Count));
        Assert.Equal(new double?[] { null, 50.0, 50.0, 0.0 }, dto.Funnel.Select(b => b.DropOff));

        var rows = dto.Leaderboard!;
        Assert.Equal((0, 0, 1), (rows.Single(r => r.Name == "Alpha Admin").CvsIn, rows.Single(r => r.Name == "Alpha Admin").Interviews, rows.Single(r => r.Name == "Alpha Admin").Hires));
        Assert.Equal((2, 1, 0), (rows.Single(r => r.Name == "Alpha Recruiter").CvsIn, rows.Single(r => r.Name == "Alpha Recruiter").Interviews, rows.Single(r => r.Name == "Alpha Recruiter").Hires));

        Assert.Equal(new[] { "Pipeline Credit", "Pipeline Teller" }, dto.Positions.Select(p => p.Name));
    }

    [Fact]
    public async Task A_Source_Narrows_To_Its_Channel()
    {
        var dto = await GetAsync(Admin(), "?source=Facebook");

        Assert.Equal(3, dto.Candidates);
        Assert.Equal((3, 100.0, 1, 33.3), Tuple(Channel(dto, "Facebook")));
        Assert.DoesNotContain(dto.Sources, s => s.Channel == "Other");    // shown only when it has someone
        Assert.Equal(new[] { 3, 2, 1, 1 }, dto.Funnel.Select(b => b.Count));
        // Only the Recruiter did anything with Facebook's applicants: I1 and A1's offer (I4 was cancelled).
        var row = Assert.Single(dto.Leaderboard!);
        Assert.Equal(("Alpha Recruiter", 0, 1, 1), (row.Name, row.CvsIn, row.Interviews, row.Hires));
    }

    [Fact]
    public async Task This_Year_Takes_In_Earlier_Arrivals_And_Events()
    {
        var dto = await GetAsync(Admin(), "?window=year");

        Assert.Equal(("year", new DateOnly(2026, 1, 1)), (dto.Window, dto.From));
        Assert.Equal(11, dto.Candidates);
        Assert.Equal((3, 27.3, 1, 33.3), Tuple(Channel(dto, "CareerPage")));  // A0 A5 Aold; Aold hired
        Assert.Equal(2, dto.Leaderboard!.Single(r => r.Name == "Alpha Admin").Interviews);   // I2, and I5 on 1 Jul
    }

    [Fact]
    public async Task Another_Company_Sees_None_Of_It()
    {
        var bravo = _scenario.Client(Roles.Admin, BravoAdminId);
        bravo.DefaultRequestHeaders.Remove("X-Test-Tenant");
        bravo.DefaultRequestHeaders.Add("X-Test-Tenant", _factory.TenantB.ToString());

        var dto = await GetAsync(bravo);
        Assert.Equal(0, dto.Candidates);
        Assert.All(dto.Sources, s => Assert.Equal((0, (double?)null), (s.Candidates, s.Share)));
        Assert.Empty(dto.Leaderboard!);
        Assert.Empty(dto.Positions);
    }

    [Theory]
    [InlineData("?window=Year")]
    [InlineData("?window=30d")]
    [InlineData("?window=0")]
    [InlineData("?source=facebook")]
    [InlineData("?source=Telegram")]
    [InlineData("?source=2")]
    public async Task A_Window_And_A_Source_Are_Named_Exactly(string query) =>
        Assert.Equal(HttpStatusCode.BadRequest, (await Admin().GetAsync($"/api/analytics/pipeline{query}")).StatusCode);
}
