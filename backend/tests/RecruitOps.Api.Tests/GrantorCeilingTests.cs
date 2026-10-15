using System.Net;
using System.Net.Http.Json;
using RecruitOps.Api.Auth;
using RecruitOps.Application.DTOs;
using Xunit;

namespace RecruitOps.Api.Tests;

/// <summary>Nobody grants what they do not hold (ADR-0021 option A, decision 4).
/// <para>Before 2026-10-15 nothing checked the grantor: anyone holding <c>users:create</c> could
/// create an Admin, and anyone holding <c>roles:create</c> could write a role with every
/// permission there is. By default only Admins hold those, so the hole opened the moment an Admin
/// delegated — which is what the Role Builder is for. Here that delegate is an "HR Lead": a
/// Recruiter-based custom role that may manage users and roles, and read requisitions.</para>
/// <para>Each refusal is paired with the legitimate action next to it: a ceiling that refuses
/// everything would pass every refusal test and make delegation useless.</para></summary>
public class GrantorCeilingTests : IClassFixture<CustomWebAppFactory>
{
    private const string Password = "P@ssw0rd-Test-123";

    private const string RequisitionsRead = "permission:requisitions:requisitions:read";
    private const string RequisitionsApprove = "permission:requisitions:requisitions:approve";
    private const string RequisitionsDelete = "permission:requisitions:requisitions:delete";
    private const string SystemManage = "permission:system:system:manage";

    private static readonly string[] HrLeadPermissions =
    {
        "permission:users:users:read", "permission:users:users:create", "permission:users:users:update",
        "permission:roles:roles:read", "permission:roles:roles:create", "permission:roles:roles:update",
        "permission:roles:roles:delete", RequisitionsRead,
    };

    private readonly CustomWebAppFactory _factory;
    private readonly Module3Scenario _scenario;

    public GrantorCeilingTests(CustomWebAppFactory factory)
    {
        _factory = factory;
        _scenario = new Module3Scenario(factory);
    }

    private HttpClient Admin() => _scenario.Client(Roles.Admin, _factory.AdminUserId);

    private static string Unique(string prefix) => $"{prefix} {Guid.NewGuid():N}";

    private static Task<HttpResponseMessage> CreateRole(HttpClient as_, string baseRole, params string[] permissions) =>
        as_.PostAsJsonAsync("/api/roles", new CreateRoleRequest(Unique("Role"), null, "test", permissions, baseRole));

    private static async Task<RoleDetailDto> Created(Task<HttpResponseMessage> request)
    {
        var res = await request;
        Assert.True(res.IsSuccessStatusCode, $"{(int)res.StatusCode}: {await res.Content.ReadAsStringAsync()}");
        return (await res.Content.ReadFromJsonAsync<RoleDetailDto>())!;
    }

    private static Task<HttpResponseMessage> CreateUser(HttpClient as_, Guid? roleId = null, string? role = null) =>
        as_.PostAsJsonAsync("/api/users",
            new CreateUserRequest($"user.{Guid.NewGuid():N}@alpha.test", "Someone", Password, roleId, role));

    private static async Task<UserDetailDto> CreatedUser(Task<HttpResponseMessage> request)
    {
        var res = await request;
        Assert.True(res.IsSuccessStatusCode, $"{(int)res.StatusCode}: {await res.Content.ReadAsStringAsync()}");
        return (await res.Content.ReadFromJsonAsync<UserDetailDto>())!;
    }

    /// <summary>A signed-in HR Lead: Recruiter base, may manage users and roles.</summary>
    private async Task<HttpClient> HrLeadAsync()
    {
        var role = await Created(CreateRole(Admin(), "Recruiter", HrLeadPermissions));
        var lead = await CreatedUser(CreateUser(Admin(), role.Id));
        return _scenario.Client(Roles.Recruiter, lead.Id);
    }

    // ---------------------------------------------------------------- building roles

    [Fact]
    public async Task A_Delegate_Cannot_Grant_A_Permission_They_Do_Not_Hold()
    {
        var lead = await HrLeadAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await CreateRole(lead, "Interviewer", RequisitionsApprove)).StatusCode);
    }

    [Fact]
    public async Task A_Delegate_Can_Build_A_Role_From_What_They_Hold()
    {
        var lead = await HrLeadAsync();
        await Created(CreateRole(lead, "Interviewer", RequisitionsRead));
        await Created(CreateRole(lead, "HiringManager", RequisitionsRead));
        await Created(CreateRole(lead, "Recruiter"));
    }

    [Theory]
    [InlineData("HrDirector")] // above Recruiter: company-wide requisition authority
    public async Task A_Delegate_Cannot_Use_A_Base_Role_Above_Their_Own(string baseRole)
    {
        var lead = await HrLeadAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await CreateRole(lead, baseRole, RequisitionsRead)).StatusCode);
    }

    [Fact]
    public async Task An_Admin_Cannot_Grant_System_Manage_Which_Only_SuperAdmin_Holds()
    {
        Assert.Equal(HttpStatusCode.Forbidden, (await CreateRole(Admin(), "Interviewer", SystemManage)).StatusCode);
    }

    [Fact]
    public async Task An_Admin_Can_Use_Any_Base_Role()
    {
        foreach (var baseRole in new[] { "HrDirector", "Recruiter", "HiringManager", "Approver", "Interviewer" })
            await Created(CreateRole(Admin(), baseRole, RequisitionsApprove));
    }

    // ---------------------------------------------------------------- editing roles

    [Fact]
    public async Task A_Delegate_May_Edit_Around_A_Permission_They_Lack_But_Not_Add_One()
    {
        var role = await Created(CreateRole(Admin(), "Interviewer", RequisitionsApprove));
        var lead = await HrLeadAsync();

        // Renaming resubmits RequisitionsApprove, which the lead lacks: not an addition, allowed.
        var rename = await lead.PutAsJsonAsync($"/api/roles/{role.Id}",
            new UpdateRoleRequest("Renamed " + role.Name, null, true, new[] { RequisitionsApprove }));
        Assert.Equal(HttpStatusCode.OK, rename.StatusCode);

        var add = await lead.PutAsJsonAsync($"/api/roles/{role.Id}",
            new UpdateRoleRequest("Renamed " + role.Name, null, true, new[] { RequisitionsApprove, RequisitionsDelete }));
        Assert.Equal(HttpStatusCode.Forbidden, add.StatusCode);
    }

    [Fact]
    public async Task A_Delegate_Cannot_Raise_A_Roles_Base_Above_Their_Own()
    {
        var role = await Created(CreateRole(Admin(), "Interviewer", RequisitionsRead));
        var lead = await HrLeadAsync();

        var res = await lead.PutAsJsonAsync($"/api/roles/{role.Id}",
            new UpdateRoleRequest(role.Name, null, true, new[] { RequisitionsRead }, "HrDirector"));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task A_Delegate_Cannot_Edit_A_Role_Above_Them()
    {
        // Removing permissions or deactivating changes what HR Directors-in-all-but-name can do.
        var role = await Created(CreateRole(Admin(), "HrDirector", RequisitionsRead));
        var lead = await HrLeadAsync();

        var res = await lead.PutAsJsonAsync($"/api/roles/{role.Id}",
            new UpdateRoleRequest(role.Name, null, false, Array.Empty<string>()));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Reactivating_A_Role_Is_A_Grant()
    {
        var role = await Created(CreateRole(Admin(), "Interviewer", RequisitionsApprove));
        (await Admin().PutAsJsonAsync($"/api/roles/{role.Id}",
            new UpdateRoleRequest(role.Name, null, false, new[] { RequisitionsApprove }))).EnsureSuccessStatusCode();
        var lead = await HrLeadAsync();

        var res = await lead.PutAsJsonAsync($"/api/roles/{role.Id}",
            new UpdateRoleRequest(role.Name, null, true, new[] { RequisitionsApprove }));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // ---------------------------------------------------------------- assigning roles

    [Fact]
    public async Task A_Delegate_Cannot_Create_An_Admin()
    {
        var lead = await HrLeadAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await CreateUser(lead, role: "Admin")).StatusCode);
    }

    [Fact]
    public async Task A_Delegate_Cannot_Assign_A_System_Role_That_Holds_More_Than_They_Do()
    {
        // The Recruiter system role's matrix includes far more than the lead's eight permissions.
        var lead = await HrLeadAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await CreateUser(lead, role: "Recruiter")).StatusCode);
    }

    [Fact]
    public async Task A_Delegate_Can_Assign_A_Role_Within_Their_Own()
    {
        var role = await Created(CreateRole(Admin(), "Interviewer", RequisitionsRead));
        var lead = await HrLeadAsync();

        var user = await CreatedUser(CreateUser(lead, role.Id));
        Assert.Equal("Interviewer", user.Role);
    }

    [Fact]
    public async Task A_Delegate_Cannot_Assign_A_Custom_Role_Holding_More_Than_They_Do()
    {
        var role = await Created(CreateRole(Admin(), "Interviewer", RequisitionsApprove));
        var lead = await HrLeadAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await CreateUser(lead, role.Id)).StatusCode);
    }

    [Fact]
    public async Task A_Delegate_Cannot_Change_The_Role_Of_Someone_Above_Them()
    {
        var director = await CreatedUser(CreateUser(Admin(), role: "HrDirector"));
        var mild = await Created(CreateRole(Admin(), "Interviewer", RequisitionsRead));
        var lead = await HrLeadAsync();

        // Demoting is not a grant, but it is managing someone the lead does not outrank.
        var res = await lead.PutAsJsonAsync($"/api/users/{director.Id}", new UpdateUserRequest("Someone", mild.Id));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task A_Delegate_Cannot_Reactivate_Someone_Above_Them()
    {
        var admin = await CreatedUser(CreateUser(Admin(), role: "Admin"));
        (await Admin().PutAsync($"/api/users/{admin.Id}/deactivate", null)).EnsureSuccessStatusCode();
        var lead = await HrLeadAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await lead.PutAsync($"/api/users/{admin.Id}/reactivate", null)).StatusCode);
    }

    [Fact]
    public async Task An_Admin_Still_Assigns_Every_System_Role_But_SuperAdmin()
    {
        foreach (var role in new[] { "Admin", "HrDirector", "Recruiter", "HiringManager", "Approver", "Interviewer" })
            Assert.Equal(role, (await CreatedUser(CreateUser(Admin(), role: role))).Role);
    }
}
