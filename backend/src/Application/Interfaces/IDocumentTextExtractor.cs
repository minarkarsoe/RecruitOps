using RecruitOps.Application.DTOs;

namespace RecruitOps.Application.Interfaces;

public record DocumentExtractionResult(
    string ExtractedText,
    string OriginalText,
    string DetectedLanguage,
    bool IsZawgyiNormalized,
    ParsedContactInfoDto ParsedContactInfo
);

public interface IDocumentTextExtractor
{
    /// <summary>
    /// Extracts text from a document stream (PDF, DOCX, PNG, JPG), normalizes Zawgyi script if detected,
    /// and parses contact information heuristics.
    /// </summary>
    Task<DocumentExtractionResult> ExtractTextAsync(
        Stream stream,
        string fileName,
        string contentType,
        CancellationToken ct = default);
}
