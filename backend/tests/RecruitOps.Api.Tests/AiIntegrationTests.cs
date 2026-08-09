using System.Net;
using System.Net.Http.Json;
using RecruitOps.Api.Auth;
using RecruitOps.Application.DTOs.Ai;
using Xunit;

namespace RecruitOps.Api.Tests;

public class AiIntegrationTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;

    public AiIntegrationTests(CustomWebAppFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateAuthorizedClient(string role = Roles.Recruiter)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Tenant", _factory.TenantA.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Roles", role);
        return client;
    }

    private HttpClient CreateUnauthenticatedClient()
    {
        return _factory.CreateClient();
    }

    private HttpClient CreateRestrictedClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Tenant", _factory.TenantA.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Roles", "Interviewer"); // Role without AI permissions
        return client;
    }

    #region Authentication & Authorization Tests (401 & 403)

    [Theory]
    [InlineData("/api/ai/claude/parse-resume")]
    [InlineData("/api/ai/claude/match-candidate")]
    [InlineData("/api/ai/gemini/executive-summary")]
    [InlineData("/api/ai/gemini/document-prep")]
    [InlineData("/api/ai/gemini/burmese-localization")]
    public async Task AiEndpoints_Return_401_Unauthorized_When_Unauthenticated(string endpoint)
    {
        var client = CreateUnauthenticatedClient();
        var response = await client.PostAsJsonAsync(endpoint, new { });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/ai/claude/parse-resume")]
    [InlineData("/api/ai/claude/match-candidate")]
    [InlineData("/api/ai/gemini/executive-summary")]
    [InlineData("/api/ai/gemini/document-prep")]
    [InlineData("/api/ai/gemini/burmese-localization")]
    public async Task Restricted_Role_Returns_403_Forbidden_On_Protected_Ai_Endpoints(string endpoint)
    {
        var client = CreateRestrictedClient();
        var response = await client.PostAsJsonAsync(endpoint, new { });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    #endregion

    #region Input Validation Tests (400 Bad Request)

    [Fact]
    public async Task ParseResume_Returns_400_BadRequest_When_ResumeText_Is_Empty()
    {
        var client = CreateAuthorizedClient(Roles.Recruiter);
        var request = new ParseResumeRequest("", "resume.pdf");

        var response = await client.PostAsJsonAsync("/api/ai/claude/parse-resume", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MatchCandidate_Returns_400_BadRequest_When_Guids_Are_Empty()
    {
        var client = CreateAuthorizedClient(Roles.Recruiter);
        var request = new MatchCandidateRequest(Guid.Empty, Guid.Empty);

        var response = await client.PostAsJsonAsync("/api/ai/claude/match-candidate", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ExecutiveSummary_Returns_400_BadRequest_When_CandidateId_Is_Empty()
    {
        var client = CreateAuthorizedClient(Roles.Recruiter);
        var request = new GenerateExecutiveSummaryRequest(Guid.Empty, Guid.NewGuid(), "Executive");

        var response = await client.PostAsJsonAsync("/api/ai/gemini/executive-summary", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000", "00000000-0000-0000-0000-000000000000", "InterviewKit")]
    [InlineData("a1b2c3d4-e5f6-7890-abcd-ef1234567890", "00000000-0000-0000-0000-000000000000", "InterviewKit")]
    [InlineData("00000000-0000-0000-0000-000000000000", "a1b2c3d4-e5f6-7890-abcd-ef1234567890", "InterviewKit")]
    [InlineData("a1b2c3d4-e5f6-7890-abcd-ef1234567890", "a1b2c3d4-e5f6-7890-abcd-ef1234567890", "")]
    [InlineData("a1b2c3d4-e5f6-7890-abcd-ef1234567890", "a1b2c3d4-e5f6-7890-abcd-ef1234567890", "   ")]
    public async Task PrepareDocument_Returns_400_BadRequest_When_Inputs_Are_Invalid(string candidateId, string jobPostingId, string docType)
    {
        var client = CreateAuthorizedClient(Roles.Recruiter);
        var request = new PrepareDocumentRequest(Guid.Parse(candidateId), Guid.Parse(jobPostingId), docType);

        var response = await client.PostAsJsonAsync("/api/ai/gemini/document-prep", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("", "my")]
    [InlineData("   ", "my")]
    [InlineData("Hello", "")]
    [InlineData("Hello", "   ")]
    public async Task BurmeseLocalization_Returns_400_BadRequest_When_Parameters_Are_Empty(string sourceText, string targetLang)
    {
        var client = CreateAuthorizedClient(Roles.Recruiter);
        var request = new BurmeseLocalizationRequest(sourceText, targetLang, null);

        var response = await client.PostAsJsonAsync("/api/ai/gemini/burmese-localization", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    #region Successful AI Operations Tests (200 OK)

    [Theory]
    [InlineData(Roles.Recruiter)]
    [InlineData(Roles.HrDirector)]
    [InlineData(Roles.Admin)]
    public async Task All_Authorized_Roles_Can_Access_ParseResume(string role)
    {
        var client = CreateAuthorizedClient(role);
        var request = new ParseResumeRequest("Senior Engineer with 5 years experience", "cv.pdf");

        var response = await client.PostAsJsonAsync("/api/ai/claude/parse-resume", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ParseResume_Returns_200_OK_With_ParsedResumeResult()
    {
        var client = CreateAuthorizedClient(Roles.Recruiter);
        var request = new ParseResumeRequest(
            ResumeText: "Aung Kyaw Thu. Senior C# .NET Engineer with 7 years experience.",
            FileName: "cv.pdf"
        );

        var response = await client.PostAsJsonAsync("/api/ai/claude/parse-resume", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<ParsedResumeResultDto>();
        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.FullName));
        Assert.NotEmpty(result.Skills);
        Assert.True(result.EstimatedYearsOfExperience > 0);
    }

    [Fact]
    public async Task MatchCandidate_Returns_200_OK_With_CandidateMatchAnalysis()
    {
        var client = CreateAuthorizedClient(Roles.Recruiter);
        var request = new MatchCandidateRequest(Guid.NewGuid(), Guid.NewGuid());

        var response = await client.PostAsJsonAsync("/api/ai/claude/match-candidate", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<CandidateMatchAnalysisDto>();
        Assert.NotNull(result);
        Assert.InRange(result.MatchScore, 0, 100);
        Assert.False(string.IsNullOrWhiteSpace(result.OverallVerdict));
        Assert.NotEmpty(result.MatchedSkills);
    }

    [Fact]
    public async Task ExecutiveSummary_Returns_200_OK_With_ExecutiveSummaryResult()
    {
        var client = CreateAuthorizedClient(Roles.Recruiter);
        var request = new GenerateExecutiveSummaryRequest(Guid.NewGuid(), Guid.NewGuid(), "Executive");

        var response = await client.PostAsJsonAsync("/api/ai/gemini/executive-summary", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<ExecutiveSummaryDto>();
        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.Headline));
        Assert.NotEmpty(result.KeyHighlights);
        Assert.NotEmpty(result.RecommendedInterviewQuestions);
    }

    [Theory]
    [InlineData("InterviewKit", "Candidate Interview Kit & Assessment Guide")]
    [InlineData("ClientDossier", "Executive Candidate Dossier (Client Presentation)")]
    [InlineData("JdBrief", "Job Description & Sourcing Brief")]
    public async Task PrepareDocument_Returns_200_OK_For_Various_DocumentTypes(string docType, string expectedTitle)
    {
        var client = CreateAuthorizedClient(Roles.Recruiter);
        var request = new PrepareDocumentRequest(Guid.NewGuid(), Guid.NewGuid(), docType);

        var response = await client.PostAsJsonAsync("/api/ai/gemini/document-prep", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<DocumentPrepResultDto>();
        Assert.NotNull(result);
        Assert.Equal(expectedTitle, result.DocumentTitle);
        Assert.Contains("#", result.ContentMarkdown);
        Assert.Contains("<div", result.ContentHtml);
    }

    [Fact]
    public async Task BurmeseLocalization_Returns_200_OK_With_BurmeseLocalizationResult()
    {
        var client = CreateAuthorizedClient(Roles.Recruiter);
        var request = new BurmeseLocalizationRequest("Qualified candidate with strong background", "my", "ResumeNote");

        var response = await client.PostAsJsonAsync("/api/ai/gemini/burmese-localization", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<BurmeseLocalizationResultDto>();
        Assert.NotNull(result);
        Assert.Equal("my", result.TargetLanguage);
        Assert.False(string.IsNullOrWhiteSpace(result.TranslatedText));
    }

    #endregion
}
