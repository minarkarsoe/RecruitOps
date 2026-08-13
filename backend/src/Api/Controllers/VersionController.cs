using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using RecruitOps.Application.Interfaces;

namespace RecruitOps.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class VersionController : ControllerBase
{
    private readonly IFeatureFlagService _featureFlagService;
    private readonly IWebHostEnvironment _environment;

    public VersionController(
        IFeatureFlagService featureFlagService,
        IWebHostEnvironment environment)
    {
        _featureFlagService = featureFlagService;
        _environment = environment;
    }

    [HttpGet]
    public async Task<IActionResult> GetVersion(CancellationToken ct)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version?.ToString() ?? "0.1.0";
        var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? version;

        var flags = await _featureFlagService.GetAllFlagsAsync(ct);

        var versionInfo = new VersionInfoDto
        {
            Version = version,
            InformationalVersion = informationalVersion,
            Environment = _environment.EnvironmentName,
            DeploymentTier = "SingleTenantDedicated",
            Timestamp = DateTime.UtcNow,
            FeatureFlags = flags
        };

        return Ok(versionInfo);
    }
}

public class VersionInfoDto
{
    public string Version { get; set; } = "0.1.0";
    public string InformationalVersion { get; set; } = string.Empty;
    public string Environment { get; set; } = "Production";
    public string DeploymentTier { get; set; } = "SingleTenantDedicated";
    public DateTime Timestamp { get; set; }
    public IReadOnlyDictionary<string, bool> FeatureFlags { get; set; } = new Dictionary<string, bool>();
}
