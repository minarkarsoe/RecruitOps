namespace RecruitOps.Application.Common.Exceptions;

public class AiApiKeyMissingException : Exception
{
    public string ProviderName { get; }

    public AiApiKeyMissingException(string providerName)
        : base($"API key for AI provider '{providerName}' is not configured.")
    {
        ProviderName = providerName;
    }
}
