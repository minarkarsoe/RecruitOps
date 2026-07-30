using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using RecruitOps.Api.Auth;
using RecruitOps.Application.DTOs;
using Xunit;

namespace RecruitOps.Api.Tests;

public class EmpiricalUserManagementChallengeTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;
    private readonly Module3Scenario _scenario;

    public EmpiricalUserManagementChallengeTests(CustomWebAppFactory factory)
    {
        _factory = factory;
        _scenario = new Module3Scenario(factory);
    }

    private HttpClient AdminClient(Guid? userId = null, Guid? tenantId = null)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Tenant", (tenantId ?? _factory.TenantA).ToString());
        client.DefaultRequestHeaders.Add("X-Test-Roles", Roles.Admin);
        client.DefaultRequestHeaders.Add("X-Test-UserId", (userId ?? _factory.AdminUserId).ToString());
        return client;
    }

    // =========================================================================
    // Scope 1: User Deactivation Guards
    // =========================================================================

    [Fact]
    public async Task Self_Deactivation_Is_Rejected_With_409_Conflict()
    {
        var client = AdminClient(_factory.AdminUserId);

        var response = await client.PutAsync($"/api/users/{_factory.AdminUserId}/deactivate", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("cannot deactivate your own account", problem.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Deactivating_Last_Active_Admin_In_Tenant_Is_Rejected_With_409_Conflict()
    {
        var primaryAdminClient = AdminClient(_factory.AdminUserId);

        // Step 1: Create a secondary admin account
        var secEmail = $"sec.admin.{Guid.NewGuid():N}@alpha.test";
        var createRes = await primaryAdminClient.PostAsJsonAsync("/api/users", new CreateUserRequest(
            Email: secEmail,
            DisplayName: "Secondary Admin",
            Password: "Password123!",
            Role: "Admin"
        ));
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var secAdmin = await createRes.Content.ReadFromJsonAsync<UserDetailDto>();
        Assert.NotNull(secAdmin);

        var secAdminClient = AdminClient(secAdmin.Id);

        // Step 2: Secondary admin deactivates primary admin (allowed because 2 active admins exist)
        var deactPrimaryRes = await secAdminClient.PutAsync($"/api/users/{_factory.AdminUserId}/deactivate", null);
        Assert.Equal(HttpStatusCode.OK, deactPrimaryRes.StatusCode);

        // Step 3: Now secAdmin is the sole active admin in Tenant A. Attempting to deactivate secAdmin using primary admin (if reactivated) or secAdmin (self check happens first)
        // Let's reactivate primary admin so we can attempt deactivating sole active admin from a non-self admin client.
        var reactivatePrimaryRes = await secAdminClient.PutAsync($"/api/users/{_factory.AdminUserId}/reactivate", null);
        Assert.Equal(HttpStatusCode.OK, reactivatePrimaryRes.StatusCode);

        // Deactivate secondary admin -> 2 active admins exist -> OK
        var deactSecRes = await primaryAdminClient.PutAsync($"/api/users/{secAdmin.Id}/deactivate", null);
        Assert.Equal(HttpStatusCode.OK, deactSecRes.StatusCode);

        // Now Primary Admin is sole active admin. Attempting to deactivate Primary Admin via secAdmin client:
        var deactSoleAdminRes = await secAdminClient.PutAsync($"/api/users/{_factory.AdminUserId}/deactivate", null);
        Assert.Equal(HttpStatusCode.Conflict, deactSoleAdminRes.StatusCode);
        var problem = await deactSoleAdminRes.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("last active Administrator", problem.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Deactivating_Already_Inactive_User_Is_Rejected_With_409_Conflict()
    {
        var client = AdminClient();

        var email = $"to.deactivate.{Guid.NewGuid():N}@alpha.test";
        var createRes = await client.PostAsJsonAsync("/api/users", new CreateUserRequest(
            Email: email,
            DisplayName: "Temp User",
            Password: "Password123!",
            Role: "Recruiter"
        ));
        var created = await createRes.Content.ReadFromJsonAsync<UserDetailDto>();

        // Deactivate first time -> OK
        var deact1 = await client.PutAsync($"/api/users/{created!.Id}/deactivate", null);
        Assert.Equal(HttpStatusCode.OK, deact1.StatusCode);

        // Deactivate second time -> Conflict
        var deact2 = await client.PutAsync($"/api/users/{created.Id}/deactivate", null);
        Assert.Equal(HttpStatusCode.Conflict, deact2.StatusCode);
        var problem = await deact2.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Contains("already inactive", problem!.Detail, StringComparison.OrdinalIgnoreCase);
    }

    // =========================================================================
    // Scope 2: Email Uniqueness
    // =========================================================================

    [Fact]
    public async Task Create_User_With_Duplicate_Email_In_Same_Tenant_Rejected_With_409_Conflict()
    {
        var client = AdminClient();

        var request = new CreateUserRequest(
            Email: CustomWebAppFactory.AdminEmail,
            DisplayName: "Duplicate Email User",
            Password: "Password123!",
            Role: "Recruiter"
        );

        var response = await client.PostAsJsonAsync("/api/users", request);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("already exists", problem.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_User_With_Duplicate_Email_Case_Insensitive_Rejected_With_409_Conflict()
    {
        var client = AdminClient();

        var request = new CreateUserRequest(
            Email: CustomWebAppFactory.AdminEmail.ToUpperInvariant(),
            DisplayName: "Uppercase Duplicate Email User",
            Password: "Password123!",
            Role: "Recruiter"
        );

        var response = await client.PostAsJsonAsync("/api/users", request);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("already exists", problem.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_User_With_Duplicate_Email_In_Another_Tenant_Rejected_With_409_Conflict()
    {
        var tenantAClient = AdminClient(tenantId: _factory.TenantA);
        var uniqueEmail = $"cross.tenant.{Guid.NewGuid():N}@alpha.test";

        // Create user in Tenant A
        var createTenantARes = await tenantAClient.PostAsJsonAsync("/api/users", new CreateUserRequest(
            Email: uniqueEmail,
            DisplayName: "Tenant A User",
            Password: "Password123!",
            Role: "Recruiter"
        ));
        Assert.Equal(HttpStatusCode.Created, createTenantARes.StatusCode);

        // Attempt to create user in Tenant B with the same email
        var tenantBClient = AdminClient(tenantId: _factory.TenantB);
        var createTenantBRes = await tenantBClient.PostAsJsonAsync("/api/users", new CreateUserRequest(
            Email: uniqueEmail,
            DisplayName: "Tenant B Duplicate User",
            Password: "Password123!",
            Role: "Recruiter"
        ));

        Assert.Equal(HttpStatusCode.Conflict, createTenantBRes.StatusCode);
        var problem = await createTenantBRes.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("already exists", problem.Detail, StringComparison.OrdinalIgnoreCase);
    }

    // =========================================================================
    // Scope 3: EF Core 10 Query Execution
    // =========================================================================

    [Fact]
    public async Task Get_Users_Executes_Complex_Queries_Without_EFCore_Exceptions()
    {
        var client = AdminClient();

        // Query 1: Search with mixed case and leading/trailing spaces
        var res1 = await client.GetAsync("/api/users?page=1&pageSize=10&search=%20Alpha%20");
        Assert.Equal(HttpStatusCode.OK, res1.StatusCode);
        var paged1 = await res1.Content.ReadFromJsonAsync<PagedResult<UserListItemDto>>();
        Assert.NotNull(paged1);
        Assert.NotEmpty(paged1.Items);

        // Query 2: Active filter only
        var res2 = await client.GetAsync("/api/users?page=1&pageSize=5&isActive=true");
        Assert.Equal(HttpStatusCode.OK, res2.StatusCode);
        var paged2 = await res2.Content.ReadFromJsonAsync<PagedResult<UserListItemDto>>();
        Assert.NotNull(paged2);
        Assert.All(paged2.Items, item => Assert.True(item.IsActive));

        // Query 3: Inactive filter
        var res3 = await client.GetAsync("/api/users?page=1&pageSize=5&isActive=false");
        Assert.Equal(HttpStatusCode.OK, res3.StatusCode);
        var paged3 = await res3.Content.ReadFromJsonAsync<PagedResult<UserListItemDto>>();
        Assert.NotNull(paged3);

        // Query 4: Search for non-existent user
        var res4 = await client.GetAsync("/api/users?page=1&pageSize=10&search=NonExistentUserSearch9999");
        Assert.Equal(HttpStatusCode.OK, res4.StatusCode);
        var paged4 = await res4.Content.ReadFromJsonAsync<PagedResult<UserListItemDto>>();
        Assert.NotNull(paged4);
        Assert.Empty(paged4.Items);
        Assert.Equal(0, paged4.TotalCount);

        // Query 5: Out of bounds pagination
        var res5 = await client.GetAsync("/api/users?page=999&pageSize=20");
        Assert.Equal(HttpStatusCode.OK, res5.StatusCode);
        var paged5 = await res5.Content.ReadFromJsonAsync<PagedResult<UserListItemDto>>();
        Assert.NotNull(paged5);
        Assert.Empty(paged5.Items);
        Assert.Equal(999, paged5.Page);

        // Query 6: Boundary page size (<1 and >100)
        var res6 = await client.GetAsync("/api/users?page=1&pageSize=0");
        Assert.Equal(HttpStatusCode.OK, res6.StatusCode);
        var paged6 = await res6.Content.ReadFromJsonAsync<PagedResult<UserListItemDto>>();
        Assert.Equal(20, paged6!.PageSize);

        var res7 = await client.GetAsync("/api/users?page=1&pageSize=500");
        Assert.Equal(HttpStatusCode.OK, res7.StatusCode);
        var paged7 = await res7.Content.ReadFromJsonAsync<PagedResult<UserListItemDto>>();
        Assert.Equal(100, paged7!.PageSize);
    }
}
