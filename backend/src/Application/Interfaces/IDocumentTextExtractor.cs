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
    /// Extracts text from a document stream (PDF, DOCX), normalizes Zawgyi script if detected,
    /// and parses contact information heuristics.
    ///
    /// <para>⚠️ <b><see cref="DocumentExtractionResult.ExtractedText"/> may be empty, and empty
    /// means "not read", not "read, found nothing".</b> There is no OCR in this build, so a scan
    /// or a photo yields nothing — callers must treat that as a file to skip rather than as a
    /// candidate with no details. Both upload paths reject images outright; a scanned PDF is the
    /// case that still reaches here, because it is indistinguishable from a text PDF until its
    /// stream comes back empty. Do not reintroduce a placeholder string for this case: it was
    /// what allowed a fabricated "Image Document: …" to be stored as a CV and indexed by search.
    /// </para>
    /// </summary>
    Task<DocumentExtractionResult> ExtractTextAsync(
        Stream stream,
        string fileName,
        string contentType,
        CancellationToken ct = default);
}
