using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using RecruitOps.Api.Auth;
using RecruitOps.Application.DTOs.Ai;
using Xunit;

namespace RecruitOps.Api.Tests;

/// <summary>
/// The executive summary's <c>language</c> field, end to end (ADR-0009).
///
/// <para><b>Why this file exists.</b> The SPA has shipped an EN / MY / Bilingual selector since
/// Module 2 and sent a <c>language</c> field on every request. The API's request record did not
/// have one, so model binding discarded it silently — the control looked like it worked and
/// never changed a single response. Nothing caught it because no test ever asserted what the
/// API actually binds; the frontend's own test mocked the response in the shape the frontend
/// wanted. Discovered 2026-08-28 by reading the running service's OpenAPI document.</para>
///
/// <para>These tests are deliberately about <b>binding and effect</b>, not about the model's
/// prose. They run against the development stub (no API key configured in the test host), which
/// is exactly the path a developer sees, and they assert that the requested language reaches it
/// and changes the output. A test that only checked the field parsed would have passed against
/// the bug that motivated this file — the field was always parseable, it just was not there.</para>
/// </summary>
public class ExecutiveSummaryLanguageTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;

    public ExecutiveSummaryLanguageTests(CustomWebAppFactory factory) => _factory = factory;

    private HttpClient CreateAuthorizedClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Tenant", _factory.TenantA.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Roles", Roles.Recruiter);
        return client;
    }

    private async Task<ExecutiveSummaryDto> PostAsync(string? language)
    {
        var client = CreateAuthorizedClient();
        var response = await client.PostAsJsonAsync(
            "/api/ai/gemini/executive-summary",
            new GenerateExecutiveSummaryRequest(Guid.NewGuid(), null, null, language));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ExecutiveSummaryDto>())!;
    }

    /// <summary>Burmese Unicode occupies U+1000–U+109F. Zawgyi uses the same block, so this is a
    /// presence check for Myanmar script, not an encoding check — the encoding guarantee is in
    /// the prompt instruction and in ADR-0009's ingest normalisation.</summary>
    private static bool ContainsMyanmarScript(string s) => s.Any(c => c >= 'က' && c <= '႟');

    [Fact]
    public async Task The_Request_Binds_Language_Instead_Of_Discarding_It()
    {
        // The whole bug in one assertion: before `Language` existed on the record, this request
        // and the English one below returned byte-identical bodies.
        var burmese = await PostAsync("my");
        var english = await PostAsync("en");

        Assert.NotEqual(english.ExecutiveSummary, burmese.ExecutiveSummary);
    }

    [Fact]
    public async Task Asking_For_Burmese_Returns_Myanmar_Script()
    {
        var result = await PostAsync("my");

        Assert.True(ContainsMyanmarScript(result.Headline), "headline is not in Myanmar script");
        Assert.True(ContainsMyanmarScript(result.ExecutiveSummary), "summary is not in Myanmar script");
        Assert.All(result.KeyHighlights, h => Assert.True(ContainsMyanmarScript(h)));
        Assert.All(result.RecommendedInterviewQuestions, q => Assert.True(ContainsMyanmarScript(q)));
    }

    [Fact]
    public async Task Asking_For_Burmese_Returns_No_English_Alongside_It()
    {
        // "my" means Burmese, not "Burmese as well". Bilingual is its own value, and conflating
        // the two is how a Burmese-only reader ends up with half a summary they cannot read.
        var result = await PostAsync("my");

        Assert.DoesNotContain("Senior Lead Architect", result.Headline);
        Assert.DoesNotContain("\n\n", result.ExecutiveSummary);
    }

    [Fact]
    public async Task Bilingual_Returns_English_And_Burmese_In_Every_Field()
    {
        var result = await PostAsync("bilingual");

        foreach (var field in new[] { result.Headline, result.ExecutiveSummary })
        {
            Assert.Contains("\n\n", field);          // the two renderings are separated
            Assert.True(ContainsMyanmarScript(field), "bilingual field carries no Myanmar script");
            // English first, so a reader who only reads English is not made to scroll past
            // a script they cannot read to reach their own.
            Assert.False(ContainsMyanmarScript(field.Split("\n\n")[0]), "the first half is not English");
        }

        Assert.All(result.KeyHighlights, h => Assert.Contains("\n\n", h));
        Assert.All(result.RecommendedInterviewQuestions, q => Assert.Contains("\n\n", q));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("en")]
    public async Task English_Is_The_Default_And_Carries_No_Myanmar_Script(string? language)
    {
        var result = await PostAsync(language);

        Assert.False(ContainsMyanmarScript(result.Headline));
        Assert.False(ContainsMyanmarScript(result.ExecutiveSummary));
    }

    [Theory]
    [InlineData("MY")]
    [InlineData("Bilingual")]
    public async Task Language_Is_Case_Insensitive(string language)
    {
        // The value crosses a JSON boundary from a browser. Rejecting "MY" because the SPA
        // happened to upper-case it would be a support ticket, not a safety property.
        var result = await PostAsync(language);

        Assert.True(ContainsMyanmarScript(result.ExecutiveSummary));
    }

    [Fact]
    public async Task An_Unknown_Language_Falls_Back_To_English_Rather_Than_Failing()
    {
        // The API is not the place to reject a language code the UI may add next week — and a
        // 400 here would turn a cosmetic mismatch into a broken screen.
        var result = await PostAsync("klingon");

        Assert.False(ContainsMyanmarScript(result.ExecutiveSummary));
        Assert.Contains("Senior Lead Architect", result.Headline);
    }

    [Fact]
    public async Task The_Simulated_Header_Is_Still_Set_For_Every_Language()
    {
        // These responses are fabricated by the development stub. That has bitten this project
        // before — a clean audit once sat on top of invented candidate data — so the marker must
        // survive the language branch rather than only the default path.
        var client = CreateAuthorizedClient();

        foreach (var language in new[] { "en", "my", "bilingual" })
        {
            var response = await client.PostAsJsonAsync(
                "/api/ai/gemini/executive-summary",
                new GenerateExecutiveSummaryRequest(Guid.NewGuid(), null, null, language));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(
                response.Headers.Contains("X-Ai-Simulated"),
                $"X-Ai-Simulated missing for language '{language}' — fabricated output is unmarked.");
        }
    }

    [Fact]
    public async Task Language_Is_Part_Of_The_Published_Request_Contract()
    {
        // The SPA sends this field. If it ever leaves the record again, it goes back to being
        // silently discarded, which is precisely the failure this file was written for.
        var property = typeof(GenerateExecutiveSummaryRequest).GetProperty("Language");

        Assert.NotNull(property);
        Assert.Equal(typeof(string), property!.PropertyType);
    }
}
