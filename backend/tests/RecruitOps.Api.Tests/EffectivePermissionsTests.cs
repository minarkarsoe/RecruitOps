using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RecruitOps.Api.Auth;
using RecruitOps.Api.Authorization;
using RecruitOps.Application.DTOs;
using RecruitOps.Application.Interfaces;
using RecruitOps.Domain.Entities;
using RecruitOps.Domain.Enums;
using RecruitOps.Infrastructure.Persistence;
using Xunit;

namespace RecruitOps.Api.Tests;

/// <summary>The permission set sent at login is the set the API enforces.
/// <para>Until 2026-10-15 the login response carried no permissions at all, and the client's
/// <c>hasPermission</c> treated a missing list as "allow everything" — so every user saw every
/// navigation link and action button, and the permission-aware UI had never worked against the
/// real API. Its tests passed because they built sessions that already held a permission list.</para>
/// <para>The handler is not refactored to share code with the login path; instead this suite
/// checks the two agree on every permission, for every role, including the one case where the
/// handler's seed-role fallback changes the answer (a custom role narrower than its system role).</para></summary>
public class EffectivePermissionsTests : IClassFixture<CustomWebAppFactory>
{
    private const string Password = "P@ssw0rd-Test-123";

    private readonly CustomWebAppFactory _factory;
    private readonly Module3Scenario _scenario;

    public EffectivePermissionsTests(CustomWebAppFactory factory)
    {
        _factory = factory;
        _scenario = new Module3Scenario(factory);
    }

    private HttpClient Admin() => _scenario.Client(Roles.Admin, _factory.AdminUserId);

    private async Task<(Guid Id, string Email)> CreateUserAsync(string role)
    {
        var email = $"perm.{Guid.NewGuid():N}@alpha.test";
        var res = await Admin().PostAsJsonAsync("/api/users",
            new CreateUserRequest(email, "Permission Subject", Password, null, role));
        res.EnsureSuccessStatusCode();
        return ((await res.Content.ReadFromJsonAsync<UserDetailDto>())!.Id, email);
    }

    /// <summary>Users the API cannot create (ADR-0021) are written directly.</summary>
    private async Task<Guid> InsertUserAsync(UserRole role, Guid? roleId = null, bool isSuperAdmin = false)
    {
        var id = Guid.NewGuid();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Users.Add(new User
        {
            Id = id, TenantId = _factory.TenantA, Email = $"direct.{id:N}@alpha.test",
            DisplayName = "Direct", Role = role, RoleId = roleId, IsSuperAdmin = isSuperAdmin,
            IsActive = true, PasswordHash = "not-used",
        });
        await db.SaveChangesAsync();
        return id;
    }

    /// <summary>Asks the real handler about every canonical permission and compares each answer
    /// with membership in the effective set.</summary>
    private async Task AssertAgreementAsync(Guid userId, string roleClaim, bool superAdminClaim = false)
    {
        using var scope = _factory.Services.CreateScope();
        var evaluator = scope.ServiceProvider.GetRequiredService<IPermissionEvaluator>();
        var handler = new PermissionAuthorizationHandler(evaluator, NullLogger<PermissionAuthorizationHandler>.Instance);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(AppClaims.TenantId, _factory.TenantA.ToString()),
            new(ClaimTypes.Role, roleClaim),
            new(AppClaims.IsSuperAdmin, superAdminClaim ? "true" : "false"),
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));

        var effective = await evaluator.GetEffectivePermissionsAsync(userId, _factory.TenantA, new[] { roleClaim });

        var disagreements = new List<string>();
        foreach (var code in RbacSeedData.GetCanonicalPermissions().Select(p => p.Code))
        {
            var requirement = new PermissionRequirement(code);
            var context = new AuthorizationHandlerContext(new[] { requirement }, principal, null);
            await handler.HandleAsync(context);

            if (context.HasSucceeded != effective.Contains(code))
                disagreements.Add($"{code}: handler={context.HasSucceeded}, login set={effective.Contains(code)}");
        }

        Assert.Empty(disagreements);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("HrDirector")]
    [InlineData("Recruiter")]
    [InlineData("HiringManager")]
    [InlineData("Approver")]
    [InlineData("Interviewer")]
    public async Task The_Login_Set_Agrees_With_The_Handler_For_Each_System_Role(string role)
    {
        var (id, _) = await CreateUserAsync(role);
        await AssertAgreementAsync(id, role);
    }

    [Fact]
    public async Task The_Login_Set_Agrees_With_The_Handler_For_SuperAdmin()
    {
        var id = await InsertUserAsync(UserRole.SuperAdmin, isSuperAdmin: true);
        await AssertAgreementAsync(id, "SuperAdmin", superAdminClaim: true);
    }

    /// <summary>The handler unions the system role's seed permissions on top of the custom
    /// matrix. A login set built from the custom role alone would hide links the API allows —
    /// this is the case that pins the union.</summary>
    [Fact]
    public async Task The_Login_Set_Agrees_With_The_Handler_For_A_Custom_Role_Holder()
    {
        var res = await Admin().PostAsJsonAsync("/api/roles", new CreateRoleRequest(
            $"Narrow {Guid.NewGuid():N}", null, "test", new[] { "permission:roles:roles:read" }));
        res.EnsureSuccessStatusCode();
        var custom = (await res.Content.ReadFromJsonAsync<RoleDetailDto>())!;

        var id = await InsertUserAsync(UserRole.HiringManager, custom.Id);
        await AssertAgreementAsync(id, "HiringManager");
    }

    [Fact]
    public async Task Login_Returns_The_Callers_Effective_Permissions()
    {
        var (_, email) = await CreateUserAsync("HiringManager");

        var res = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = email, Password = Password });
        res.EnsureSuccessStatusCode();
        var body = (await res.Content.ReadFromJsonAsync<LoginResponse>())!;

        Assert.False(body.IsSuperAdmin);
        Assert.Contains("permission:requisitions:requisitions:create", body.Permissions);
        Assert.DoesNotContain("permission:users:users:read", body.Permissions);
        Assert.DoesNotContain("permission:roles:roles:read", body.Permissions);
        Assert.Equal(body.Permissions.Order(StringComparer.Ordinal), body.Permissions);
    }

    [Fact]
    public async Task Login_Gives_An_Admin_The_Directory_And_Role_Builder_Permissions()
    {
        var res = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = CustomWebAppFactory.AdminEmail, Password = CustomWebAppFactory.AdminPassword });
        res.EnsureSuccessStatusCode();
        var body = (await res.Content.ReadFromJsonAsync<LoginResponse>())!;

        Assert.Contains("permission:users:users:read", body.Permissions);
        Assert.Contains("permission:roles:roles:read", body.Permissions);
        Assert.DoesNotContain("permission:system:system:manage", body.Permissions);
    }
}
