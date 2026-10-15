using RecruitOps.Domain;
using RecruitOps.Domain.Entities;
using RecruitOps.Domain.Enums;
using Xunit;

namespace RecruitOps.Domain.Tests;

/// <summary>ADR-0021 option A: a custom role carries a base role, and nobody grants reach they do
/// not have. The API-level behaviour is in <c>CustomRoleBaseRoleTests</c> and
/// <c>GrantorCeilingTests</c>; <c>RoleScopeCoversTests</c> pins the order against the live role
/// policies.</summary>
public class CustomRoleBaseRoleDomainTests
{
    [Fact]
    public void Base_Roles_Are_Every_System_Role_Below_Admin()
    {
        Assert.Equal(
            new[] { UserRole.HrDirector, UserRole.Recruiter, UserRole.HiringManager, UserRole.Approver, UserRole.Interviewer },
            RoleScope.AssignableBaseRoles);
    }

    // ------------------------------------------------------------- the reach order

    [Theory]
    [InlineData(UserRole.Admin, UserRole.HrDirector)]
    [InlineData(UserRole.Admin, UserRole.Interviewer)]
    [InlineData(UserRole.HrDirector, UserRole.Recruiter)]
    [InlineData(UserRole.HrDirector, UserRole.HiringManager)]
    [InlineData(UserRole.Recruiter, UserRole.HiringManager)]
    [InlineData(UserRole.Recruiter, UserRole.Approver)]
    [InlineData(UserRole.HiringManager, UserRole.Interviewer)]
    [InlineData(UserRole.Approver, UserRole.Interviewer)]
    [InlineData(UserRole.SuperAdmin, UserRole.Admin)]
    public void A_Role_Covers_Those_Below_It(UserRole actor, UserRole target) =>
        Assert.True(RoleScope.Covers(actor, target));

    [Theory]
    [InlineData(UserRole.HrDirector, UserRole.Admin)]
    [InlineData(UserRole.Recruiter, UserRole.HrDirector)]
    [InlineData(UserRole.HiringManager, UserRole.Recruiter)]
    [InlineData(UserRole.HiringManager, UserRole.Approver)]   // siblings: neither covers the other
    [InlineData(UserRole.Approver, UserRole.HiringManager)]
    [InlineData(UserRole.Interviewer, UserRole.Approver)]
    [InlineData(UserRole.Admin, UserRole.SuperAdmin)]         // nothing in a tenant reaches SuperAdmin
    public void A_Role_Does_Not_Cover_Those_Above_Or_Beside_It(UserRole actor, UserRole target) =>
        Assert.False(RoleScope.Covers(actor, target));

    [Fact]
    public void Every_Role_Covers_Itself()
    {
        foreach (var role in Enum.GetValues<UserRole>())
            Assert.True(RoleScope.Covers(role, role), role.ToString());
    }

    // ------------------------------------------------------------- the holder's enum

    [Theory]
    [InlineData(UserRole.HiringManager)]
    [InlineData(UserRole.Approver)]
    [InlineData(UserRole.HrDirector)]
    public void An_Active_Custom_Role_Gives_Its_Holders_Its_Base_Role(UserRole baseRole)
    {
        var role = new Role { IsSystemRole = false, IsActive = true, BaseRole = baseRole };
        Assert.Equal(baseRole, role.HolderRole());
    }

    [Fact]
    public void An_Inactive_Custom_Role_Gives_Its_Holders_No_Reach()
    {
        var role = new Role { IsSystemRole = false, IsActive = false, BaseRole = UserRole.HrDirector };
        Assert.Equal(UserRole.Interviewer, role.HolderRole());
    }

    [Fact]
    public void A_Custom_Role_Without_A_Base_Role_Gives_No_Reach_Rather_Than_Guessing()
    {
        // Every custom role has one after the migration; a row without is a defect, and a defect
        // should not read like Recruiter — that guess is what ADR-0021 removed.
        var role = new Role { IsSystemRole = false, IsActive = true, BaseRole = null };
        Assert.Equal(UserRole.Interviewer, role.HolderRole());
    }
}
