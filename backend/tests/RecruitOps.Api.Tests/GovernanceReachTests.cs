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

/// <summary>Who reaches the governance screens — the user directory and departments (ADR-0019 and
/// ADR-0022 amendments, 2026-10-17; Module 7 §7.1 "Who sees the governance screens").
///
/// <para>Found by signing in as every role on a running install and opening every menu item. A
/// Recruiter's menu showed "Users"; the list answered 403, but the same permission let them read
/// any user's full record by id — every email in the company, using the ids the email-free picker
/// hands out. An HR Director's "Users" and "Departments" both answered 403.</para>
///
/// <para>Every client here is a <b>real user of the role it claims</b>. <c>[HasPermission]</c>
/// reads the user's own role from the database, not the test header — a "Recruiter" header on
/// the Admin's id is an Admin, and would pass these tests for the wrong reason.</para>
/// </summary>
public class GovernanceReachTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;

    public GovernanceReachTests(CustomWebAppFactory factory) => _factory = factory;

    private HttpClient As(string role, Guid userId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Tenant", _factory.TenantA.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Roles", role);
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString());
        return client;
    }

    private HttpClient Admin() => As(Roles.Admin, _factory.AdminUserId);
    private HttpClient Recruiter() => As(Roles.Recruiter, _factory.RecruiterUserId);

    private async Task<HttpClient> HrDirectorAsync()
    {
        var res = await Admin().PostAsJsonAsync("/api/users", new CreateUserRequest(
            $"hrd.{Guid.NewGuid():N}@alpha.test", "Hla HR Director", "Correct-Horse-9", Role: "HrDirector"));
        res.EnsureSuccessStatusCode();
        var user = (await res.Content.ReadFromJsonAsync<UserDetailDto>())!;
        return As(Roles.HrDirector, user.Id);
    }

    // ---- the user directory -----------------------------------------------------------

    [Fact]
    public async Task A_Recruiter_Cannot_Read_A_Users_Full_Record_By_Id()
    {
        // The leak, exactly as measured: ids from the picker, chained into the full record.
        var picker = await Recruiter().GetFromJsonAsync<List<SelectableUserDto>>("/api/users/selectable");
        Assert.NotEmpty(picker!);

        var full = await Recruiter().GetAsync($"/api/users/{picker![0].Id}");

        Assert.Equal(HttpStatusCode.Forbidden, full.StatusCode);
    }

    [Fact]
    public async Task A_Recruiter_Still_Names_A_Panel_Through_The_Picker_And_Not_The_Directory()
    {
        Assert.Equal(HttpStatusCode.OK, (await Recruiter().GetAsync("/api/users/selectable")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Recruiter().GetAsync("/api/users")).StatusCode);
    }

    [Fact]
    public async Task An_HrDirector_Reads_The_Directory_But_Cannot_Change_It()
    {
        var hrd = await HrDirectorAsync();

        Assert.Equal(HttpStatusCode.OK, (await hrd.GetAsync("/api/users")).StatusCode);
        var create = await hrd.PostAsJsonAsync("/api/users", new CreateUserRequest(
            $"x.{Guid.NewGuid():N}@alpha.test", "Not Created", "Correct-Horse-9", Role: "Recruiter"));
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
    }

    [Theory]
    [InlineData(Roles.HiringManager)]
    [InlineData(Roles.Approver)]
    public async Task Other_Roles_Get_No_Directory(string role)
    {
        var userId = role == Roles.HiringManager ? _factory.HiringManagerUserId : _factory.FinanceApproverUserId;
        Assert.Equal(HttpStatusCode.Forbidden, (await As(role, userId).GetAsync("/api/users")).StatusCode);
    }

    [Fact]
    public async Task An_Admin_Still_Reads_And_Manages_The_Directory()
    {
        Assert.Equal(HttpStatusCode.OK, (await Admin().GetAsync("/api/users")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Admin().GetAsync($"/api/users/{_factory.RecruiterUserId}")).StatusCode);
    }

    // ---- departments -----------------------------------------------------------------

    [Fact]
    public async Task An_HrDirector_Reads_Departments_But_Cannot_Change_Them()
    {
        var hrd = await HrDirectorAsync();

        Assert.Equal(HttpStatusCode.OK, (await hrd.GetAsync("/api/departments/admin")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await hrd.GetAsync($"/api/departments/{_factory.SalesDepartmentId}/members")).StatusCode);

        var create = await hrd.PostAsJsonAsync("/api/departments", new { name = "Not Created", code = "NOPE" });
        var members = await hrd.PutAsJsonAsync($"/api/departments/{_factory.SalesDepartmentId}/members",
            new { userIds = Array.Empty<Guid>() });
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        // Membership is the department-scoping axis (ADR-0003): reading it must not mean moving it.
        Assert.Equal(HttpStatusCode.Forbidden, members.StatusCode);
    }

    [Fact]
    public async Task A_Recruiter_Gets_No_Department_Administration()
    {
        Assert.Equal(HttpStatusCode.Forbidden, (await Recruiter().GetAsync("/api/departments/admin")).StatusCode);
    }

    // ---- the seed is the definition of a system role ---------------------------------

    [Fact]
    public async Task Startup_Seeding_Removes_A_Permission_A_System_Role_No_Longer_Has()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // An install from before 2026-10-17: its Recruiter system role still holds users:read.
        var recruiterRole = await db.Roles.IgnoreQueryFilters().Include(r => r.RolePermissions)
            .SingleAsync(r => r.IsSystemRole && r.Code == "Recruiter");
        var usersRead = await db.Permissions.IgnoreQueryFilters()
            .SingleAsync(p => p.Code == "permission:users:users:read");
        if (recruiterRole.RolePermissions.All(rp => rp.PermissionId != usersRead.Id))
        {
            recruiterRole.RolePermissions.Add(new RolePermission
            {
                RoleId = recruiterRole.Id,
                PermissionId = usersRead.Id,
                AssignedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        await DbInitializer.SeedPermissionsAndRolesAsync(db);

        var after = await db.RolePermissions.IgnoreQueryFilters()
            .AnyAsync(rp => rp.RoleId == recruiterRole.Id && rp.PermissionId == usersRead.Id);
        Assert.False(after);

        // …and the system says so, like every other access change (ADR-0027).
        var entry = await db.AuditLogEntries.IgnoreQueryFilters()
            .Where(e => e.Action == AuditAction.RoleUpdated && e.TargetId == recruiterRole.Id
                        && e.ActorName == "System (startup)")
            .OrderByDescending(e => e.OccurredAt).FirstOrDefaultAsync();
        Assert.NotNull(entry);
        Assert.Contains("permission:users:users:read", entry!.DetailsJson);
    }
}
