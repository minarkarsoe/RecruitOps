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

    private const string RequisitionsRead = "permission:requisitions:requisitions:read";
    private const string SettingsRead = "permission:settings:settings:read";
    private const string SettingsUpdate = "permission:settings:settings:update";
    private const string UsersRead = "permission:users:users:read";

    /// <summary>A real holder of a fresh custom role, signed in as its base role — what the token
    /// carries (ADR-0021 option A).</summary>
    private async Task<(HttpClient Client, Guid UserId, Guid RoleId)> CustomRoleHolderAsync(
        string baseRole, params string[] permissions)
    {
        var role = await Admin().PostAsJsonAsync("/api/roles", new CreateRoleRequest(
            $"Probe {Guid.NewGuid():N}", null, "governance reach probe", permissions, baseRole));
        role.EnsureSuccessStatusCode();
        var roleId = (await role.Content.ReadFromJsonAsync<RoleDetailDto>())!.Id;

        var user = await Admin().PostAsJsonAsync("/api/users", new CreateUserRequest(
            $"probe.{Guid.NewGuid():N}@alpha.test", "Probe Holder", "Correct-Horse-9", RoleId: roleId));
        user.EnsureSuccessStatusCode();
        var userId = (await user.Content.ReadFromJsonAsync<UserDetailDto>())!.Id;
        return (As(baseRole, userId), userId, roleId);
    }

    /// <summary>A 403 also comes back for an id the evaluator cannot resolve, so every denial below
    /// is paired with an allow for the same identity — a mis-wired client fails here instead of
    /// passing (security review, 2026-10-17).</summary>
    private static async Task AssertIdentityResolvesAsync(HttpClient client) =>
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/requisitions")).StatusCode);

    // ---- the user directory -----------------------------------------------------------

    [Fact]
    public async Task A_Recruiter_Cannot_Read_A_Users_Full_Record_By_Id()
    {
        await AssertIdentityResolvesAsync(Recruiter());

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
    public async Task A_Recruiter_Setting_A_Panel_Gets_Names_Back_Not_Emails()
    {
        // The second route to the same leak (security review, 2026-10-17, M1): name everyone the
        // picker offers to a panel, and read what the panel response says about them.
        var (_, applicationId) = await new Module3Scenario(_factory).ApplicationAsync("Panel Email Probe");
        var picker = await Recruiter().GetFromJsonAsync<List<SelectableUserDto>>("/api/users/selectable");
        var everyone = picker!.Select(u => u.Id).Take(PanelLimits.MaxParticipants).ToArray();

        var res = await Recruiter().PostAsJsonAsync($"/api/applications/{applicationId}/interviews",
            new ScheduleInterviewRequest
            {
                ScheduledStart = DateTimeOffset.UtcNow.AddDays(3),
                Mode = "Video",
                ParticipantUserIds = everyone,
            });

        res.EnsureSuccessStatusCode();
        var json = await res.Content.ReadAsStringAsync();
        Assert.Contains("displayName", json);
        Assert.DoesNotContain("email", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@alpha.test", json);
    }

    [Fact]
    public async Task A_Panel_Cannot_Name_The_Whole_Company()
    {
        var (_, applicationId) = await new Module3Scenario(_factory).ApplicationAsync("Panel Size Probe");

        var res = await Recruiter().PostAsJsonAsync($"/api/applications/{applicationId}/interviews",
            new ScheduleInterviewRequest
            {
                ScheduledStart = DateTimeOffset.UtcNow.AddDays(3),
                Mode = "Video",
                ParticipantUserIds = Enumerable.Range(0, PanelLimits.MaxParticipants + 1)
                    .Select(_ => Guid.NewGuid()).ToArray(),
            });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
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
        await AssertIdentityResolvesAsync(As(role, userId));
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
        var roster = await hrd.GetFromJsonAsync<List<DepartmentMemberDto>>(
            $"/api/departments/{_factory.SalesDepartmentId}/members");

        var create = await hrd.PostAsJsonAsync("/api/departments", new { name = "Not Created", code = "NOPE" });
        // The probe sends the membership as it already is: if the guard ever regressed, this test
        // must fail without first wiping the fixture's Sales department.
        var members = await hrd.PutAsJsonAsync($"/api/departments/{_factory.SalesDepartmentId}/members",
            new { userIds = roster!.Where(m => m.IsMember).Select(m => m.UserId).ToArray() });
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        // Membership is the department-scoping axis (ADR-0003): reading it must not mean moving it.
        Assert.Equal(HttpStatusCode.Forbidden, members.StatusCode);
    }

    [Fact]
    public async Task A_Recruiter_Gets_No_Department_Administration()
    {
        await AssertIdentityResolvesAsync(Recruiter());
        Assert.Equal(HttpStatusCode.Forbidden, (await Recruiter().GetAsync("/api/departments/admin")).StatusCode);
    }

    [Theory]
    [InlineData(Roles.HiringManager)]
    [InlineData(Roles.Interviewer)]
    [InlineData(Roles.Approver)]
    public async Task A_Role_Without_Company_Wide_Reach_Administers_No_Department_Whatever_It_Holds(string baseRole)
    {
        // An Admin can build this role: Admin holds every permission here and covers every base.
        // What it must not become is a way out of the base: membership IS a scoped role's reach
        // (security review, 2026-10-17, M2) — and an Approver, who reaches no candidate, moving a
        // Hiring Manager into every department would hand out candidate reach it does not hold
        // (second review, 2026-10-19, F1).
        var (holder, holderId, _) = await CustomRoleHolderAsync(baseRole, RequisitionsRead, SettingsRead, SettingsUpdate);
        await AssertIdentityResolvesAsync(holder);

        // Today's members plus the holder, so a regression adds one person instead of emptying the
        // fixture's Sales department under every test that runs after this one.
        var roster = await Admin().GetFromJsonAsync<List<DepartmentMemberDto>>(
            $"/api/departments/{_factory.SalesDepartmentId}/members");
        var addSelf = await holder.PutAsJsonAsync($"/api/departments/{_factory.SalesDepartmentId}/members",
            new SetDepartmentMembersRequest
            {
                UserIds = [.. roster!.Where(m => m.IsMember).Select(m => m.UserId), holderId],
            });
        var create = await holder.PostAsJsonAsync("/api/departments", new CreateDepartmentRequest { Name = $"Mine {Guid.NewGuid():N}" });

        Assert.Equal(HttpStatusCode.Forbidden, addSelf.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await holder.GetAsync("/api/departments/admin")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await holder.GetAsync($"/api/departments/{_factory.SalesDepartmentId}/members")).StatusCode);
        // Naming yourself on a chain opens every requisition it governs — the same way out. The
        // chain is for a department of its own, so a regression cannot reroute Sales' approvals.
        var scratch = await Admin().PostAsJsonAsync("/api/departments",
            new CreateDepartmentRequest { Name = $"Chain Probe {Guid.NewGuid():N}" });
        var scratchId = (await scratch.Content.ReadFromJsonAsync<DepartmentDetailDto>())!.Id;
        var chain = await holder.PostAsJsonAsync("/api/approvalchains", new CreateApprovalChainRequest
        {
            Name = "Mine",
            DepartmentId = scratchId,
            Steps = [new CreateApprovalChainStepRequest { ApproverUserId = holderId, Label = "Me" }],
        });
        Assert.Equal(HttpStatusCode.Forbidden, (await holder.GetAsync("/api/approvalchains")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, chain.StatusCode);
    }

    [Fact]
    public async Task Changing_A_User_Needs_Reading_Them_Because_The_Answer_Is_Their_Record()
    {
        // PUT /api/users/{id} answers with the full record — email, permissions. A custom role given
        // users:update without users:read could rename anyone its base covers to read them
        // (second review, 2026-10-19, F2b).
        var (holder, _, _) = await CustomRoleHolderAsync(Roles.Recruiter, RequisitionsRead, "permission:users:users:update");
        await AssertIdentityResolvesAsync(holder);
        var target = await Admin().PostAsJsonAsync("/api/users", new CreateUserRequest(
            $"hm.{Guid.NewGuid():N}@alpha.test", "Target Manager", "Correct-Horse-9", Role: "HiringManager"));
        var targetId = (await target.Content.ReadFromJsonAsync<UserDetailDto>())!.Id;

        var rename = await holder.PutAsJsonAsync($"/api/users/{targetId}", new UpdateUserRequest("Target Manager"));
        var reactivate = await holder.PutAsync($"/api/users/{targetId}/reactivate", null);

        Assert.Equal(HttpStatusCode.Forbidden, rename.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, reactivate.StatusCode);
        Assert.DoesNotContain("@alpha.test", await rename.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Settings_Writes_Are_Bounded()
    {
        // Every submission writes one approval row per chain step, and a member list is one IN
        // list: neither should be arbitrarily large (second review, 2026-10-19, F3).
        var steps = Enumerable.Range(0, 21)
            .Select(i => new CreateApprovalChainStepRequest { ApproverUserId = _factory.FinanceApproverUserId, Label = $"Step {i}" })
            .ToList();
        var chain = await Admin().PostAsJsonAsync("/api/approvalchains",
            new CreateApprovalChainRequest { Name = "Too long", Steps = steps });
        var members = await Admin().PutAsJsonAsync($"/api/departments/{_factory.SalesDepartmentId}/members",
            new SetDepartmentMembersRequest { UserIds = Enumerable.Range(0, 5001).Select(_ => Guid.NewGuid()).ToArray() });

        Assert.Equal(HttpStatusCode.BadRequest, chain.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, members.StatusCode);
    }

    [Fact]
    public async Task A_Company_Wide_Custom_Role_With_Settings_Read_Sees_The_Roster_Without_Addresses()
    {
        // settings:read is not users:read. The roster is every active user, so with emails it would
        // be the directory under another permission (security review, 2026-10-17, L1).
        var (holder, _, _) = await CustomRoleHolderAsync(Roles.Recruiter, RequisitionsRead, SettingsRead);

        var roster = await holder.GetFromJsonAsync<List<DepartmentMemberDto>>(
            $"/api/departments/{_factory.SalesDepartmentId}/members");
        Assert.NotEmpty(roster!);
        Assert.All(roster!, m => Assert.Null(m.Email));

        // Admin and HR Director hold users:read, so their screen keeps the addresses.
        var asAdmin = await Admin().GetFromJsonAsync<List<DepartmentMemberDto>>(
            $"/api/departments/{_factory.SalesDepartmentId}/members");
        Assert.All(asAdmin!, m => Assert.NotNull(m.Email));
    }

    [Fact]
    public async Task Department_Changes_Are_In_The_Audit_Log()
    {
        // Delegable since 2026-10-17, so who did it has to be answerable (security review, L4).
        var created = await Admin().PostAsJsonAsync("/api/departments",
            new CreateDepartmentRequest { Name = $"Audited {Guid.NewGuid():N}" });
        created.EnsureSuccessStatusCode();
        var department = (await created.Content.ReadFromJsonAsync<DepartmentDetailDto>())!;

        (await Admin().PutAsJsonAsync($"/api/departments/{department.Id}",
            new UpdateDepartmentRequest { Name = department.Name + " Renamed" })).EnsureSuccessStatusCode();
        (await Admin().PostAsync($"/api/departments/{department.Id}/deactivate", null)).EnsureSuccessStatusCode();
        (await Admin().PostAsync($"/api/departments/{department.Id}/activate", null)).EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var actions = await db.AuditLogEntries.IgnoreQueryFilters()
            .Where(e => e.TargetId == department.Id)
            .OrderBy(e => e.OccurredAt)
            .Select(e => new { e.Action, e.ActorUserId })
            .ToListAsync();

        Assert.Equal(
            new[] { AuditAction.DepartmentCreated, AuditAction.DepartmentUpdated,
                    AuditAction.DepartmentDeactivated, AuditAction.DepartmentReactivated },
            actions.Select(a => a.Action));
        Assert.All(actions, a => Assert.Equal(_factory.AdminUserId, a.ActorUserId));
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

    [Fact]
    public async Task Startup_Seeding_Leaves_Custom_Roles_And_Admin_As_They_Are()
    {
        // The removal sync is for system roles only: a custom role is the customer's own decision,
        // and the Admin's set must come through an upgrade whole.
        var (_, _, customRoleId) = await CustomRoleHolderAsync(Roles.Recruiter, RequisitionsRead, UsersRead);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var adminRole = await db.Roles.IgnoreQueryFilters().SingleAsync(r => r.IsSystemRole && r.Code == "Admin");
        var adminBefore = await db.RolePermissions.IgnoreQueryFilters().CountAsync(rp => rp.RoleId == adminRole.Id);

        await DbInitializer.SeedPermissionsAndRolesAsync(db);

        var customCodes = await db.RolePermissions.IgnoreQueryFilters()
            .Where(rp => rp.RoleId == customRoleId)
            .Join(db.Permissions.IgnoreQueryFilters(), rp => rp.PermissionId, p => p.Id, (_, p) => p.Code)
            .ToListAsync();
        Assert.Contains(UsersRead, customCodes);
        Assert.Contains(RequisitionsRead, customCodes);
        Assert.Equal(adminBefore,
            await db.RolePermissions.IgnoreQueryFilters().CountAsync(rp => rp.RoleId == adminRole.Id));
    }
}
