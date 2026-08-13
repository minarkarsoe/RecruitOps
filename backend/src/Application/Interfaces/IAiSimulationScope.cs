namespace RecruitOps.Application.Interfaces;

/// <summary>
/// Per-request record of whether an AI answer was fabricated locally rather than produced by a
/// provider. Set by the provider clients when they serve a development stub; read by the API layer,
/// which stamps <c>X-Ai-Simulated: true</c> on the response so no caller can mistake sample data
/// for a real analysis.
/// </summary>
public interface IAiSimulationScope
{
    bool IsSimulated { get; }

    /// <summary>Name of the provider that was stubbed, or null when the answer is genuine.</summary>
    string? ProviderName { get; }

    void MarkSimulated(string providerName);
}
