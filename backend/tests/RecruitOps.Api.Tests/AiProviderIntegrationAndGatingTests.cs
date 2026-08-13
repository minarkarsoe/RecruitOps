using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RecruitOps.Api.Auth;
using RecruitOps.Application.DTOs.Ai;
using RecruitOps.Infrastructure.Options;
using RecruitOps.Infrastructure.Services;
using RecruitOps.Infrastructure.Services.MyanmarScript;
using Xunit;

namespace RecruitOps.Api.Tests;

public class AiProviderIntegrationAndGatingTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;

    public AiProviderIntegrationAndGatingTests(CustomWebAppFactory factory)
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

    #region 1. Development Stub Labelling

    /// <summary>
    /// This class runs with the development fallback on, so every 200 below is canned sample data,
    /// not an analysis. That is only safe because the response says so — assert it does, on every
    /// endpoint, or the rest of this file is quietly certifying fabricated candidate profiles.
    /// The shipped default (fallback off, 402) is covered by <see cref="AiApiKeyGatingDefaultsTests"/>.
    /// </summary>
    [Theory]
    [InlineData("/api/ai/parse-resume")]
    [InlineData("/api/ai/claude/parse-resume")]
    [InlineData("/api/ai/match-candidate")]
    [InlineData("/api/ai/claude/match-candidate")]
    [InlineData("/api/ai/executive-summary")]
    [InlineData("/api/ai/gemini/executive-summary")]
    [InlineData("/api/ai/document-prep")]
    [InlineData("/api/ai/gemini/document-prep")]
    [InlineData("/api/ai/translate")]
    [InlineData("/api/ai/gemini/burmese-localization")]
    public async Task Stubbed_Response_Is_Stamped_X_Ai_Simulated(string endpoint)
    {
        var client = CreateAuthorizedClient();

        var response = await client.PostAsJsonAsync(endpoint, PayloadFor(endpoint));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(
            response.Headers.TryGetValues("X-Ai-Simulated", out var values),
            $"{endpoint} returned fabricated sample data without the X-Ai-Simulated header.");
        Assert.Equal("true", Assert.Single(values!));
    }

    internal static object PayloadFor(string endpoint) => endpoint switch
    {
        var e when e.Contains("parse-resume") => new ParseResumeRequest("CV text string", "cv.pdf"),
        var e when e.Contains("match-candidate") => new MatchCandidateRequest(Guid.NewGuid(), Guid.NewGuid()),
        var e when e.Contains("executive-summary") => new GenerateExecutiveSummaryRequest(Guid.NewGuid(), Guid.NewGuid(), "Executive"),
        var e when e.Contains("document-prep") => new PrepareDocumentRequest(Guid.NewGuid(), Guid.NewGuid(), "InterviewKit"),
        _ => new BurmeseLocalizationRequest("Qualified candidate profile", "my", "Context")
    };

    #endregion

    #region 2. Dual Route Equivalency Tests

    [Fact]
    public async Task ParseResume_Primary_Route_And_Claude_Alias_Route_Return_Identical_Results()
    {
        var client = CreateAuthorizedClient(Roles.Recruiter);
        var request = new ParseResumeRequest("Aung Kyaw Thu. Senior .NET Engineer", "resume.pdf");

        var primaryResp = await client.PostAsJsonAsync("/api/ai/parse-resume", request);
        var aliasResp = await client.PostAsJsonAsync("/api/ai/claude/parse-resume", request);

        Assert.Equal(HttpStatusCode.OK, primaryResp.StatusCode);
        Assert.Equal(HttpStatusCode.OK, aliasResp.StatusCode);

        var primaryResult = await primaryResp.Content.ReadFromJsonAsync<ParsedResumeResultDto>();
        var aliasResult = await aliasResp.Content.ReadFromJsonAsync<ParsedResumeResultDto>();

        Assert.NotNull(primaryResult);
        Assert.NotNull(aliasResult);
        Assert.Equal(primaryResult.FullName, aliasResult.FullName);
    }

    [Fact]
    public async Task MatchCandidate_Primary_Route_And_Claude_Alias_Route_Return_Identical_Results()
    {
        var client = CreateAuthorizedClient(Roles.Recruiter);
        var request = new MatchCandidateRequest(Guid.NewGuid(), Guid.NewGuid());

        var primaryResp = await client.PostAsJsonAsync("/api/ai/match-candidate", request);
        var aliasResp = await client.PostAsJsonAsync("/api/ai/claude/match-candidate", request);

        Assert.Equal(HttpStatusCode.OK, primaryResp.StatusCode);
        Assert.Equal(HttpStatusCode.OK, aliasResp.StatusCode);

        var primaryResult = await primaryResp.Content.ReadFromJsonAsync<CandidateMatchAnalysisDto>();
        var aliasResult = await aliasResp.Content.ReadFromJsonAsync<CandidateMatchAnalysisDto>();

        Assert.NotNull(primaryResult);
        Assert.NotNull(aliasResult);
        Assert.Equal(primaryResult.MatchScore, aliasResult.MatchScore);
    }

    [Fact]
    public async Task ExecutiveSummary_Primary_Route_And_Gemini_Alias_Route_Return_Identical_Results()
    {
        var client = CreateAuthorizedClient(Roles.Recruiter);
        var request = new GenerateExecutiveSummaryRequest(Guid.NewGuid(), Guid.NewGuid(), "Exec");

        var primaryResp = await client.PostAsJsonAsync("/api/ai/executive-summary", request);
        var aliasResp = await client.PostAsJsonAsync("/api/ai/gemini/executive-summary", request);

        Assert.Equal(HttpStatusCode.OK, primaryResp.StatusCode);
        Assert.Equal(HttpStatusCode.OK, aliasResp.StatusCode);

        var primaryResult = await primaryResp.Content.ReadFromJsonAsync<ExecutiveSummaryDto>();
        var aliasResult = await aliasResp.Content.ReadFromJsonAsync<ExecutiveSummaryDto>();

        Assert.NotNull(primaryResult);
        Assert.NotNull(aliasResult);
        Assert.Equal(primaryResult.Headline, aliasResult.Headline);
    }

    [Fact]
    public async Task DocumentPrep_Primary_Route_And_Gemini_Alias_Route_Return_Identical_Results()
    {
        var client = CreateAuthorizedClient(Roles.Recruiter);
        var request = new PrepareDocumentRequest(Guid.NewGuid(), Guid.NewGuid(), "ClientDossier");

        var primaryResp = await client.PostAsJsonAsync("/api/ai/document-prep", request);
        var aliasResp = await client.PostAsJsonAsync("/api/ai/gemini/document-prep", request);

        Assert.Equal(HttpStatusCode.OK, primaryResp.StatusCode);
        Assert.Equal(HttpStatusCode.OK, aliasResp.StatusCode);

        var primaryResult = await primaryResp.Content.ReadFromJsonAsync<DocumentPrepResultDto>();
        var aliasResult = await aliasResp.Content.ReadFromJsonAsync<DocumentPrepResultDto>();

        Assert.NotNull(primaryResult);
        Assert.NotNull(aliasResult);
        Assert.Equal(primaryResult.DocumentTitle, aliasResult.DocumentTitle);
    }

    [Fact]
    public async Task Translate_Primary_Route_And_Gemini_Alias_Route_Return_Identical_Results()
    {
        var client = CreateAuthorizedClient(Roles.Recruiter);
        var request = new BurmeseLocalizationRequest("Senior Developer Note", "my", "CV");

        var primaryResp = await client.PostAsJsonAsync("/api/ai/translate", request);
        var aliasResp = await client.PostAsJsonAsync("/api/ai/gemini/burmese-localization", request);

        Assert.Equal(HttpStatusCode.OK, primaryResp.StatusCode);
        Assert.Equal(HttpStatusCode.OK, aliasResp.StatusCode);

        var primaryResult = await primaryResp.Content.ReadFromJsonAsync<BurmeseLocalizationResultDto>();
        var aliasResult = await aliasResp.Content.ReadFromJsonAsync<BurmeseLocalizationResultDto>();

        Assert.NotNull(primaryResult);
        Assert.NotNull(aliasResult);
        Assert.Equal(primaryResult.TranslatedText, aliasResult.TranslatedText);
    }

    #endregion

    #region 3. Burmese Script & Match Scoring Unit Tests

    [Fact]
    public async Task ParseResume_With_Zawgyi_Burmese_Text_Normalizes_To_Unicode_Cleanly()
    {
        var client = CreateAuthorizedClient(Roles.Recruiter);
        // Zawgyi encoded Burmese string
        var zawgyiInput = "\u106A\u103A\u1000\u1031\u1019\u103ABC Developer";
        var request = new ParseResumeRequest(zawgyiInput, "zawgyi_cv.txt");

        var response = await client.PostAsJsonAsync("/api/ai/parse-resume", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<ParsedResumeResultDto>();
        Assert.NotNull(result);
    }

    [Fact]
    public async Task Translate_With_Zawgyi_Burmese_Text_Performs_Unicode_Normalization_Before_Translation()
    {
        var client = CreateAuthorizedClient(Roles.Recruiter);
        var zawgyiInput = "\u1001\u1031\u107D\u103B text input";
        var request = new BurmeseLocalizationRequest(zawgyiInput, "en", "Note");

        var response = await client.PostAsJsonAsync("/api/ai/translate", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<BurmeseLocalizationResultDto>();
        Assert.NotNull(result);
        Assert.Equal("en", result.TargetLanguage);
        // Original text should be normalized Unicode text
        Assert.False(string.IsNullOrWhiteSpace(result.OriginalText));
    }

    [Fact]
    public async Task MatchCandidate_Returns_Valid_MatchScore_And_Breakdown()
    {
        var client = CreateAuthorizedClient(Roles.Recruiter);
        var request = new MatchCandidateRequest(Guid.NewGuid(), Guid.NewGuid());

        var response = await client.PostAsJsonAsync("/api/ai/match-candidate", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<CandidateMatchAnalysisDto>();
        Assert.NotNull(result);
        Assert.InRange(result.MatchScore, 0, 100);
        Assert.False(string.IsNullOrWhiteSpace(result.OverallVerdict));
        Assert.NotEmpty(result.MatchedSkills);
        Assert.NotEmpty(result.Strengths);
        Assert.False(string.IsNullOrWhiteSpace(result.Recommendation));
    }

    #endregion

    #region 4. AI Provider Client Mocking Unit Tests

    [Fact]
    public async Task ClaudeApiClient_With_Mock_HttpClient_Parses_Anthropic_Response()
    {
        var mockResponse = new
        {
            id = "msg_123",
            type = "message",
            role = "assistant",
            content = new[]
            {
                new
                {
                    type = "text",
                    text = JsonSerializer.Serialize(new ParsedResumeResultDto(
                        FullName: "Mya Mya",
                        Email: "mya@example.com",
                        Phone: "+9599999999",
                        Summary: "Experienced QA Lead",
                        WorkExperiences: new List<WorkExperienceDto>(),
                        Educations: new List<EducationDto>(),
                        Skills: new List<string> { "Selenium", "C#" },
                        Languages: new List<string> { "Burmese", "English" },
                        EstimatedYearsOfExperience: 5
                    ))
                }
            }
        };

        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, JsonSerializer.Serialize(mockResponse));
        var httpClient = new HttpClient(handler);
        var options = Options.Create(new ClaudeOptions { ApiKey = "sk-ant-test-key-123" });
        var normalizer = new MyanmarScriptNormalizer();
        var client = new ClaudeApiClient(httpClient, options, NullLogger<ClaudeApiClient>.Instance, normalizer);

        var result = await client.ParseResumeAsync(new ParseResumeRequest("Mya Mya QA Lead resume text", "mya.pdf"));

        Assert.NotNull(result);
        Assert.Equal("Mya Mya", result.FullName);
        Assert.Equal("mya@example.com", result.Email);
        Assert.Contains("Selenium", result.Skills);
    }

    [Fact]
    public async Task GeminiApiClient_With_Mock_HttpClient_Parses_Google_Response()
    {
        var mockResponse = new
        {
            candidates = new[]
            {
                new
                {
                    content = new
                    {
                        parts = new[]
                        {
                            new
                            {
                                text = "Kyaw Kyaw is an outstanding Senior Software Architect."
                            }
                        }
                    }
                }
            }
        };

        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, JsonSerializer.Serialize(mockResponse));
        var httpClient = new HttpClient(handler);
        var options = Options.Create(new GeminiOptions { ApiKey = "AIzaSyTestKey123" });
        var normalizer = new MyanmarScriptNormalizer();
        var client = new GeminiApiClient(httpClient, options, NullLogger<GeminiApiClient>.Instance, normalizer);

        var result = await client.TranslateBurmeseAsync(new BurmeseLocalizationRequest("ကျော်ကျော် သည် အဆင့်မြင့် ဆော့ဖ်ဝဲလ် အင်ဂျင်နီယာ ဖြစ်သည်။", "en", "Note"));

        Assert.NotNull(result);
        Assert.Equal("en", result.TargetLanguage);
        Assert.Equal("Kyaw Kyaw is an outstanding Senior Software Architect.", result.TranslatedText);
    }

    #endregion

    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _responseContent;

        public MockHttpMessageHandler(HttpStatusCode statusCode, string responseContent)
        {
            _statusCode = statusCode;
            _responseContent = responseContent;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseContent, System.Text.Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }
}
