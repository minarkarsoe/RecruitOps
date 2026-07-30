using System.Net;
using System.Net.Http.Json;
using RecruitOps.Api.Auth;
using RecruitOps.Application.DTOs;
using Xunit;

namespace RecruitOps.Api.Tests;

/// <summary>Module 3.3 configuration — which criteria set actually applies (ADR-0017 §1).
///
/// <para>Its own class, and therefore its own database: creating a department-level template
/// changes what every interview in the same store resolves to, so mixing these with the
/// scoring tests would make both depend on execution order.</para>
/// </summary>
public class ScorecardTemplateResolutionTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;
    private readonly Module3Scenario _scenario;

    public ScorecardTemplateResolutionTests(CustomWebAppFactory factory)
    {
        _factory = factory;
        _scenario = new Module3Scenario(factory);
    }

    private static SaveScorecardTemplateRequest Template(
        string name, Guid? departmentId = null, Guid? jobPostingId = null) =>
        new()
        {
            Name = name,
            DepartmentId = departmentId,
            JobPostingId = jobPostingId,
            Criteria = new List<ScorecardCriterionInput>
            {
                new() { Label = $"{name} criterion", Type = "Rating", IsRequired = true },
            },
        };

    [Fact]
    public async Task Most_Specific_Scope_Wins()
    {
        var (postingId, _) = await _scenario.ApplicationAsync("Resolution Role");

        var companyWide = await _scenario.Recruiter()
            .PostAsJsonAsync("/api/scorecardtemplates", Template("Company default"));
        companyWide.EnsureSuccessStatusCode();

        var atCompany = await _scenario.Recruiter()
            .GetFromJsonAsync<ScorecardTemplateDto>($"/api/scorecardtemplates/resolve/{postingId}");
        Assert.Equal("Company default", atCompany!.Name);

        var department = await _scenario.Recruiter().PostAsJsonAsync(
            "/api/scorecardtemplates", Template("Sales standard", departmentId: _factory.SalesDepartmentId));
        department.EnsureSuccessStatusCode();

        // The department is the level at which comparison means anything — the criteria that
        // make two salespeople comparable make a salesperson and an engineer incomparable.
        var atDepartment = await _scenario.Recruiter()
            .GetFromJsonAsync<ScorecardTemplateDto>($"/api/scorecardtemplates/resolve/{postingId}");
        Assert.Equal("Sales standard", atDepartment!.Name);

        var posting = await _scenario.Recruiter().PostAsJsonAsync(
            "/api/scorecardtemplates", Template("This role only", jobPostingId: postingId));
        posting.EnsureSuccessStatusCode();

        var atPosting = await _scenario.Recruiter()
            .GetFromJsonAsync<ScorecardTemplateDto>($"/api/scorecardtemplates/resolve/{postingId}");
        Assert.Equal("This role only", atPosting!.Name);
    }

    [Fact]
    public async Task A_Template_Cannot_Belong_To_A_Department_And_A_Posting_At_Once()
    {
        var (postingId, _) = await _scenario.ApplicationAsync("Ambiguous Scope Role");

        var res = await _scenario.Recruiter().PostAsJsonAsync("/api/scorecardtemplates",
            Template("Both at once", departmentId: _factory.SalesDepartmentId, jobPostingId: postingId));

        // The two scopes are alternatives, not a hierarchy to fill in. Allowing both invents
        // a third scope with no defined precedence, and resolution would silently pick one.
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task Two_Active_Templates_Cannot_Share_A_Scope()
    {
        var first = await _scenario.Recruiter().PostAsJsonAsync("/api/scorecardtemplates",
            Template("Finance standard", departmentId: _factory.FinanceDepartmentId));
        first.EnsureSuccessStatusCode();

        var second = await _scenario.Recruiter().PostAsJsonAsync("/api/scorecardtemplates",
            Template("Finance standard v2", departmentId: _factory.FinanceDepartmentId));

        // Otherwise resolution becomes "whichever row came back first" — stable in testing,
        // arbitrary in production.
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task A_Template_Needs_At_Least_One_Criterion_With_A_Known_Type()
    {
        var empty = await _scenario.Recruiter().PostAsJsonAsync("/api/scorecardtemplates",
            new SaveScorecardTemplateRequest
            {
                Name = "Empty",
                Criteria = Array.Empty<ScorecardCriterionInput>(),
            });
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);

        var badType = await _scenario.Recruiter().PostAsJsonAsync("/api/scorecardtemplates",
            new SaveScorecardTemplateRequest
            {
                Name = "Unknown type",
                Criteria = new List<ScorecardCriterionInput>
                {
                    new() { Label = "Vibes", Type = "Vibes" },
                },
            });
        Assert.Equal(HttpStatusCode.Conflict, badType.StatusCode);
    }

    [Fact]
    public async Task Criteria_Are_Sequenced_By_The_Order_They_Were_Sent()
    {
        // Scoped to its own posting: every test in this class shares one database, and the
        // service allows only one active template per scope, so tests must not collide on one.
        var (postingId, _) = await _scenario.ApplicationAsync("Ordering Role");

        var res = await _scenario.Recruiter().PostAsJsonAsync("/api/scorecardtemplates",
            new SaveScorecardTemplateRequest
            {
                Name = "Ordered template",
                JobPostingId = postingId,
                Criteria = new List<ScorecardCriterionInput>
                {
                    new() { Label = "First", Type = "Rating" },
                    new() { Label = "Second", Type = "YesNo" },
                    new() { Label = "Third", Type = "Text" },
                },
            });
        res.EnsureSuccessStatusCode();

        var dto = (await res.Content.ReadFromJsonAsync<ScorecardTemplateDto>())!;

        // Sequence derived from list position, so gaps and duplicates are unrepresentable —
        // the same trick ApprovalChainStep uses.
        Assert.Equal(new[] { 1, 2, 3 }, dto.Criteria.Select(c => c.Sequence).ToArray());
        Assert.Equal(new[] { "First", "Second", "Third" }, dto.Criteria.Select(c => c.Label).ToArray());
    }

    [Fact]
    public async Task A_Hiring_Manager_Cannot_Define_The_Criteria_Everyone_Is_Judged_By()
    {
        var res = await _scenario.SalesManager().PostAsJsonAsync("/api/scorecardtemplates",
            Template("Manager's own", departmentId: _factory.SalesDepartmentId));

        // Setting a department's criteria is setting the standard everyone in it is compared
        // against — the same reasoning that made approval chains admin-only in Module 1.
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Any_Internal_User_Can_Read_The_Criteria_They_Will_Be_Asked()
    {
        var res = await _scenario.Client(Roles.HiringManager, _factory.HiringManagerUserId)
            .GetAsync("/api/scorecardtemplates");

        // An interviewer should be able to see what they will be asked before the day.
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }
}
