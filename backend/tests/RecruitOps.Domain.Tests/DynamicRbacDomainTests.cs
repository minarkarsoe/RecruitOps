using RecruitOps.Domain.Entities;
using RecruitOps.Domain.Enums;
using Xunit;

namespace RecruitOps.Domain.Tests;

public class DynamicRbacDomainTests
{
    [Fact]
    public void Role_Entity_Defaults_IsActive_To_True()
    {
        var role = new Role
        {
            Name = "Test Role",
            Code = "TEST_ROLE"
        };

        Assert.True(role.IsActive);
        Assert.False(role.IsSystemRole);
        Assert.False(role.IsSuperAdmin);
        Assert.Empty(role.RolePermissions);
    }

    [Fact]
    public void Permission_Entity_Formatted_Code_Matches_Canonical_Pattern()
    {
        var permission = new Permission
        {
            Module = "requisitions",
            Feature = "requisitions",
            Action = "approve",
            Name = "Approve Requisitions",
            Code = "permission:requisitions:requisitions:approve"
        };

        Assert.Equal("permission:requisitions:requisitions:approve", permission.Code);
    }

    [Fact]
    public void RolePermission_Join_Entity_Associates_Role_And_Permission()
    {
        var roleId = Guid.NewGuid();
        var permissionId = Guid.NewGuid();

        var join = new RolePermission
        {
            RoleId = roleId,
            PermissionId = permissionId
        };

        Assert.Equal(roleId, join.RoleId);
        Assert.Equal(permissionId, join.PermissionId);
    }
}
