using System.Net;
using System.Net.Http.Json;
using RecruitOps.Api.Auth;
using RecruitOps.Application.DTOs;
using Xunit;

namespace RecruitOps.Api.Tests;

/// <summary>ADR-0019 — the panel picker got a narrower directory, not a wider policy.
///
/// <para>Two endpoints now live on one controller under two different policies, which is the
/// shape most likely to be "simplified" later by someone who reads the class-level
/// <c>AdminOnly</c> and the method-level <c>RecruitmentStaff</c> as a contradiction. It is not
/// one: the wider audience gets the narrower payload. These tests pin both halves, because
/// either half alone is a bug — a Recruiter who cannot list users cannot schedule an interview
/// at all (the panel is required and non-empty, ADR-0017), and a Recruiter who can read
/// <c>GET /api/users</c> has been handed every email address in the company.</para>
///
/// <para>The gap this closes was itself invisible to a test suite: Module 3's tests post user
/// ids they already hold, so nothing ever asked whether the role the endpoint was opened to
/// could obtain one. Opening an endpoint to a role means walking the whole flow as that role,
/// lookups included.</para>
/// </summary>
public class UserDirectoryTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;
    private readonly Module3Scenario _scenario;

    public UserDirectoryTests(CustomWebAppFactory factory)
    {
        _factory = factory;
        _scenario = new Module3Scenario(factory);
    }

    // ---- The policy boundary itself -------------------------------------------------

    [Fact]
    public async Task A_Recruiter_Reads_Selectable_But_Is_Still_Refused_The_Full_Directory()
    {
        var recruiter = _scenario.Client(Roles.Recruiter, _factory.AdminUserId);

        var selectable = await recruiter.GetAsync("/api/users/selectable");
        var full = await recruiter.GetAsync("/api/users");

        // Both assertions in one case on purpose: they are the two halves of one decision,
        // and a future edit that widens `Get` would otherwise leave a green test named
        // "a recruiter can read selectable" standing over the hole.
        Assert.Equal(HttpStatusCode.OK, selectable.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, full.StatusCode);
    }

    [Fact]
    public async Task An_HrDirector_Gets_The_Same_Split_As_A_Recruiter()
    {
        // RecruitmentStaff is three roles, and the picker has to work for all of them —
        // "adding a rule to two of three siblings" is this repo's recurring bug.
        var hrDirector = _scenario.Client(Roles.HrDirector, _factory.AdminUserId);

        var selectable = await hrDirector.GetAsync("/api/users/selectable");
        var full = await hrDirector.GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.OK, selectable.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, full.StatusCode);
    }

    [Fact]
    public async Task An_Admin_Still_Reads_Both()
    {
        var admin = _scenario.Client(Roles.Admin, _factory.AdminUserId);

        var selectable = await admin.GetAsync("/api/users/selectable");
        var full = await admin.GetAsync("/api/users");

        // The approval-chain builder is an Admin task and must not have been broken by
        // opting one method down to a weaker policy.
        Assert.Equal(HttpStatusCode.OK, selectable.StatusCode);
        Assert.Equal(HttpStatusCode.OK, full.StatusCode);
    }

    [Theory]
    [InlineData(Roles.HiringManager)]
    [InlineData(Roles.Approver)]
    public async Task Neither_Directory_Opens_To_A_Role_Outside_RecruitmentStaff(string role)
    {
        var userId = role == Roles.HiringManager
            ? _factory.HiringManagerUserId
            : _factory.FinanceApproverUserId;
        var client = _scenario.Client(role, userId);

        var selectable = await client.GetAsync("/api/users/selectable");
        var full = await client.GetAsync("/api/users");

        // 403, not 404: unlike a candidate row, the existence of a user directory is not a
        // secret, and there is nothing here whose existence a status code could leak.
        Assert.Equal(HttpStatusCode.Forbidden, selectable.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, full.StatusCode);
    }

    [Fact]
    public async Task An_Unauthenticated_Caller_Gets_401_On_Selectable()
    {
        // No X-Test-Tenant header — the new route is not accidentally anonymous.
        var res = await _factory.CreateClient().GetAsync("/api/users/selectable");

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    // ---- The payload, which is the reason the policy could be widened at all ---------

    [Fact]
    public async Task The_Selectable_Payload_Carries_No_Email_Address()
    {
        var res = await _scenario.Client(Roles.Recruiter, _factory.AdminUserId)
            .GetAsync("/api/users/selectable");
        res.EnsureSuccessStatusCode();
        var json = await res.Content.ReadAsStringAsync();

        // Asserted against the raw JSON, not a deserialised SelectableUserDto. Reading into
        // the DTO would drop an email property silently and report green — the whole argument
        // of ADR-0019 is about what crosses the wire, so that is what is inspected.
        Assert.DoesNotContain("email", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@", json, StringComparison.Ordinal);

        // And the seeded addresses specifically, in case a future field carries one under
        // another name.
        Assert.DoesNotContain(CustomWebAppFactory.AdminEmail, json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(CustomWebAppFactory.HiringManagerEmail, json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Selectable_Returns_What_A_Picker_Needs_Id_Name_And_Role()
    {
        var users = await _scenario.Client(Roles.Recruiter, _factory.AdminUserId)
            .GetFromJsonAsync<List<SelectableUserDto>>("/api/users/selectable");

        var admin = Assert.Single(users!, u => u.Id == _factory.AdminUserId);
        Assert.Equal("Alpha Admin", admin.DisplayName);

        // Role is projected in memory because EF Core 10 will not translate enum.ToString().
        // If someone folds this back into the query, the endpoint throws against Postgres and
        // this assertion is the only thing between that and production.
        Assert.Equal("Admin", admin.Role);
        Assert.All(users!, u => Assert.False(string.IsNullOrWhiteSpace(u.Role)));
        Assert.All(users!, u => Assert.NotEqual(Guid.Empty, u.Id));
    }

    [Fact]
    public async Task An_Approver_Is_Deliberately_Selectable()
    {
        var users = await _scenario.Client(Roles.Recruiter, _factory.AdminUserId)
            .GetFromJsonAsync<List<SelectableUserDto>>("/api/users/selectable");

        // ADR-0018 removed an Approver's *standing* reach into candidate data, and it reads
        // at a glance like "keep them off the panel list". It says the opposite: panel
        // membership is precisely how an excluded role reaches one application, on purpose
        // (ADR-0017 §4). Filtering them out here would delete that escape hatch in a
        // controller, far from the ADR that granted it — so it is pinned here.
        var approver = Assert.Single(users!, u => u.Id == _factory.FinanceApproverUserId);
        Assert.Equal("Approver", approver.Role);
    }

    [Fact]
    public async Task Selectable_Is_Not_Department_Scoped()
    {
        var users = await _scenario.Client(Roles.Recruiter, _factory.AdminUserId)
            .GetFromJsonAsync<List<SelectableUserDto>>("/api/users/selectable");

        // A Finance interviewer on a Sales hire is the normal case, not the exception. If the
        // picker were scoped, the cross-department panel would be unbuildable from the UI
        // while the API that accepts it stayed wide open.
        Assert.Contains(users!, u => u.Id == _factory.HiringManagerUserId);
        Assert.Contains(users!, u => u.Id == _factory.FinanceManagerUserId);
    }

    [Fact]
    public async Task Selectable_Is_Ordered_By_Display_Name()
    {
        var users = await _scenario.Client(Roles.Recruiter, _factory.AdminUserId)
            .GetFromJsonAsync<List<SelectableUserDto>>("/api/users/selectable");

        // The picker is a dropdown; ordering is part of its contract, and the OrderBy sits
        // before the in-memory projection where it is easy to lose in a refactor.
        // Sorted with the default comparer rather than an explicit one, so the assertion uses
        // the same collation as the provider and fails on "not ordered", not on "ordered
        // differently than this test's opinion of alphabetical".
        var names = users!.Select(u => u.DisplayName).ToList();
        Assert.Equal(names.OrderBy(n => n).ToList(), names);
    }

    [Fact]
    public async Task Selectable_Does_Not_Cross_Tenants()
    {
        var users = await _scenario.Client(Roles.Recruiter, _factory.AdminUserId)
            .GetFromJsonAsync<List<SelectableUserDto>>("/api/users/selectable");

        // The global query filter is the safety net here (ADR-0004); the endpoint applies no
        // tenant predicate of its own, so this asserts the net is actually under it.
        var tenantBClient = _scenario.Client(Roles.Recruiter, _factory.AdminUserId);
        tenantBClient.DefaultRequestHeaders.Remove("X-Test-Tenant");
        tenantBClient.DefaultRequestHeaders.Add("X-Test-Tenant", _factory.TenantB.ToString());

        var fromTenantB = await tenantBClient
            .GetFromJsonAsync<List<SelectableUserDto>>("/api/users/selectable");

        Assert.NotEmpty(users!);
        Assert.Empty(fromTenantB!);
    }
}
