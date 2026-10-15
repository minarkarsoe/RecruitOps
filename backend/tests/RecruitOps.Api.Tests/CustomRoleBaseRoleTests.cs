using System.Net;
using System.Net.Http.Json;
using RecruitOps.Api.Auth;
using RecruitOps.Application.DTOs;
using Xunit;

namespace RecruitOps.Api.Tests;

/// <summary>ADR-0021 option A: a custom role's holders carry the base role it was built with.
/// <para>Until 2026-10-15 every custom-role holder carried <c>Recruiter</c>, so a role built only
/// to approve requisitions reached the whole pipeline — every candidate, company-wide — through
/// the role policies, whatever its matrix said. The enum is now the Admin's explicit choice, shown
/// in the Role Builder, and it follows the role when the role changes.</para></summary>
public class CustomRoleBaseRoleTests : IClassFixture<CustomWebAppFactory>
{
    private const string Password = "P@ssw0rd-Test-123";
    private const string RequisitionsRead = "permission:requisitions:requisitions:read";

    private readonly CustomWebAppFactory _factory;
    private readonly Module3Scenario _scenario;

    public CustomRoleBaseRoleTests(CustomWebAppFactory factory)
    {
        _factory = factory;
        _scenario = new Module3Scenario(factory);
    }

    private HttpClient Admin() => _scenario.Client(Roles.Admin, _factory.AdminUserId);

    private Task<HttpResponseMessage> CreateRole(string? baseRole, params string[] permissions) =>
        Admin().PostAsJsonAsync("/api/roles",
            new CreateRoleRequest($"Custom {Guid.NewGuid():N}", null, "test", permissions, baseRole));

    private async Task<RoleDetailDto> RoleAsync(string baseRole, params string[] permissions)
    {
        var res = await CreateRole(baseRole, permissions);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (await res.Content.ReadFromJsonAsync<RoleDetailDto>())!;
    }

    private async Task<(UserDetailDto User, string Email)> HolderAsync(RoleDetailDto role)
    {
        var email = $"holder.{Guid.NewGuid():N}@alpha.test";
        var res = await Admin().PostAsJsonAsync("/api/users",
            new CreateUserRequest(email, "Role Holder", Password, role.Id));
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return ((await res.Content.ReadFromJsonAsync<UserDetailDto>())!, email);
    }

    private async Task<string> RoleOf(Guid userId) =>
        (await Admin().GetFromJsonAsync<UserDetailDto>($"/api/users/{userId}"))!.Role;

    private Task<HttpResponseMessage> UpdateRole(RoleDetailDto role, bool isActive, string? baseRole) =>
        Admin().PutAsJsonAsync($"/api/roles/{role.Id}",
            new UpdateRoleRequest(role.Name, role.Description, isActive, role.AssignedPermissionCodes, baseRole));

    // ---------------------------------------------------------------- building a role

    [Theory]
    [InlineData(null)]          // a base role is a decision, never a default
    [InlineData("")]
    [InlineData("Admin")]       // admin reach is the Admin system role, assigned openly
    [InlineData("SuperAdmin")]
    [InlineData("superadmin")]
    [InlineData("0")]           // numeric values are refused, as on user assignment
    [InlineData("3")]
    [InlineData("Admin,Recruiter")]
    [InlineData("Bogus")]
    public async Task A_Custom_Role_Needs_A_Base_Role_Below_Admin(string? baseRole)
    {
        var res = await CreateRole(baseRole, RequisitionsRead);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task The_Role_Builder_Reports_Each_Roles_Base_Role()
    {
        var role = await RoleAsync("Approver", RequisitionsRead);
        Assert.Equal("Approver", role.BaseRole);

        var roles = await Admin().GetFromJsonAsync<List<RoleListItemDto>>("/api/roles");
        Assert.Equal("Approver", roles!.Single(r => r.Id == role.Id).BaseRole);
        Assert.All(roles!.Where(r => r.IsSystemRole), r => Assert.Null(r.BaseRole));
    }

    // ---------------------------------------------------------------- what holders carry

    [Theory]
    [InlineData("HrDirector")]
    [InlineData("Recruiter")]
    [InlineData("HiringManager")]
    [InlineData("Approver")]
    [InlineData("Interviewer")]
    public async Task A_Holder_Carries_The_Roles_Base_Role(string baseRole)
    {
        var (holder, _) = await HolderAsync(await RoleAsync(baseRole, RequisitionsRead));
        Assert.Equal(baseRole, holder.Role);
    }

    [Fact]
    public async Task The_Base_Role_Is_What_Signing_In_Puts_In_The_Token()
    {
        var (holder, email) = await HolderAsync(await RoleAsync("HiringManager", RequisitionsRead));

        var login = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = email, Password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Equal("HiringManager", (await login.Content.ReadFromJsonAsync<LoginResponse>())!.Role);
        Assert.Equal("HiringManager", await RoleOf(holder.Id));
    }

    [Fact]
    public async Task Changing_A_Roles_Base_Role_Moves_Its_Holders()
    {
        var role = await RoleAsync("Recruiter", RequisitionsRead);
        var (holder, _) = await HolderAsync(role);

        Assert.Equal(HttpStatusCode.OK, (await UpdateRole(role, isActive: true, "Approver")).StatusCode);

        Assert.Equal("Approver", await RoleOf(holder.Id));
    }

    [Fact]
    public async Task Leaving_The_Base_Role_Out_Of_An_Edit_Keeps_It()
    {
        var role = await RoleAsync("HiringManager", RequisitionsRead);
        var (holder, _) = await HolderAsync(role);

        var res = await UpdateRole(role, isActive: true, baseRole: null);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        Assert.Equal("HiringManager", (await res.Content.ReadFromJsonAsync<RoleDetailDto>())!.BaseRole);
        Assert.Equal("HiringManager", await RoleOf(holder.Id));
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("SuperAdmin")]
    [InlineData("0")]
    public async Task An_Edit_Cannot_Raise_A_Base_Role_To_Admin(string baseRole)
    {
        var role = await RoleAsync("Recruiter", RequisitionsRead);
        Assert.Equal(HttpStatusCode.BadRequest, (await UpdateRole(role, isActive: true, baseRole)).StatusCode);
    }

    [Fact]
    public async Task Deactivating_A_Role_Leaves_Its_Holders_No_Reach_And_Reactivating_Restores_It()
    {
        var role = await RoleAsync("Recruiter", RequisitionsRead);
        var (holder, _) = await HolderAsync(role);

        Assert.Equal(HttpStatusCode.OK, (await UpdateRole(role, isActive: false, null)).StatusCode);
        // Interviewer is the enum no role policy admits: the pipeline, candidates and analytics
        // close along with the matrix, rather than staying open on the old base role.
        Assert.Equal("Interviewer", await RoleOf(holder.Id));

        Assert.Equal(HttpStatusCode.OK, (await UpdateRole(role, isActive: true, null)).StatusCode);
        Assert.Equal("Recruiter", await RoleOf(holder.Id));
    }

    [Fact]
    public async Task System_Roles_Still_Assign_Their_Own_Enum()
    {
        var res = await Admin().PostAsJsonAsync("/api/users",
            new CreateUserRequest($"sys.{Guid.NewGuid():N}@alpha.test", "System Holder", Password, null, "Approver"));
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        Assert.Equal("Approver", (await res.Content.ReadFromJsonAsync<UserDetailDto>())!.Role);
    }
}
