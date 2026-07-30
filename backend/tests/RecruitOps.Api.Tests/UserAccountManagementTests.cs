using System.Net;
using System.Net.Http.Json;
using RecruitOps.Api.Auth;
using RecruitOps.Application.DTOs;
using Xunit;

namespace RecruitOps.Api.Tests;

public class UserAccountManagementTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;
    private readonly Module3Scenario _scenario;

    public UserAccountManagementTests(CustomWebAppFactory factory)
    {
        _factory = factory;
        _scenario = new Module3Scenario(factory);
    }

    private HttpClient AdminClient(Guid? userId = null) =>
        _scenario.Client(Roles.Admin, userId ?? _factory.AdminUserId);

    [Fact]
    public async Task Get_Users_Paged_With_Search_And_Filters()
    {
        var client = AdminClient();
        var response = await client.GetAsync("/api/users?page=1&pageSize=10&search=Manager");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var pagedResult = await response.Content.ReadFromJsonAsync<PagedResult<UserListItemDto>>();

        Assert.NotNull(pagedResult);
        Assert.Equal(1, pagedResult.Page);
        Assert.Equal(10, pagedResult.PageSize);
        Assert.True(pagedResult.TotalCount > 0);
        Assert.All(pagedResult.Items, item =>
            Assert.True(item.DisplayName.Contains("Manager", StringComparison.OrdinalIgnoreCase) ||
                        item.Email.Contains("Manager", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task Get_User_ById_Returns_Details_And_Permissions()
    {
        var client = AdminClient();
        var response = await client.GetAsync($"/api/users/{_factory.AdminUserId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var userDetail = await response.Content.ReadFromJsonAsync<UserDetailDto>();

        Assert.NotNull(userDetail);
        Assert.Equal(_factory.AdminUserId, userDetail.Id);
        Assert.Equal(CustomWebAppFactory.AdminEmail, userDetail.Email);
        Assert.NotEmpty(userDetail.Permissions);
    }

    [Fact]
    public async Task Create_User_Succeeds_For_Valid_Payload()
    {
        var client = AdminClient();
        var email = $"new.user.{Guid.NewGuid():N}@alpha.test";
        var request = new CreateUserRequest(
            Email: email,
            DisplayName: "New Test User",
            Password: "SecurePassword123!",
            Role: "Recruiter"
        );

        var response = await client.PostAsJsonAsync("/api/users", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<UserDetailDto>();
        Assert.NotNull(created);
        Assert.Equal(email, created.Email);
        Assert.Equal("New Test User", created.DisplayName);
        Assert.True(created.IsActive);
    }

    [Fact]
    public async Task Create_User_Fails_When_Email_Already_Exists()
    {
        var client = AdminClient();
        var request = new CreateUserRequest(
            Email: CustomWebAppFactory.AdminEmail,
            DisplayName: "Duplicate Email User",
            Password: "SecurePassword123!",
            Role: "Recruiter"
        );

        var response = await client.PostAsJsonAsync("/api/users", request);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Update_User_Updates_Metadata_And_Role()
    {
        var client = AdminClient();
        var createRequest = new CreateUserRequest(
            Email: $"updatable.{Guid.NewGuid():N}@alpha.test",
            DisplayName: "Before Update Name",
            Password: "SecurePassword123!",
            Role: "Interviewer"
        );

        var createRes = await client.PostAsJsonAsync("/api/users", createRequest);
        var created = await createRes.Content.ReadFromJsonAsync<UserDetailDto>();

        var updateRequest = new UpdateUserRequest(
            DisplayName: "Updated Display Name",
            Role: "HiringManager"
        );

        var updateRes = await client.PutAsJsonAsync($"/api/users/{created!.Id}", updateRequest);
        Assert.Equal(HttpStatusCode.OK, updateRes.StatusCode);

        var updated = await updateRes.Content.ReadFromJsonAsync<UserDetailDto>();
        Assert.Equal("Updated Display Name", updated!.DisplayName);
        Assert.Equal("HiringManager", updated.Role);
    }

    [Fact]
    public async Task Deactivate_And_Reactivate_User_Lifecycle()
    {
        var client = AdminClient();
        var createRequest = new CreateUserRequest(
            Email: $"deactivatable.{Guid.NewGuid():N}@alpha.test",
            DisplayName: "Deactivatable User",
            Password: "SecurePassword123!",
            Role: "Recruiter"
        );

        var createRes = await client.PostAsJsonAsync("/api/users", createRequest);
        var created = await createRes.Content.ReadFromJsonAsync<UserDetailDto>();

        // Deactivate
        var deactivateRes = await client.PutAsync($"/api/users/{created!.Id}/deactivate", null);
        Assert.Equal(HttpStatusCode.OK, deactivateRes.StatusCode);

        var deactivated = await deactivateRes.Content.ReadFromJsonAsync<UserDetailDto>();
        Assert.False(deactivated!.IsActive);

        // Reactivate
        var reactivateRes = await client.PutAsync($"/api/users/{created.Id}/reactivate", null);
        Assert.Equal(HttpStatusCode.OK, reactivateRes.StatusCode);

        var reactivated = await reactivateRes.Content.ReadFromJsonAsync<UserDetailDto>();
        Assert.True(reactivated!.IsActive);
    }

    [Fact]
    public async Task Deactivate_User_Fails_On_Self_Deactivation()
    {
        // Admin calling deactivate on Admin's own ID
        var client = AdminClient(_factory.AdminUserId);

        var response = await client.PutAsync($"/api/users/{_factory.AdminUserId}/deactivate", null);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Deactivate_User_Fails_On_Last_Active_Admin()
    {
        // Create a separate caller admin user to bypass self-deactivation check
        var primaryAdminClient = AdminClient(_factory.AdminUserId);
        var secondaryAdminEmail = $"second.admin.{Guid.NewGuid():N}@alpha.test";

        var createRes = await primaryAdminClient.PostAsJsonAsync("/api/users", new CreateUserRequest(
            Email: secondaryAdminEmail,
            DisplayName: "Secondary Admin",
            Password: "Password123!",
            Role: "Admin"
        ));
        var secondaryAdmin = await createRes.Content.ReadFromJsonAsync<UserDetailDto>();

        var secondaryAdminClient = AdminClient(secondaryAdmin!.Id);

        // Deactivate primary admin using secondary admin client -> succeeds because 2 admins exist
        var deactPrimary = await secondaryAdminClient.PutAsync($"/api/users/{_factory.AdminUserId}/deactivate", null);
        Assert.Equal(HttpStatusCode.OK, deactPrimary.StatusCode);

        // Now secondary admin tries to deactivate self -> fails self check, but if primary admin (now inactive) or third party tries to deactivate secondary admin (last active admin) -> fails last active admin check!
        // To test last active admin check specifically: primary admin (inactive, or reactivated) trying to deactivate secondary admin when secondary admin is sole active admin!
        // Reactivate primary admin first
        await secondaryAdminClient.PutAsync($"/api/users/{_factory.AdminUserId}/reactivate", null);
        // Deactivate secondary admin -> 2 active admins exist -> succeeds
        await primaryAdminClient.PutAsync($"/api/users/{secondaryAdmin.Id}/deactivate", null);

        // Now primary admin is sole active admin in tenant. Attempting to deactivate primary admin using secondary admin client (or any caller):
        var deactSoleAdmin = await secondaryAdminClient.PutAsync($"/api/users/{_factory.AdminUserId}/deactivate", null);
        Assert.Equal(HttpStatusCode.Conflict, deactSoleAdmin.StatusCode);
    }
}
