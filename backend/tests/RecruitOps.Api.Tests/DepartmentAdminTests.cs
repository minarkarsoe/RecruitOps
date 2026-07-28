using System.Net;
using System.Net.Http.Json;
using RecruitOps.Api.Auth;
using RecruitOps.Application.DTOs;
using Xunit;

namespace RecruitOps.Api.Tests;

/// <summary>Department administration.
///
/// <para>Departments are not just labels — membership is the axis department scoping is
/// applied along (ADR-0003), so editing this list grants and revokes access to
/// requisitions. Most of what is asserted here is about who may do that and what must not
/// be quietly lost when they do.</para>
/// </summary>
public class DepartmentAdminTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;

    public DepartmentAdminTests(CustomWebAppFactory factory) => _factory = factory;

    private HttpClient ClientFor(string role, Guid? userId = null)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Tenant", _factory.TenantA.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Roles", role);
        if (userId is not null)
            client.DefaultRequestHeaders.Add("X-Test-UserId", userId.Value.ToString());
        return client;
    }

    private async Task<DepartmentDetailDto> CreateAsync(string name, string? code = null)
    {
        var res = await ClientFor(Roles.Admin, _factory.AdminUserId)
            .PostAsJsonAsync("/api/departments", new CreateDepartmentRequest { Name = name, Code = code });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<DepartmentDetailDto>())!;
    }

    // ── Authority ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Only_An_Admin_Can_Create_A_Department()
    {
        var request = new CreateDepartmentRequest { Name = "Engineering" };

        // Creating a department creates a scope that requisitions live in, and whoever can
        // create one can put themselves in it. Same authority as editing an approval chain.
        foreach (var role in new[] { Roles.Recruiter, Roles.HrDirector, Roles.HiringManager })
        {
            var denied = await ClientFor(role, _factory.AdminUserId).PostAsJsonAsync("/api/departments", request);
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        }

        var allowed = await ClientFor(Roles.Admin, _factory.AdminUserId)
            .PostAsJsonAsync("/api/departments", request);
        Assert.Equal(HttpStatusCode.Created, allowed.StatusCode);
    }

    [Fact]
    public async Task Only_An_Admin_Can_Read_Or_Write_Membership()
    {
        var department = await CreateAsync("Membership Authority");

        var read = await ClientFor(Roles.Recruiter).GetAsync($"/api/departments/{department.Id}/members");
        Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);

        var write = await ClientFor(Roles.HrDirector).PutAsJsonAsync(
            $"/api/departments/{department.Id}/members",
            new SetDepartmentMembersRequest { UserIds = [_factory.HiringManagerUserId] });
        Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);
    }

    // ── The bug this screen exists to fix ────────────────────────────────────

    [Fact]
    public async Task A_Hiring_Manager_Can_Load_Their_Own_Department_Picker()
    {
        // This endpoint feeds the department dropdown on the new-requisition form. It used
        // to require RecruitmentStaff, which excludes HiringManager — so the dropdown was
        // always empty and a hiring manager could not raise a requisition through the UI at
        // all, while the API happily accepted one.
        var res = await ClientFor(Roles.HiringManager, _factory.HiringManagerUserId).GetAsync("/api/departments");
        res.EnsureSuccessStatusCode();

        var list = (await res.Content.ReadFromJsonAsync<List<DepartmentListItemDto>>())!;
        Assert.Contains(list, d => d.Id == _factory.SalesDepartmentId);
        // ...and only their own: Finance belongs to someone else (ADR-0003).
        Assert.DoesNotContain(list, d => d.Id == _factory.FinanceDepartmentId);
    }

    // ── Naming ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Duplicate_Names_Are_Refused_With_A_Readable_Message()
    {
        await CreateAsync("Customer Support");

        var again = await ClientFor(Roles.Admin, _factory.AdminUserId)
            .PostAsJsonAsync("/api/departments", new CreateDepartmentRequest { Name = "Customer Support" });

        // The unique index would refuse this anyway; the point is that an admin gets a
        // sentence rather than a constraint violation.
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Renaming_To_An_Existing_Name_Is_Refused_But_Keeping_Its_Own_Is_Fine()
    {
        var first = await CreateAsync("Logistics");
        await CreateAsync("Procurement");

        var clash = await ClientFor(Roles.Admin, _factory.AdminUserId).PutAsJsonAsync(
            $"/api/departments/{first.Id}", new UpdateDepartmentRequest { Name = "Procurement" });
        Assert.Equal(HttpStatusCode.Conflict, clash.StatusCode);

        // Saving a department without changing its name must not collide with itself.
        var sameName = await ClientFor(Roles.Admin, _factory.AdminUserId).PutAsJsonAsync(
            $"/api/departments/{first.Id}", new UpdateDepartmentRequest { Name = "Logistics", Code = "LOG" });
        sameName.EnsureSuccessStatusCode();
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_Department_With_Work_In_Progress_Cannot_Be_Deactivated()
    {
        var department = await CreateAsync("Temporary Team");

        // Give the admin access, then raise a Draft in it.
        await ClientFor(Roles.Admin, _factory.AdminUserId).PutAsJsonAsync(
            $"/api/departments/{department.Id}/members",
            new SetDepartmentMembersRequest { UserIds = [_factory.AdminUserId] });

        var draft = await ClientFor(Roles.Admin, _factory.AdminUserId)
            .PostAsJsonAsync("/api/requisitions", new CreateRequisitionRequest
            {
                DepartmentId = department.Id,
                Title = "Stranded Role",
                JobDescription = "Mid-flight.",
                Headcount = 1,
            });
        draft.EnsureSuccessStatusCode();

        // Deactivating now would strand that requisition: nobody can finish an approval
        // chain in a department that no longer accepts work.
        var res = await ClientFor(Roles.Admin, _factory.AdminUserId)
            .PostAsync($"/api/departments/{department.Id}/deactivate", null);
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task Deactivating_Removes_It_From_The_Picker_But_Keeps_Its_Members()
    {
        var department = await CreateAsync("Seasonal Team");
        await ClientFor(Roles.Admin, _factory.AdminUserId).PutAsJsonAsync(
            $"/api/departments/{department.Id}/members",
            new SetDepartmentMembersRequest { UserIds = [_factory.HiringManagerUserId] });

        var off = await ClientFor(Roles.Admin, _factory.AdminUserId)
            .PostAsync($"/api/departments/{department.Id}/deactivate", null);
        off.EnsureSuccessStatusCode();

        // Gone from the list people raise work against...
        var picker = await ClientFor(Roles.Admin, _factory.AdminUserId)
            .GetFromJsonAsync<List<DepartmentListItemDto>>("/api/departments");
        Assert.DoesNotContain(picker!, d => d.Id == department.Id);

        // ...but still in the admin list, and its membership survives: reactivating later
        // must not silently come back with nobody in it.
        var admin = await ClientFor(Roles.Admin, _factory.AdminUserId)
            .GetFromJsonAsync<List<DepartmentDetailDto>>("/api/departments/admin");
        var row = Assert.Single(admin!, d => d.Id == department.Id);
        Assert.False(row.IsActive);
        Assert.Equal(1, row.MemberCount);

        var on = await ClientFor(Roles.Admin, _factory.AdminUserId)
            .PostAsync($"/api/departments/{department.Id}/activate", null);
        on.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task A_Requisition_Cannot_Be_Raised_In_An_Inactive_Department()
    {
        var department = await CreateAsync("Closing Team");
        await ClientFor(Roles.Admin, _factory.AdminUserId).PutAsJsonAsync(
            $"/api/departments/{department.Id}/members",
            new SetDepartmentMembersRequest { UserIds = [_factory.AdminUserId] });
        await ClientFor(Roles.Admin, _factory.AdminUserId)
            .PostAsync($"/api/departments/{department.Id}/deactivate", null);

        var res = await ClientFor(Roles.Admin, _factory.AdminUserId)
            .PostAsJsonAsync("/api/requisitions", new CreateRequisitionRequest
            {
                DepartmentId = department.Id,
                Title = "Too Late",
                JobDescription = "The company stopped hiring here.",
                Headcount = 1,
            });

        // Hiding it from the picker isn't enough — the API is the boundary, not the UI.
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    // ── Membership ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Setting_Members_Grants_And_Revokes_Requisition_Visibility()
    {
        var department = await CreateAsync("Visibility Team");

        // Not a member yet: the department must not appear in their picker.
        var before = await ClientFor(Roles.HiringManager, _factory.HiringManagerUserId)
            .GetFromJsonAsync<List<DepartmentListItemDto>>("/api/departments");
        Assert.DoesNotContain(before!, d => d.Id == department.Id);

        await ClientFor(Roles.Admin, _factory.AdminUserId).PutAsJsonAsync(
            $"/api/departments/{department.Id}/members",
            new SetDepartmentMembersRequest { UserIds = [_factory.HiringManagerUserId] });

        var after = await ClientFor(Roles.HiringManager, _factory.HiringManagerUserId)
            .GetFromJsonAsync<List<DepartmentListItemDto>>("/api/departments");
        Assert.Contains(after!, d => d.Id == department.Id);

        // Revoking is the same call with an empty list — and must take effect immediately,
        // not when a token expires (that is why access is a DB lookup, not a claim).
        await ClientFor(Roles.Admin, _factory.AdminUserId).PutAsJsonAsync(
            $"/api/departments/{department.Id}/members",
            new SetDepartmentMembersRequest { UserIds = [] });

        var revoked = await ClientFor(Roles.HiringManager, _factory.HiringManagerUserId)
            .GetFromJsonAsync<List<DepartmentListItemDto>>("/api/departments");
        Assert.DoesNotContain(revoked!, d => d.Id == department.Id);
    }

    [Fact]
    public async Task An_Unknown_User_Id_Fails_Loudly_Rather_Than_Being_Skipped()
    {
        var department = await CreateAsync("Strict Team");

        var res = await ClientFor(Roles.Admin, _factory.AdminUserId).PutAsJsonAsync(
            $"/api/departments/{department.Id}/members",
            new SetDepartmentMembersRequest { UserIds = [_factory.HiringManagerUserId, Guid.NewGuid()] });

        // Skipping it quietly would leave an admin believing they granted access that
        // nobody has — an invisible failure on the access-control path.
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);

        // And nothing was half-applied.
        var members = await ClientFor(Roles.Admin, _factory.AdminUserId)
            .GetFromJsonAsync<List<DepartmentMemberDto>>($"/api/departments/{department.Id}/members");
        Assert.DoesNotContain(members!, m => m.IsMember);
    }

    [Fact]
    public async Task The_Member_Roster_Lists_Everyone_With_A_Membership_Flag()
    {
        var department = await CreateAsync("Roster Team");
        await ClientFor(Roles.Admin, _factory.AdminUserId).PutAsJsonAsync(
            $"/api/departments/{department.Id}/members",
            new SetDepartmentMembersRequest { UserIds = [_factory.HiringManagerUserId] });

        var members = await ClientFor(Roles.Admin, _factory.AdminUserId)
            .GetFromJsonAsync<List<DepartmentMemberDto>>($"/api/departments/{department.Id}/members");

        // The whole roster, because that is what an admin assigns from — a list of only
        // current members gives them no way to add anyone.
        Assert.True(members!.Count > 1);
        Assert.True(members.Single(m => m.UserId == _factory.HiringManagerUserId).IsMember);
        Assert.False(members.Single(m => m.UserId == _factory.AdminUserId).IsMember);
    }
}
