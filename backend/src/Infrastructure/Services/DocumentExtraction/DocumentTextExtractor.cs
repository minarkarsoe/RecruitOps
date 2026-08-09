using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using RecruitOps.Application.DTOs;
using RecruitOps.Application.Interfaces;
using UglyToad.PdfPig;

namespace RecruitOps.Infrastructure.Services.DocumentExtraction;

public class DocumentTextExtractor : IDocumentTextExtractor
{
    private readonly IMyanmarScriptNormalizer _scriptNormalizer;
    private readonly ILogger<DocumentTextExtractor> _logger;

    private static readonly Regex EmailRegex = new(
        @"(?i)[a-z0-9._%+-]+@[a-z0-9.-]+\.[a-z]{2,}", RegexOptions.Compiled, TimeSpan.FromMilliseconds(500));

    private static readonly Regex PhoneRegex = new(
        @"(?:\+?95[-. ]?|0)?9[-. ]?(?:\d[-. ]?){7,9}\b", RegexOptions.Compiled, TimeSpan.FromMilliseconds(500));

    private static readonly Regex ExpYearsRegex = new(
        @"(\d{1,2})\+?\s*(?:years?|yrs?)(?:\s+of)?(?:\s+[a-zA-Z]+)*\s+(?:experience|exp)", RegexOptions.Compiled | RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(500));

    private static readonly string[] SkillKeywords = new[]
    {
        "C#", ".NET", "ASP.NET", "React", "TypeScript", "JavaScript", "SQL", "PostgreSQL",
        "Docker", "Kubernetes", "Python", "AWS", "Azure", "Git", "REST API", "GraphQL",
        "Agile", "Scrum", "CI/CD", "Figma", "HTML", "CSS", "Tailwind"
    };

    private static readonly HashSet<string> IgnoredCandidateNameHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Resume", "Curriculum Vitae", "CV",
        "Personal Details", "Personal Information", "Contact Info", "Contact Information",
        "Career Objective", "Objective", "Profile", "Personal Profile",
        "Professional Summary", "Summary", "Summary of Qualifications",
        "Work Experience", "Professional Experience", "Employment History", "Experience",
        "Education", "Academic Background",
        "Skills", "Technical Skills", "Key Skills", "Core Competencies",
        "Projects", "Certifications", "References", "Hobbies", "Languages"
    };

    public DocumentTextExtractor(
        IMyanmarScriptNormalizer scriptNormalizer,
        ILogger<DocumentTextExtractor> logger)
    {
        _scriptNormalizer = scriptNormalizer;
        _logger = logger;
    }

    public async Task<DocumentExtractionResult> ExtractTextAsync(
        Stream stream,
        string fileName,
        string contentType,
        CancellationToken ct = default)
    {
        MemoryStream ms;
        bool createdLocalMs = false;

        if (stream is MemoryStream existingMs)
        {
            ms = existingMs;
            if (ms.CanSeek) ms.Position = 0;
        }
        else
        {
            ms = new MemoryStream();
            createdLocalMs = true;
            if (stream.CanSeek) stream.Position = 0;
            await stream.CopyToAsync(ms, ct);
            ms.Position = 0;
        }

        try
        {
            string extension = Path.GetExtension(fileName).ToLowerInvariant();
            string rawText = string.Empty;

            switch (extension)
            {
                case ".pdf":
                    rawText = ExtractFromPdf(ms);
                    if (string.IsNullOrWhiteSpace(rawText))
                    {
                        _logger.LogInformation("PDF text stream empty; using scanned PDF fallback for {FileName}", fileName);
                        ms.Position = 0;
                        rawText = await ExtractFromImageOrScannedAsync(ms, fileName, ct);
                    }
                    break;

                case ".docx":
                    rawText = ExtractFromDocx(ms);
                    break;

                case ".png":
                case ".jpg":
                case ".jpeg":
                    rawText = await ExtractFromImageOrScannedAsync(ms, fileName, ct);
                    break;

                default:
                    _logger.LogWarning("Unsupported file extension {Extension} for text extraction", extension);
                    break;
            }

            // Myanmar Script Normalization (Zawgyi -> Unicode NFC)
            var normResult = _scriptNormalizer.Normalize(rawText);
            string normalizedText = normResult.NormalizedText;

            // Detected language heuristic
            string detectedLanguage = normResult.IsZawgyiDetected ? "my-Zawgyi"
                : normResult.DetectedEncoding == MyanmarEncoding.Unicode ? "my"
                : "en";

            // Contact info heuristic parsing
            var parsedInfo = ExtractContactInfo(normalizedText);

            return new DocumentExtractionResult(
                ExtractedText: normalizedText,
                OriginalText: rawText,
                DetectedLanguage: detectedLanguage,
                IsZawgyiNormalized: normResult.IsZawgyiDetected,
                ParsedContactInfo: parsedInfo
            );
        }
        finally
        {
            if (createdLocalMs)
            {
                ms.Dispose();
            }
        }
    }

    private string ExtractFromPdf(MemoryStream ms)
    {
        try
        {
            var builder = new StringBuilder();
            using var pdf = PdfDocument.Open(ms);
            foreach (var page in pdf.GetPages())
            {
                if (!string.IsNullOrWhiteSpace(page.Text))
                {
                    builder.AppendLine(page.Text);
                }
            }
            return builder.ToString().Trim();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse PDF text stream for document");
            return string.Empty;
        }
    }

    private string ExtractFromDocx(MemoryStream ms)
    {
        try
        {
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read, leaveOpen: true);
            var entry = zip.GetEntry("word/document.xml");
            if (entry is null)
            {
                return string.Empty;
            }

            using var entryStream = entry.Open();
            var xdoc = XDocument.Load(entryStream);
            XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

            var sb = new StringBuilder();
            foreach (var p in xdoc.Descendants(w + "p"))
            {
                var text = string.Concat(p.Descendants(w + "t").Select(t => t.Value));
                if (!string.IsNullOrWhiteSpace(text))
                {
                    sb.AppendLine(text);
                }
            }
            return sb.ToString().Trim();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse DOCX OpenXML document stream");
            return string.Empty;
        }
    }

    private async Task<string> ExtractFromImageOrScannedAsync(MemoryStream ms, string fileName, CancellationToken ct)
    {
        await Task.Yield();
        string extension = Path.GetExtension(fileName).TrimStart('.').ToUpperInvariant();
        if (string.IsNullOrEmpty(extension)) extension = "IMAGE";

        int? width = null;
        int? height = null;

        byte[] bytes = ms.ToArray();
        if (bytes.Length >= 24 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
        {
            width = (bytes[16] << 24) | (bytes[17] << 16) | (bytes[18] << 8) | bytes[19];
            height = (bytes[20] << 24) | (bytes[21] << 16) | (bytes[22] << 8) | bytes[23];
        }

        if (width.HasValue && height.HasValue)
        {
            return $"Image Document: {fileName} | Format: {extension} | Dimensions: {width}x{height} | Size: {ms.Length} bytes";
        }

        return $"Image Document: {fileName} | Format: {extension} | Size: {ms.Length} bytes";
    }

    public static ParsedContactInfoDto ExtractContactInfo(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new ParsedContactInfoDto(null, null, null, null, new List<string>());
        }

        string? email = null;
        try
        {
            var emailMatch = EmailRegex.Match(text);
            if (emailMatch.Success) email = emailMatch.Value;
        }
        catch (RegexMatchTimeoutException) { }

        string? phone = null;
        try
        {
            var phoneMatch = PhoneRegex.Match(text);
            if (phoneMatch.Success) phone = phoneMatch.Value.Trim();
        }
        catch (RegexMatchTimeoutException) { }

        int? yearsOfExperience = null;
        try
        {
            var expMatch = ExpYearsRegex.Match(text);
            if (expMatch.Success && int.TryParse(expMatch.Groups[1].Value, out int yrs))
            {
                yearsOfExperience = yrs;
            }
        }
        catch (RegexMatchTimeoutException) { }

        // Candidate Name heuristic (first clean line that isn't a header)
        string? candidateName = null;
        var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.Length > 2 && trimmed.Length < 60 &&
                !trimmed.Equals("Resume", StringComparison.OrdinalIgnoreCase) &&
                !trimmed.Equals("Curriculum Vitae", StringComparison.OrdinalIgnoreCase) &&
                !trimmed.Equals("CV", StringComparison.OrdinalIgnoreCase) &&
                !trimmed.EndsWith(":") &&
                !trimmed.Contains("@") &&
                !trimmed.StartsWith("http", StringComparison.OrdinalIgnoreCase) &&
                !trimmed.StartsWith("Email", StringComparison.OrdinalIgnoreCase) &&
                !trimmed.StartsWith("Phone", StringComparison.OrdinalIgnoreCase) &&
                !trimmed.StartsWith("Tel", StringComparison.OrdinalIgnoreCase))
            {
                candidateName = trimmed;
                break;
            }
        }

        // Skills keyword match
        var foundSkills = new List<string>();
        foreach (var k in SkillKeywords)
        {
            try
            {
                string pattern = $@"(?<=^|\W){Regex.Escape(k)}(?=$|\W)";
                if (Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(200)))
                {
                    foundSkills.Add(k);
                }
            }
            catch (RegexMatchTimeoutException) { }
        }

        return new ParsedContactInfoDto(
            CandidateName: candidateName,
            Email: email,
            Phone: phone,
            YearsOfExperience: yearsOfExperience,
            Skills: foundSkills.Distinct().ToList()
        );
    }
}

