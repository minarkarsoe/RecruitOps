using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RecruitOps.Api.Auth;
using RecruitOps.Application.DTOs.Search;
using RecruitOps.Domain.Entities;
using RecruitOps.Domain.Enums;
using RecruitOps.Infrastructure.Persistence;
using Xunit;

namespace RecruitOps.Api.Tests.Search;

public class SearchImplementationChallengerTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;

    public SearchImplementationChallengerTests(CustomWebAppFactory factory)
    {
        _factory = factory;
        SeedTestData();
    }

    private HttpClient ClientFor(string role, Guid? userId = null)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Tenant", _factory.TenantA.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Roles", role);
        if (userId is not null)
            client.DefaultRequestHeaders.Add("X-Test-UserId", userId.Value.ToString());
        return client;
    }

    private void SeedTestData()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (db.Candidates.IgnoreQueryFilters().Any(c => c.Email == "challenger.v2.test1@alpha.test"))
            return;

        var now = DateTimeOffset.UtcNow;

        // Candidate 1: Exact title match for "DevOps Engineer" in FullName
        var candExact = new Candidate
        {
            TenantId = _factory.TenantA,
            FullName = "DevOps Engineer",
            Email = "challenger.test1@alpha.test",
            Phone = "0911111111",
            Source = SourceChannel.Direct
        };

        // Candidate 2: CV match only for "DevOps Engineer"
        var candCvOnly = new Candidate
        {
            TenantId = _factory.TenantA,
            FullName = "Kyaw Kyaw",
            Email = "challenger.test2@alpha.test",
            Phone = "0922222222",
            Source = SourceChannel.Direct
        };

        // Candidate 3: Burmese candidate name in Unicode NFC "အောင်အောင်"
        var candZawgyi = new Candidate
        {
            TenantId = _factory.TenantA,
            FullName = "အောင်အောင်", // Unicode Burmese text
            Email = "challenger.test3@alpha.test",
            Phone = "0933333333",
            Source = SourceChannel.Direct
        };

        // Candidate 4: HTML Special Chars in CV text
        var candHtmlChars = new Candidate
        {
            TenantId = _factory.TenantA,
            FullName = "HTML Special Candidate",
            Email = "challenger.test4@alpha.test",
            Phone = "0944444444",
            Source = SourceChannel.Direct
        };

        db.Candidates.AddRange(candExact, candCvOnly, candZawgyi, candHtmlChars);
        db.SaveChanges();

        var posting = new JobPosting
        {
            TenantId = _factory.TenantA,
            DepartmentId = _factory.SalesDepartmentId,
            Title = "Lead DevOps Engineer",
            Description = "Seeking experienced DevOps professional with Kubernetes experience.",
            Status = JobStatus.Live
        };
        db.JobPostings.Add(posting);
        db.SaveChanges();

        var app1 = new JobApplication
        {
            TenantId = _factory.TenantA,
            JobPostingId = posting.Id,
            CandidateId = candCvOnly.Id,
            Status = PipelineStatus.Applied,
            AppliedAt = now,
            ResumeExtractedText = "Experienced with DevOps Engineer practices, CI/CD pipelines, and cloud automation. DevOps Engineer DevOps Engineer.",
            IsZawgyiNormalized = false
        };

        var app2 = new JobApplication
        {
            TenantId = _factory.TenantA,
            JobPostingId = posting.Id,
            CandidateId = candHtmlChars.Id,
            Status = PipelineStatus.Applied,
            AppliedAt = now,
            ResumeExtractedText = "Expert in R&D and AT&T network security protocols with C# .NET core.",
            IsZawgyiNormalized = false
        };

        db.JobApplications.AddRange(app1, app2);
        db.SaveChanges();
    }

    [Fact]
    public async Task ChallengerTest1_RelevanceScoring_ExactTitleMatch_Outranks_CvMatch()
    {
        var client = ClientFor(Roles.Admin, _factory.AdminUserId);
        var res = await client.GetAsync("/api/search?q=DevOps%20Engineer");
        res.EnsureSuccessStatusCode();

        var dto = await res.Content.ReadFromJsonAsync<SearchResponseDto>();
        Assert.NotNull(dto);
        Assert.True(dto!.Items.Count >= 2);

        // Find candidate items
        var candExactItem = dto.Items.FirstOrDefault(i => i.Category == "Candidates" && i.Title == "DevOps Engineer");
        var candCvItem = dto.Items.FirstOrDefault(i => i.Category == "Candidates" && i.Title == "Kyaw Kyaw");

        Assert.NotNull(candExactItem);
        Assert.NotNull(candCvItem);

        // Exact name match score (100) must be higher than CV text match score (65 + occurrence bonus)
        Assert.True(candExactItem!.RelevanceScore > candCvItem!.RelevanceScore,
            $"Exact name match score ({candExactItem.RelevanceScore}) should be > CV match score ({candCvItem.RelevanceScore})");
    }

    [Fact]
    public async Task ChallengerTest2_RelevanceScoring_OccurrenceCount_IncreasesScore()
    {
        var client = ClientFor(Roles.Admin, _factory.AdminUserId);
        var res = await client.GetAsync("/api/search?q=DevOps%20Engineer");
        res.EnsureSuccessStatusCode();

        var dto = await res.Content.ReadFromJsonAsync<SearchResponseDto>();
        Assert.NotNull(dto);

        var candCvItem = dto.Items.FirstOrDefault(i => i.Category == "Candidates" && i.Title == "Kyaw Kyaw");
        Assert.NotNull(candCvItem);

        // Base score for CV text match is 65.0, text contains 3 occurrences -> +4 points bonus = 69.0
        Assert.Equal(69.0, candCvItem!.RelevanceScore);
    }

    [Fact]
    public async Task ChallengerTest3_ZawgyiBurmeseQuery_Converts_And_Finds_Unicode_Candidate()
    {
        var client = ClientFor(Roles.Admin, _factory.AdminUserId);

        // Zawgyi text for "အောင်" (\u1031\u1021\u102B\u1004\u103A)
        string zawgyiText = "\u1031\u1021\u102B\u1004\u103A";

        var res = await client.GetAsync($"/api/search?q={Uri.EscapeDataString(zawgyiText)}");
        res.EnsureSuccessStatusCode();

        var dto = await res.Content.ReadFromJsonAsync<SearchResponseDto>();
        Assert.NotNull(dto);

        // Proves Zawgyi query is normalized to Unicode "အောင်"
        Assert.Equal("အောင်", dto!.NormalizedQuery);
        Assert.True(dto.TotalMatches >= 1);
        Assert.Contains(dto.Items, item => item.Title.Contains("အောင်အောင်"));
    }

    [Fact]
    public async Task ChallengerTest4_SnippetExtraction_Length_And_MarkTag_Correctness()
    {
        var client = ClientFor(Roles.Admin, _factory.AdminUserId);
        var res = await client.GetAsync("/api/search?q=Kubernetes");
        res.EnsureSuccessStatusCode();

        var dto = await res.Content.ReadFromJsonAsync<SearchResponseDto>();
        Assert.NotNull(dto);

        var itemWithSnippet = dto!.Items.FirstOrDefault(i => i.DescriptionSnippet != null && i.DescriptionSnippet.Contains("<mark>"));
        Assert.NotNull(itemWithSnippet);

        string snippet = itemWithSnippet!.DescriptionSnippet!;
        
        // Check snippet contains <mark>Kubernetes</mark>
        Assert.Contains("<mark>Kubernetes</mark>", snippet, StringComparison.OrdinalIgnoreCase);

        // Strip HTML tags to check character count
        string rawText = Regex.Replace(snippet, "<.*?>", string.Empty);
        Assert.True(rawText.Length <= 200, $"Snippet raw text length ({rawText.Length}) should be <= ~200 chars");
    }

    [Fact]
    public async Task ChallengerTest5_SnippetExtraction_HtmlEncoding_Preserves_SpecialChars()
    {
        var client = ClientFor(Roles.Admin, _factory.AdminUserId);
        var res = await client.GetAsync("/api/search?q=R%26D");
        res.EnsureSuccessStatusCode();

        var dto = await res.Content.ReadFromJsonAsync<SearchResponseDto>();
        Assert.NotNull(dto);

        var candidateItem = dto!.Items.FirstOrDefault(i => i.Title == "HTML Special Candidate");
        Assert.NotNull(candidateItem);

        string snippet = candidateItem!.DescriptionSnippet!;
        // Check R&D was html encoded as R&amp;D inside <mark>
        Assert.Contains("<mark>R&amp;D</mark>", snippet);
    }
}
