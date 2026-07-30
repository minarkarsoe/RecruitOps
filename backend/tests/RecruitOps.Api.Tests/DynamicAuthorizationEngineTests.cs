using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging.Abstractions;
using RecruitOps.Api.Auth;
using RecruitOps.Api.Authorization;
using RecruitOps.Application.Interfaces;
using RecruitOps.Domain.Enums;
using Xunit;

namespace RecruitOps.Api.Tests;

public class DynamicAuthorizationEngineTests
{
    [Fact]
    public void HasPermissionAttribute_Normalizes_Shorthand_Permission_Code()
    {
        var attr = new HasPermissionAttribute("requisitions:requisitions:approve");
        Assert.Equal("permission:requisitions:requisitions:approve", attr.Permission);
        Assert.Equal("Permission:permission:requisitions:requisitions:approve", attr.Policy);
    }

    [Fact]
    public void HasPermissionAttribute_Retains_Full_Permission_Code()
    {
        var attr = new HasPermissionAttribute("permission:roles:roles:read");
        Assert.Equal("permission:roles:roles:read", attr.Permission);
        Assert.Equal("Permission:permission:roles:roles:read", attr.Policy);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void HasPermissionAttribute_Throws_On_Null_Or_Whitespace(string? input)
    {
        Assert.Throws<ArgumentNullException>(() => new HasPermissionAttribute(input!));
    }

    [Fact]
    public void PermissionRequirement_Stores_Code()
    {
        var req = new PermissionRequirement("permission:users:users:create");
        Assert.Equal("permission:users:users:create", req.PermissionCode);
    }

    [Fact]
    public async Task SuperAdmin_Claim_Triggers_Instant_Bypass_In_AuthorizationHandler()
    {
        var evaluator = new DummyPermissionEvaluator(hasPermission: false);
        var handler = new PermissionAuthorizationHandler(evaluator, NullLogger<PermissionAuthorizationHandler>.Instance);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(AppClaims.TenantId, Guid.NewGuid().ToString()),
            new Claim(AppClaims.IsSuperAdmin, "true")
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);

        var requirement = new PermissionRequirement("permission:any:restricted:action");
        var context = new AuthorizationHandlerContext(new[] { requirement }, principal, null);

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task Admin_Role_Claim_Evaluates_System_Role_Permissions_In_AuthorizationHandler()
    {
        var evaluator = new DummyPermissionEvaluator(hasPermission: false);
        var handler = new PermissionAuthorizationHandler(evaluator, NullLogger<PermissionAuthorizationHandler>.Instance);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(AppClaims.TenantId, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, "Admin")
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);

        var requirement = new PermissionRequirement("permission:users:users:read");
        var context = new AuthorizationHandlerContext(new[] { requirement }, principal, null);

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task StandardUser_With_Permission_Succeeds_In_AuthorizationHandler()
    {
        var evaluator = new DummyPermissionEvaluator(hasPermission: true);
        var handler = new PermissionAuthorizationHandler(evaluator, NullLogger<PermissionAuthorizationHandler>.Instance);

        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(AppClaims.TenantId, tenantId.ToString()),
            new Claim(ClaimTypes.Role, "Recruiter")
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);

        var requirement = new PermissionRequirement("permission:requisitions:requisitions:read");
        var context = new AuthorizationHandlerContext(new[] { requirement }, principal, null);

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task StandardUser_Without_Permission_Fails_In_AuthorizationHandler()
    {
        var evaluator = new DummyPermissionEvaluator(hasPermission: false);
        var handler = new PermissionAuthorizationHandler(evaluator, NullLogger<PermissionAuthorizationHandler>.Instance);

        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(AppClaims.TenantId, tenantId.ToString()),
            new Claim(ClaimTypes.Role, "Recruiter")
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);

        var requirement = new PermissionRequirement("permission:roles:roles:delete");
        var context = new AuthorizationHandlerContext(new[] { requirement }, principal, null);

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    private class DummyPermissionEvaluator : IPermissionEvaluator
    {
        private readonly bool _hasPermission;
        public DummyPermissionEvaluator(bool hasPermission) => _hasPermission = hasPermission;

        public Task<bool> HasPermissionAsync(Guid userId, Guid tenantId, string permissionCode, CancellationToken ct = default)
            => Task.FromResult(_hasPermission);

        public Task<IReadOnlySet<string>> GetUserPermissionsAsync(Guid userId, Guid tenantId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());

        public void InvalidateUserPermissionsCache(Guid userId, Guid tenantId) { }
        public void InvalidateRolePermissionsCache(Guid roleId) { }
    }
}
