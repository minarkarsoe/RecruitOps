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

/// <summary>Module 5.1's executive dashboard (ADR-0031). One scenario, seeded once, with every expected
/// number worked out by hand below — so each rule is pinned with the figure it should produce.
///
/// <para>Today is <b>18 August 2026</b> in Yangon (UTC+06:30): this quarter is 1 Jul – 18 Aug, the previous
/// one 1 Apr – 30 Jun.</para>
///
/// <code>
///      dept     maker  outcome                       applied         TTH   req approved   TTF
/// O1   Sales    R      accepted 11 Jul               1 Jul           10    1 Jun          40
/// O2   Sales    A      accepted 10 Aug               21 Jul          20    1 Jun          70
/// O3   Finance  R      accepted 31 Jul               1 Jul           30    20 Jun         41
/// O9   Finance  A      accepted 30 Jun 20:00 UTC     20 Jun 20:00    10    20 Jun         10.83
///                      = 1 Jul in Yangon → this quarter, not the last
/// O4   Sales    R      declined 20 Jul
/// O5   Finance  A      Viewed, expired 1 Aug (never written) → an expiry on 2 Aug
/// O6   Sales    R      Sent, expires 30 Aug → still waiting, not counted
/// O7   Sales    R      accepted 15 Jun  (previous)    16 May          30    1 Jun          14
/// O8   Finance  A      Expired 10 Jun   (previous)
/// </code>
/// Sales requisition: 5 seats, 3 accepted (O1, O2, O7) → 2 open. Finance: 4 seats, 2 accepted → 2 open.
/// A Finance requisition still pending approval (6 seats) is not a vacancy.</summary>
public class ExecutiveDashboardTests : IClassFixture<OfferPortalWebAppFactory>
{
    private readonly OfferPortalWebAppFactory _factory;
    private readonly Module3Scenario _scenario;
    private static readonly object Gate = new();

    /// <summary>An administrator of the other company — a real user there, so the permission check passes and
    /// what is asserted is the data behind it.</summary>
    private static readonly Guid BravoAdminId = new("b7a40000-0000-4000-8000-00000000ad31");

    public ExecutiveDashboardTests(OfferPortalWebAppFactory factory)
    {
        _factory = factory;
        _scenario = new Module3Scenario(factory);
        _factory.Clock.Now = new DateTimeOffset(2026, 8, 18, 6, 0, 0, TimeSpan.Zero);
        lock (Gate) Seed();
    }

    private static DateTimeOffset Utc(int month, int day, int hour = 0) => new(2026, month, day, hour, 0, 0, TimeSpan.Zero);

    private void Seed()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (db.Requisitions.IgnoreQueryFilters().Any(r => r.Title == "Executive Sales")) return;

        var tenant = _factory.TenantA;
        Requisition Requisition(Guid department, string title, int seats, RequisitionStatus status, DateTimeOffset? approved)
        {
            var r = new Requisition
            {
                TenantId = tenant,
                DepartmentId = department,
                RequestedByUserId = _factory.HiringManagerUserId,
                Title = title,
                JobDescription = "-",
                Headcount = seats,
                Status = status,
                DecidedAt = approved,
            };
            db.Requisitions.Add(r);
            return r;
        }
        JobPosting Posting(Requisition r)
        {
            var p = new JobPosting { TenantId = tenant, DepartmentId = r.DepartmentId, RequisitionId = r.Id, Title = r.Title, Description = "-" };
            db.JobPostings.Add(p);
            return p;
        }

        var sales = Posting(Requisition(_factory.SalesDepartmentId, "Executive Sales", 5, RequisitionStatus.Approved, Utc(6, 1)));
        var finance = Posting(Requisition(_factory.FinanceDepartmentId, "Executive Finance", 4, RequisitionStatus.Approved, Utc(6, 20)));
        Requisition(_factory.FinanceDepartmentId, "Executive Pending", 6, RequisitionStatus.PendingApproval, null);

        var number = 9000;
        void Offer(JobPosting posting, Guid maker, OfferStatus status, DateTimeOffset applied, DateTimeOffset? responded, DateOnly expires)
        {
            var candidate = new Candidate { TenantId = tenant, FullName = $"Executive candidate {number}" };
            var application = new JobApplication
            {
                TenantId = tenant,
                CandidateId = candidate.Id,
                JobPostingId = posting.Id,
                AppliedAt = applied,
                Status = PipelineStatus.Offer,
            };
            db.Candidates.Add(candidate);
            db.JobApplications.Add(application);
            db.Offers.Add(new Offer
            {
                TenantId = tenant,
                JobApplicationId = application.Id,
                Year = 2026,
                Number = number++,
                Status = status,
                BaseSalary = 500_000,
                LetterBody = "-",
                CreatedByUserId = maker,
                JoiningDate = new DateOnly(2026, 10, 1),
                ExpiresOn = expires,
                RespondedAt = responded,
            });
        }

        var r = _factory.RecruiterUserId;
        var a = _factory.AdminUserId;
        Offer(sales, r, OfferStatus.Accepted, Utc(7, 1), Utc(7, 11), new DateOnly(2026, 7, 15));                     // O1
        Offer(sales, a, OfferStatus.Accepted, Utc(7, 21), Utc(8, 10), new DateOnly(2026, 8, 15));                    // O2
        Offer(finance, r, OfferStatus.Accepted, Utc(7, 1), Utc(7, 31), new DateOnly(2026, 8, 5));                    // O3
        Offer(finance, a, OfferStatus.Accepted, Utc(6, 20, 20), Utc(6, 30, 20), new DateOnly(2026, 7, 5));           // O9
        Offer(sales, r, OfferStatus.Rejected, Utc(7, 1), Utc(7, 20), new DateOnly(2026, 7, 25));                     // O4
        Offer(finance, a, OfferStatus.Viewed, Utc(7, 1), null, new DateOnly(2026, 8, 1));                            // O5
        Offer(sales, r, OfferStatus.Sent, Utc(8, 1), null, new DateOnly(2026, 8, 30));                               // O6
        Offer(sales, r, OfferStatus.Accepted, Utc(5, 16), Utc(6, 15), new DateOnly(2026, 6, 20));                    // O7
        Offer(finance, a, OfferStatus.Expired, Utc(5, 20), null, new DateOnly(2026, 6, 10));                         // O8
        db.Users.Add(new User
        {
            Id = BravoAdminId,
            TenantId = _factory.TenantB,
            Email = "admin@bravo.test",
            DisplayName = "Bravo Admin",
            Role = UserRole.Admin,
            IsActive = true,
            PasswordHash = "not-used-in-these-tests",
        });
        db.SaveChanges();
    }

    private async Task<ExecutiveDashboardDto> GetAsync(HttpClient client, string query = "")
    {
        var res = await client.GetAsync($"/api/analytics/executive{query}");
        Assert.True(res.IsSuccessStatusCode, $"{res.StatusCode}: {await res.Content.ReadAsStringAsync()}");
        return (await res.Content.ReadFromJsonAsync<ExecutiveDashboardDto>())!;
    }

    private HttpClient Admin() => _scenario.Client(Roles.Admin, _factory.AdminUserId);
    private HttpClient Recruiter() => _scenario.Client(Roles.Recruiter, _factory.RecruiterUserId);

    [Fact]
    public async Task Company_Wide_The_Clocks_End_At_Acceptance_And_The_Rate_Counts_Every_Outcome_In_The_Quarter()
    {
        var dto = await GetAsync(Admin());

        Assert.Equal("Company", dto.Scope);
        Assert.Equal(new ReportingPeriodDto("Quarter", new DateOnly(2026, 7, 1), new DateOnly(2026, 8, 18)), dto.Period);
        Assert.Equal(new ReportingPeriodDto("Quarter", new DateOnly(2026, 4, 1), new DateOnly(2026, 6, 30)), dto.Previous);

        // (10 + 20 + 30 + 10) / 4 — O9's acceptance was already 1 July in Yangon.
        Assert.Equal(17.5, dto.TimeToHire!.Days);
        Assert.Equal(4, dto.TimeToHire.Count);
        Assert.Equal(30.0, dto.TimeToHire.PreviousDays);
        Assert.Equal(1, dto.TimeToHire.PreviousCount);

        // (40 + 70 + 41 + 10.83) / 4 = 40.46
        Assert.Equal(40.5, dto.TimeToFill.Days);
        Assert.Equal(14.0, dto.TimeToFill.PreviousDays);

        // Accepted O1 O2 O3 O9, declined O4, expired O5 (still Viewed in the database); O6 still waiting.
        Assert.Equal(4, dto.Acceptance!.Accepted);
        Assert.Equal(6, dto.Acceptance.Decided);
        Assert.Equal(66.7, dto.Acceptance.Rate);
        Assert.Equal(50.0, dto.Acceptance.PreviousRate);

        // Seats less accepted offers, approved requisitions only.
        Assert.Equal(4, dto.OpenVacancies);
        Assert.Equal(new[] { 2, 2 }, dto.Vacancies.Select(v => v.Open));
        Assert.Equal(new[] { "Alpha Finance", "Alpha Sales" }, dto.Vacancies.Select(v => v.DepartmentName));

        Assert.Equal(new[] { _factory.AdminUserId, _factory.RecruiterUserId }.OrderBy(x => x), dto.Recruiters.Select(r => r.Id).OrderBy(x => x));
    }

    [Fact]
    public async Task A_Recruiter_Sees_Only_The_Offers_They_Made_Recomputed_Whatever_They_Ask_For()
    {
        foreach (var query in new[] { "", $"?recruiterId={_factory.AdminUserId}" })
        {
            var dto = await GetAsync(Recruiter(), query);
            Assert.Equal("Recruiter", dto.Scope);
            Assert.Equal(20.0, dto.TimeToHire!.Days);         // O1 10, O3 30
            Assert.Equal(40.5, dto.TimeToFill.Days);          // O1 40, O3 41
            Assert.Equal(2, dto.Acceptance!.Accepted);         // O1, O3 of O1 O3 O4
            Assert.Equal(3, dto.Acceptance.Decided);
            Assert.Equal(100.0, dto.Acceptance.PreviousRate);  // O7
            Assert.Empty(dto.Recruiters);
            // Vacancies are not anyone's performance: the company's.
            Assert.Equal(4, dto.OpenVacancies);
        }
    }

    [Fact]
    public async Task An_Admin_Narrows_By_Recruiter_And_By_Department()
    {
        var byRecruiter = await GetAsync(Admin(), $"?recruiterId={_factory.RecruiterUserId}");
        Assert.Equal(20.0, byRecruiter.TimeToHire!.Days);

        var finance = await GetAsync(Admin(), $"?departmentId={_factory.FinanceDepartmentId}");
        Assert.Equal(20.0, finance.TimeToHire!.Days);          // O3 30, O9 10
        Assert.Equal(2, finance.Acceptance!.Accepted);          // O3, O9 of O3 O9 O5
        Assert.Equal(3, finance.Acceptance.Decided);
        Assert.Equal(2, finance.OpenVacancies);
        Assert.Equal("Alpha Finance", Assert.Single(finance.Vacancies).DepartmentName);

        // The recruiter choices do not follow the department filter: a department nobody has made an
        // offer in still lists everyone who has.
        var none = await GetAsync(Admin(), $"?departmentId={Guid.NewGuid()}");
        Assert.Null(none.TimeToHire!.Days);
        Assert.Equal(new[] { _factory.AdminUserId, _factory.RecruiterUserId }.OrderBy(x => x), none.Recruiters.Select(r => r.Id).OrderBy(x => x));
    }

    [Fact]
    public async Task A_Hiring_Manager_Gets_Their_Departments_Time_To_Fill_And_Vacancies_Only()
    {
        var res = await _scenario.SalesManager().GetAsync("/api/analytics/executive");
        res.EnsureSuccessStatusCode();
        var json = await res.Content.ReadAsStringAsync();
        var dto = (await res.Content.ReadFromJsonAsync<ExecutiveDashboardDto>())!;

        Assert.Equal("Department", dto.Scope);
        Assert.Null(dto.TimeToHire);                            // omitted by the API, not hidden by the screen
        Assert.Null(dto.Acceptance);
        Assert.Equal(55.0, dto.TimeToFill.Days);                // O1 40, O2 70
        Assert.Equal(2, dto.OpenVacancies);
        Assert.Equal("Alpha Sales", Assert.Single(dto.Vacancies).DepartmentName);
        Assert.Equal(new[] { "Alpha Sales" }, dto.Departments.Select(d => d.Name));
        Assert.Empty(dto.Recruiters);
        Assert.DoesNotContain("\"accepted\"", json);

        // Another department is out of reach.
        Assert.Equal(HttpStatusCode.NotFound,
            (await _scenario.SalesManager().GetAsync($"/api/analytics/executive?departmentId={_factory.FinanceDepartmentId}")).StatusCode);
    }

    [Fact]
    public async Task An_Approver_Or_Interviewer_Sees_Nothing()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _scenario.FinanceApprover().GetAsync("/api/analytics/executive")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await _scenario.Client(Roles.Interviewer, _factory.HiringManagerUserId).GetAsync("/api/analytics/executive")).StatusCode);
    }

    [Fact]
    public async Task A_Hiring_Manager_Cannot_Narrow_To_A_Recruiter()
    {
        var res = await _scenario.SalesManager().GetAsync($"/api/analytics/executive?recruiterId={_factory.RecruiterUserId}");
        var dto = (await res.Content.ReadFromJsonAsync<ExecutiveDashboardDto>())!;
        Assert.Equal(55.0, dto.TimeToFill.Days);   // every recruiter's Sales offers, as without it
        Assert.Empty(dto.Recruiters);
    }

    [Fact]
    public async Task Another_Company_Sees_None_Of_It()
    {
        var bravo = _scenario.Client(Roles.Admin, BravoAdminId);
        bravo.DefaultRequestHeaders.Remove("X-Test-Tenant");
        bravo.DefaultRequestHeaders.Add("X-Test-Tenant", _factory.TenantB.ToString());

        var dto = await GetAsync(bravo);
        Assert.Null(dto.TimeToHire!.Days);
        Assert.Null(dto.TimeToFill.Days);
        Assert.Equal(0, dto.Acceptance!.Decided);
        Assert.Empty(dto.Vacancies);
        Assert.Empty(dto.Recruiters);
        Assert.Equal("Bravo Finance", Assert.Single(dto.Departments).Name);
    }

    [Fact]
    public async Task A_Custom_Role_Needs_Requisitions_Read_And_Then_Follows_Its_Base_Role()
    {
        async Task<HttpClient> HolderOf(params string[] permissions)
        {
            var role = await Admin().PostAsJsonAsync("/api/roles",
                new CreateRoleRequest($"Analytics {Guid.NewGuid():N}", null, "test", permissions, Roles.Recruiter));
            Assert.Equal(HttpStatusCode.Created, role.StatusCode);
            var roleId = (await role.Content.ReadFromJsonAsync<RoleDetailDto>())!.Id;
            var user = await Admin().PostAsJsonAsync("/api/users",
                new CreateUserRequest($"analytics.{Guid.NewGuid():N}@alpha.test", "Analytics Holder", "P@ssw0rd-Test-123", roleId));
            Assert.Equal(HttpStatusCode.Created, user.StatusCode);
            return _scenario.Client(Roles.Recruiter, (await user.Content.ReadFromJsonAsync<UserDetailDto>())!.Id);
        }

        // Trimmed of requisitions: the vacancies are requisitions' seats, so not here either.
        var trimmed = await HolderOf("permission:postings:postings:read");
        Assert.Equal(HttpStatusCode.Forbidden, (await trimmed.GetAsync("/api/analytics/executive")).StatusCode);

        // Holding it: a Recruiter base, so their own offers — none.
        var dto = await GetAsync(await HolderOf("permission:requisitions:requisitions:read"));
        Assert.Equal("Recruiter", dto.Scope);
        Assert.Null(dto.TimeToHire!.Days);
        Assert.Equal(0, dto.Acceptance!.Decided);
    }

    [Fact]
    public async Task Month_And_Year_Compare_With_The_Whole_Previous_One()
    {
        var month = await GetAsync(Admin(), "?period=Month");
        Assert.Equal(new DateOnly(2026, 8, 1), month.Period.From);
        Assert.Equal(new DateOnly(2026, 7, 1), month.Previous.From);
        Assert.Equal(new DateOnly(2026, 7, 31), month.Previous.To);
        Assert.Equal(20.0, month.TimeToHire!.Days);               // O2
        Assert.Equal(16.7, month.TimeToHire.PreviousDays);        // July: O1 10, O3 30, O9 10

        var year = await GetAsync(Admin(), "?period=Year");
        Assert.Equal(new DateOnly(2026, 1, 1), year.Period.From);
        Assert.Equal(20.0, year.TimeToHire!.Days);                // O1 O2 O3 O9 O7
        Assert.Null(year.TimeToHire.PreviousDays);                // nothing in 2025: no number, not zero
        Assert.Equal(0, year.TimeToHire.PreviousCount);
        Assert.Null(year.Acceptance!.PreviousRate);
    }

    [Theory]
    [InlineData("Week")]
    [InlineData("0")]
    [InlineData("Month,Year")]
    [InlineData("quarter")]
    public async Task A_Period_Is_Named_Exactly(string period)
    {
        var res = await Admin().GetAsync($"/api/analytics/executive?period={period}");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }
}
