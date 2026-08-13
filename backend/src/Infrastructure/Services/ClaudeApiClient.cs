using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RecruitOps.Application.Common.Exceptions;
using RecruitOps.Application.DTOs.Ai;
using RecruitOps.Application.Interfaces;
using RecruitOps.Infrastructure.Options;

namespace RecruitOps.Infrastructure.Services;

public class ClaudeApiClient : IClaudeService
{
    private const string ProviderName = "Claude";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _httpClient;
    private readonly ClaudeOptions _options;
    private readonly ILogger<ClaudeApiClient> _logger;
    private readonly IMyanmarScriptNormalizer _normalizer;
    private readonly IAiSimulationScope? _simulation;

    public ClaudeApiClient(
        HttpClient httpClient,
        IOptions<ClaudeOptions> options,
        ILogger<ClaudeApiClient> logger,
        IMyanmarScriptNormalizer normalizer,
        IAiSimulationScope? simulation = null)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
        _normalizer = normalizer;
        _simulation = simulation;
    }

    /// <summary>
    /// Decides, once, what an unconfigured key means. Returns true when the caller should serve a
    /// development stub; throws <see cref="AiApiKeyMissingException"/> — which the API turns into
    /// 402 — when it should not.
    /// </summary>
    /// <remarks>
    /// Every public method starts here so the rule cannot be added to two of three siblings. It is
    /// only ever reached when the key is missing: once a key is configured a failing call raises
    /// <see cref="AiProviderUnavailableException"/> and no stub is served at all.
    /// </remarks>
    private bool ShouldServeDevelopmentStub()
    {
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return false;
        }

        if (!_options.EnableFallback)
        {
            throw new AiApiKeyMissingException(ProviderName);
        }

        _logger.LogWarning(
            "Claude API key is not configured and AI:Claude:EnableFallback is on — returning a fabricated sample. "
            + "Responses are stamped X-Ai-Simulated: true. This must not be enabled outside local development.");
        _simulation?.MarkSimulated(ProviderName);
        return true;
    }

    public async Task<ParsedResumeResultDto> ParseResumeAsync(ParseResumeRequest request, CancellationToken ct = default)
    {
        var normalizedResumeText = _normalizer.Normalize(request.ResumeText).NormalizedText;

        if (ShouldServeDevelopmentStub())
        {
            return GetParsedResumeStub(request);
        }

        var completion = await RequestCompletionAsync(
            $"Extract structured resume JSON from the following text:\n{normalizedResumeText}", ct);

        return DeserializeCompletion<ParsedResumeResultDto>(completion);
    }

    public async Task<CandidateMatchAnalysisDto> MatchCandidateAsync(
        MatchCandidateRequest request, string? candidateProfileData = null, string? jobPostingData = null, CancellationToken ct = default)
    {
        if (ShouldServeDevelopmentStub())
        {
            return GetMatchAnalysisStub(request);
        }

        var completion = await RequestCompletionAsync(
            $"Analyze the fit between Candidate {request.CandidateId} and Job Posting {request.JobPostingId}. "
            + $"Candidate profile: {candidateProfileData}\nJob posting: {jobPostingData}", ct);

        return DeserializeCompletion<CandidateMatchAnalysisDto>(completion);
    }

    /// <summary>
    /// Calls Anthropic and returns the completion text. Any outcome that is not a usable completion
    /// — transport fault, timeout, non-success status, unexpected body — raises
    /// <see cref="AiProviderUnavailableException"/>, which the API turns into 502.
    /// </summary>
    private async Task<string> RequestCompletionAsync(string prompt, CancellationToken ct)
    {
        HttpResponseMessage resp;
        try
        {
            using var reqMsg = new HttpRequestMessage(HttpMethod.Post, _options.ApiUrl);
            reqMsg.Headers.Add("x-api-key", _options.ApiKey);
            reqMsg.Headers.Add("anthropic-version", "2023-06-01");
            reqMsg.Content = JsonContent.Create(new
            {
                model = _options.Model,
                max_tokens = _options.MaxTokens,
                messages = new[] { new { role = "user", content = prompt } }
            });

            resp = await _httpClient.SendAsync(reqMsg, ct);
        }
        // A caller who went away is a cancellation, not a provider fault; a timeout is the reverse.
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogError(ex, "Anthropic API call timed out after {TimeoutSeconds}s.", _options.TimeoutSeconds);
            throw new AiProviderUnavailableException(ProviderName, "the request timed out", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Anthropic API call failed before a response was received.");
            throw new AiProviderUnavailableException(ProviderName, "the request failed before a response was received", ex);
        }

        using (resp)
        {
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogError("Anthropic API returned {StatusCode}.", (int)resp.StatusCode);
                throw new AiProviderUnavailableException(ProviderName, $"HTTP {(int)resp.StatusCode} from the provider");
            }

            var body = await resp.Content.ReadAsStringAsync(ct);
            try
            {
                using var doc = JsonDocument.Parse(body);
                var text = doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString();
                if (string.IsNullOrWhiteSpace(text))
                {
                    throw new AiProviderUnavailableException(ProviderName, "the response carried no completion text");
                }

                return text;
            }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or IndexOutOfRangeException)
            {
                _logger.LogError(ex, "Anthropic API response was not in the expected shape.");
                throw new AiProviderUnavailableException(ProviderName, "the response was not in the expected shape", ex);
            }
        }
    }

    private static T DeserializeCompletion<T>(string completion)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(completion, JsonOptions)
                   ?? throw new AiProviderUnavailableException(ProviderName, $"the completion deserialized to null for {typeof(T).Name}");
        }
        catch (JsonException ex)
        {
            throw new AiProviderUnavailableException(ProviderName, $"the completion was not valid JSON for {typeof(T).Name}", ex);
        }
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
