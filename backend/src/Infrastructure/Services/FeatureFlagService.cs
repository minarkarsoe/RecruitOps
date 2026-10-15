using Microsoft.Extensions.Configuration;

using RecruitOps.Application.Interfaces;

namespace RecruitOps.Infrastructure.Services;

public class FeatureFlagService : IFeatureFlagService
{
    private readonly IConfiguration _configuration;

    public FeatureFlagService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public Task<bool> IsEnabledAsync(string featureName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(featureName))
        {
            return Task.FromResult(false);
        }

        var isEnabled = _configuration.GetValue<bool>($"FeatureFlags:{featureName}", true);
        return Task.FromResult(isEnabled);
    }

    public Task<IReadOnlyDictionary<string, bool>> GetAllFlagsAsync(CancellationToken cancellationToken = default)
    {
        var section = _configuration.GetSection("FeatureFlags");
        var flags = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        if (section.Exists())
        {
            foreach (var child in section.GetChildren())
            {
                if (bool.TryParse(child.Value, out var result))
                {
                    flags[child.Key] = result;
                }
            }
        }

        // Default standard feature set if section is absent or partial
        EnsureDefaultFlag(flags, "EnableAiProfiling", true);
        EnsureDefaultFlag(flags, "EnableAnalytics", true);
        EnsureDefaultFlag(flags, "EnableBulkCvUpload", true);
        EnsureDefaultFlag(flags, "EnableSmartMatch", true);
        EnsureDefaultFlag(flags, "EnableFullTextSearch", true);

        return Task.FromResult<IReadOnlyDictionary<string, bool>>(flags);
    }

    private static void EnsureDefaultFlag(Dictionary<string, bool> flags, string key, bool defaultValue)
    {
        if (!flags.ContainsKey(key))
        {
            flags[key] = defaultValue;
        }
    }
}
