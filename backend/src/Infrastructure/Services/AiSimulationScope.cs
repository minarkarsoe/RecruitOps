using RecruitOps.Application.Interfaces;

namespace RecruitOps.Infrastructure.Services;

/// <summary>Scoped implementation of <see cref="IAiSimulationScope"/> — one instance per request.</summary>
public class AiSimulationScope : IAiSimulationScope
{
    public bool IsSimulated { get; private set; }
    public string? ProviderName { get; private set; }

    public void MarkSimulated(string providerName)
    {
        IsSimulated = true;
        ProviderName = providerName;
    }
}
