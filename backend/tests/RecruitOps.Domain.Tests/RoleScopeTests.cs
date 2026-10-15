using RecruitOps.Domain;
using RecruitOps.Domain.Enums;
using Xunit;

namespace RecruitOps.Domain.Tests;

/// <summary><c>RoleScope.Parse</c> turns the role claim into the enum that drives department scoping
/// (ADR-0003) and candidate-data exclusion (ADR-0018). It used <c>Enum.TryParse</c>, which also
/// accepts numeric strings — including values no member has — and comma-joined names. An undefined
/// value is neither department-scoped nor excluded: the permissive answer, where this method promises
/// the restrictive one (security review, 2026-10-15). Not reachable through a token the API issued
/// today; pinned so it never becomes reachable.</summary>
public class RoleScopeTests
{
    [Theory]
    [InlineData("Admin", UserRole.Admin)]
    [InlineData("Recruiter", UserRole.Recruiter)]
    [InlineData("HiringManager", UserRole.HiringManager)]
    [InlineData("Approver", UserRole.Approver)]
    public void Parses_Exact_Role_Names(string claim, UserRole expected) =>
        Assert.Equal(expected, RoleScope.Parse(claim));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0")]                 // SuperAdmin's value
    [InlineData("4")]                 // HiringManager's value
    [InlineData("99")]                // no member at all
    [InlineData("-1")]
    [InlineData("Admin,Recruiter")]
    [InlineData("recruiter")]         // case-sensitive, as before
    [InlineData(" Recruiter")]
    public void Anything_But_An_Exact_Name_Is_Unrecognised(string? claim) =>
        Assert.Null(RoleScope.Parse(claim));
}
