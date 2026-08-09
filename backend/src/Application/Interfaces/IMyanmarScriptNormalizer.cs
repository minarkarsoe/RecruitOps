namespace RecruitOps.Application.Interfaces;

public enum MyanmarEncoding
{
    NonMyanmar = 0,
    Unicode = 1,
    Zawgyi = 2
}

public record MyanmarScriptNormalizationResult(
    string NormalizedText,
    string OriginalText,
    bool IsZawgyiDetected,
    double ConfidenceScore,
    MyanmarEncoding DetectedEncoding
)
{
    public static implicit operator string(MyanmarScriptNormalizationResult result) => result.NormalizedText;
}

public interface IMyanmarScriptNormalizer
{
    /// <summary>
    /// Normalizes Myanmar text to canonical Unicode (FormC).
    /// If Zawgyi encoding is detected, converts it to Unicode prior to normalization.
    /// </summary>
    /// <param name="input">The raw text string to normalize.</param>
    /// <returns>A result object containing normalized text and detection metadata.</returns>
    MyanmarScriptNormalizationResult Normalize(string? input);

    /// <summary>
    /// Fast-path detection to check if a string contains Zawgyi encoding.
    /// </summary>
    bool IsZawgyi(string? input);
}
