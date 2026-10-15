using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using RecruitOps.Api.Auth;
using RecruitOps.Domain;
using RecruitOps.Domain.Enums;
using Xunit;

namespace RecruitOps.Api.Tests;

/// <summary><c>RoleScope.Covers</c> is the grantor ceiling's reach order (ADR-0021 option A). It
/// is written out by hand in the Domain, and the reach it describes lives in <c>Program.cs</c>'s
/// role policies — two copies of one rule, which is this repo's recurring bug. So the order is
/// checked here against the policies the running API actually registers: add a role to a policy
/// without revisiting the order, and this fails.</summary>
public class RoleScopeCoversTests : IClassFixture<CustomWebAppFactory>
{
    private static readonly string[] RolePolicies = { Policies.AdminOnly, Policies.RecruitmentStaff, Policies.InternalUser };

    private readonly CustomWebAppFactory _factory;

    public RoleScopeCoversTests(CustomWebAppFactory factory) => _factory = factory;

    private async Task<Dictionary<UserRole, HashSet<string>>> PoliciesAdmittingAsync()
    {
        var provider = _factory.Services.GetRequiredService<IAuthorizationPolicyProvider>();
        var admits = Enum.GetValues<UserRole>().ToDictionary(r => r, _ => new HashSet<string>());

        foreach (var name in RolePolicies)
        {
            var policy = await provider.GetPolicyAsync(name);
            Assert.NotNull(policy);
            var allowed = policy!.Requirements.OfType<RolesAuthorizationRequirement>().Single().AllowedRoles;
            foreach (var role in allowed)
                admits[Enum.Parse<UserRole>(role)].Add(name);
        }

        return admits;
    }

    [Fact]
    public async Task No_Role_Policy_Admits_Interviewer()
    {
        // A deactivated custom role leaves its holders on Interviewer precisely because of this;
        // if a policy ever admits Interviewer, that stops meaning "no reach".
        var admits = await PoliciesAdmittingAsync();
        Assert.Empty(admits[UserRole.Interviewer]);
    }

    [Fact]
    public async Task Whatever_A_Role_Covers_Reaches_No_Further_Than_It_Does()
    {
        var admits = await PoliciesAdmittingAsync();
        var roles = Enum.GetValues<UserRole>().Where(r => r != UserRole.SuperAdmin).ToArray();

        foreach (var actor in roles)
            foreach (var target in roles.Where(t => t != actor && RoleScope.Covers(actor, t)))
            {
                var pair = $"{actor} covers {target}";

                Assert.True(admits[target].IsSubsetOf(admits[actor]), $"{pair}, but {target} passes a policy {actor} does not");

                // No exemption for a role no policy admits: permission-gated endpoints
                // (requisitions) still apply the scopes to it. Skipping it here is how the first
                // version of this test missed Interviewer being company-wide.
                Assert.False(RoleScope.IsDepartmentScoped(actor) && !RoleScope.IsDepartmentScoped(target),
                    $"{pair}, but {actor} is department-scoped and {target} is not");
                Assert.False(RoleScope.IsExcludedFromCandidateData(actor) && !RoleScope.IsExcludedFromCandidateData(target),
                    $"{pair}, but {actor} is excluded from candidate data and {target} is not");
            }
    }
}
