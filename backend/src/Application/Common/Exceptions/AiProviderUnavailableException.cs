namespace RecruitOps.Application.Common.Exceptions;

/// <summary>
/// An AI provider was configured and called, but did not return a usable answer — a non-success
/// status, a network fault, a timeout, or a body that could not be read as the expected shape.
/// </summary>
/// <remarks>
/// This exists so those cases surface as 502 instead of a fabricated sample. Returning a canned
/// profile when the provider is down reads to a recruiter exactly like a real analysis, and the
/// parsed profile it produces is written to the candidate record on confirmation.
/// </remarks>
public class AiProviderUnavailableException : Exception
{
    public string ProviderName { get; }

    public AiProviderUnavailableException(string providerName, string reason, Exception? inner = null)
        : base($"AI provider '{providerName}' did not return a usable response: {reason}", inner)
    {
        ProviderName = providerName;
    }
}
