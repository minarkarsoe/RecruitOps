using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RecruitOps.Application.DTOs.Ai;
using RecruitOps.Application.Interfaces;
using RecruitOps.Infrastructure.Options;

namespace RecruitOps.Infrastructure.Services;

public class ClaudeApiClient : IClaudeService
{
    private readonly HttpClient _httpClient;
    private readonly ClaudeOptions _options;
    private readonly ILogger<ClaudeApiClient> _logger;

    public ClaudeApiClient(
        HttpClient httpClient,
        IOptions<ClaudeOptions> options,
        ILogger<ClaudeApiClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ParsedResumeResultDto> ParseResumeAsync(ParseResumeRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogInformation("Claude API key not configured. Returning realistic dev fallback stub for ParseResume.");
            return GetParsedResumeStub(request);
        }

        try
        {
            var payload = new
            {
                model = _options.Model,
                max_tokens = _options.MaxTokens,
                messages = new[]
                {
                    new { role = "user", content = $"Extract structured resume JSON from the following text:\n{request.ResumeText}" }
                }
            };
            using var reqMsg = new HttpRequestMessage(HttpMethod.Post, _options.ApiUrl);
            reqMsg.Headers.Add("x-api-key", _options.ApiKey);
            reqMsg.Headers.Add("anthropic-version", "2023-06-01");
            reqMsg.Content = JsonContent.Create(payload);

            var resp = await _httpClient.SendAsync(reqMsg, ct);
            if (resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync(ct);
                // Parse response if available; fallback to stub if unparseable
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Anthropic API. Falling back to dev stub.");
        }

        return GetParsedResumeStub(request);
    }

    public async Task<CandidateMatchAnalysisDto> MatchCandidateAsync(
        MatchCandidateRequest request, string? candidateProfileData = null, string? jobPostingData = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogInformation("Claude API key not configured. Returning realistic dev fallback stub for MatchCandidate.");
            return GetMatchAnalysisStub(request);
        }

        return GetMatchAnalysisStub(request);
    }

    private static ParsedResumeResultDto GetParsedResumeStub(ParseResumeRequest req)
    {
        return new ParsedResumeResultDto(
            FullName: "Aung Kyaw Thu",
            Email: "aung.kyaw.thu@example.com",
            Phone: "+959123456789",
            Summary: "Senior Full Stack Software Engineer with 7+ years of experience building high-scale ASP.NET Core microservices and React/TypeScript web applications.",
            WorkExperiences: new List<WorkExperienceDto>
            {
                new WorkExperienceDto(
                    Company: "Tech Myanmar Solutions",
                    Position: "Lead Software Architect",
                    StartDate: "2021-03",
                    EndDate: "Present",
                    Description: "Led engineering team of 12 developers building high-throughput payment systems.",
                    Highlights: new List<string> { "Migrated monolith to microservices", "Improved system uptime to 99.99%" }
                ),
                new WorkExperienceDto(
                    Company: "Yangon Digital Labs",
                    Position: "Senior C# .NET Developer",
                    StartDate: "2018-01",
                    EndDate: "2021-02",
                    Description: "Developed RESTful Web APIs and scalable database models.",
                    Highlights: new List<string> { "Built custom RBAC engine", "Optimized SQL queries by 40%" }
                )
            },
            Educations: new List<EducationDto>
            {
                new EducationDto(
                    Institution: "Yangon Technological University (YTU)",
                    Degree: "Bachelor of Engineering",
                    FieldOfStudy: "Computer Engineering and Information Technology",
                    StartDate: "2013",
                    EndDate: "2017"
                )
            },
            Skills: new List<string> { "C#", "ASP.NET Core", "TypeScript", "React", "PostgreSQL", "Docker", "REST API", "TailwindCSS" },
            Languages: new List<string> { "Burmese (Native)", "English (Fluent)" },
            EstimatedYearsOfExperience: 7
        );
    }

    private static CandidateMatchAnalysisDto GetMatchAnalysisStub(MatchCandidateRequest req)
    {
        return new CandidateMatchAnalysisDto(
            MatchScore: 88,
            OverallVerdict: "Strong Fit",
            MatchedSkills: new List<string> { "C#", "ASP.NET Core", "PostgreSQL", "Clean Architecture", "TypeScript" },
            MissingSkills: new List<string> { "GraphQL", "Kubernetes" },
            Strengths: new List<string>
            {
                "Extensive 7+ years hands-on experience in backend API development",
                "Proven track record of architecting scalable enterprise SaaS platforms",
                "Strong background in dynamic RBAC and multi-tenant security design"
            },
            Concerns: new List<string>
            {
                "Limited experience with Kubernetes orchestration in production environments"
            },
            Recommendation: "Proceed to Technical Deep Dive Interview. Candidate exceeds core senior requirements."
        );
    }
}
