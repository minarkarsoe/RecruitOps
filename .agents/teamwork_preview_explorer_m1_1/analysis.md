# Technical Specification & Architectural Design
## Milestone 1: CV Resume Storage & Document Extraction Backend API

**Author:** teamwork_preview_explorer  
**Working Directory:** `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_explorer_m1_1`  
**Target Domain:** RecruitOps Monolith — Module 2 / CV Upload & Ingestion  
**Date:** 2026-08-07  

---

## 1. Executive Summary & Architectural Overview

Milestone 1 introduces the backend API and domain/infrastructure services required for candidate resume CV storage, text extraction, script normalization, and structured contact information extraction.

### Core Architectural Principles & Compliance
1. **Clean Architecture Separation**:
   - **Domain Layer (`RecruitOps.Domain`)**: `JobApplication` entity extended with resume metadata (`ResumeFileKey`, `ResumeFileName`, `ResumeExtractedText`, `ResumeUploadedAt`).
   - **Application Layer (`RecruitOps.Application`)**: Defines `IDocumentTextExtractor` and `IResumeService` interfaces, as well as `ResumeExtractionResultDto` and `ParsedContactInfoDto`.
   - **Infrastructure Layer (`RecruitOps.Infrastructure`)**: Implements `DocumentTextExtractor` and `ResumeService`, integrating `IFileStorage` (S3/MinIO), `IMyanmarScriptNormalizer` (Zawgyi to Unicode), and OpenXML/PDF stream parsing libraries.
   - **API Layer (`RecruitOps.Api`)**: Exposes REST endpoints on `ApplicationsController`.

2. **ADR Alignment**:
   - **ADR-0008 (Document Extraction & Profiling)**: Mandatory local in-process text extraction (PDF, DOCX, Image OCR fallback). Permissively licensed dependencies only (MIT / Apache-2.0). Recruiter human-review gate preserved.
   - **ADR-0009 (Myanmar Script Handling)**: Ingested document text automatically normalized using `IMyanmarScriptNormalizer` to canonical Unicode NFC.
   - **ADR-0013 (Infrastructure & Storage)**: CV files stored via `IFileStorage` abstraction backing Cloudflare R2 or local MinIO.
   - **ADR-0003 & ADR-0018 (Department Scoping & Candidate Privacy)**: All resume operations enforce department scoping (`IDepartmentAccess`) and candidate data exclusion (`ICurrentUser.IsExcludedFromCandidateData`).

---

## 2. Codebase Inspection Findings

### 2.1 Existing Controller Inspection (`ApplicationsController.cs`)
- Located at `backend/src/Api/Controllers/ApplicationsController.cs`.
- Decorated with `[ApiController]`, `[Route("api/applications")]`, and `[Authorize(Policy = Policies.InternalUser)]`.
- Currently contains:
  - `POST /api/applications/{id}/stage`: Moves an application stage using `IPipelineService`.
  - `GET /api/applications/{id}/history`: Returns application stage history.
- **Milestone 1 Additions**:
  - `POST /api/applications/{id}/resume`: Upload single CV file, perform extraction, normalize script, store in object storage, return DTO.
  - `GET /api/applications/{id}/resume`: Download/view candidate CV stream or presigned URL.

### 2.2 Infrastructure Dependency Injection Inspection (`DependencyInjection.cs`)
- Located at `backend/src/Infrastructure/DependencyInjection.cs`.
- **Existing Registrations**:
  - `IFileStorage` registered as Scoped: `services.AddScoped<IFileStorage, S3FileStorage>();`
  - `IMyanmarScriptNormalizer` registered as Singleton: `services.AddSingleton<IMyanmarScriptNormalizer, MyanmarScriptNormalizer>();`
- **Milestone 1 Registrations to Add**:
  ```csharp
  // Document Text Extractor & Resume Management
  services.AddScoped<IDocumentTextExtractor, DocumentTextExtractor>();
  services.AddScoped<IResumeService, ResumeService>();
  ```

---

## 3. Detailed Interface & DTO Design

### 3.1 DTO Definitions (`backend/src/Application/DTOs/ResumeExtractionDtos.cs`)

```csharp
namespace RecruitOps.Application.DTOs;

/// <summary>
/// Structured contact and profile information parsed via heuristics/regex from extracted CV text.
/// </summary>
public record ParsedContactInfoDto(
    string? CandidateName,
    string? Email,
    string? Phone,
    int? YearsOfExperience,
    List<string> Skills
);

/// <summary>
/// Response DTO returned after processing a candidate resume upload.
/// </summary>
public record ResumeExtractionResultDto(
    Guid ApplicationId,
    string FileKey,
    string FileName,
    long FileSizeBytes,
    string ExtractedText,
    string OriginalText,
    string DetectedLanguage,
    bool IsZawgyiNormalized,
    ParsedContactInfoDto ParsedContactInfo,
    DateTimeOffset ProcessedAt
);
```

### 3.2 Extractor Interface (`backend/src/Application/Interfaces/IDocumentTextExtractor.cs`)

```csharp
namespace RecruitOps.Application.Interfaces;

using RecruitOps.Application.DTOs;

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
    /// Extracts text from a document stream (PDF, DOCX, PNG, JPG), normalizes Zawgyi script,
    /// and parses contact heuristics.
    /// </summary>
    Task<DocumentExtractionResult> ExtractTextAsync(
        Stream stream,
        string fileName,
        string contentType,
        CancellationToken ct = default);
}
```

### 3.3 Resume Service Interface (`backend/src/Application/Interfaces/IResumeService.cs`)

```csharp
namespace RecruitOps.Application.Interfaces;

using Microsoft.AspNetCore.Http;
using RecruitOps.Application.DTOs;

public interface IResumeService
{
    /// <summary>
    /// Validates, uploads, extracts text, and stores a candidate resume for an application.
    /// Returns null if the application is not found or department scoping fails.
    /// </summary>
    Task<ResumeExtractionResultDto?> UploadAndExtractResumeAsync(
        Guid applicationId,
        IFormFile file,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves stored resume file metadata and stream for streaming download.
    /// Returns null if not found or unauthorized.
    /// </summary>
    Task<(Stream Stream, string ContentType, string FileName)?> GetResumeFileAsync(
        Guid applicationId,
        CancellationToken ct = default);
}
```

---

## 4. Concrete Document Text Extractor Implementation

Located at `backend/src/Infrastructure/Services/DocumentExtraction/DocumentTextExtractor.cs`.

### Extractor Capabilities
1. **PDF Text Stream Parsing**:
   - Uses `UglyToad.PdfPig` (Apache-2.0) or `PdfSharpCore` (MIT) to extract digital text streams.
   - Fallback: If extracted text is empty or whitespace-only (scanned PDF), routes page images to OCR engine.
2. **DOCX OpenXML Parsing**:
   - Uses `System.IO.Compression.ZipArchive` to read `word/document.xml` or `DocumentFormat.OpenXml` (MIT).
   - Iterates through `<w:p>` paragraph nodes and concatenates `<w:t>` text runs.
3. **Image / Scanned OCR Fallback**:
   - Handles `.png`, `.jpg`, `.jpeg` and scanned PDFs.
   - Local OCR using Tesseract / TesseractEngine wrapper with fallback text extraction.
4. **Zawgyi Myanmar Script Normalization**:
   - Passes raw extracted text through `IMyanmarScriptNormalizer.Normalize(rawText)`.
   - Populates `IsZawgyiNormalized = normalizationResult.IsZawgyiDetected`.
5. **Contact Info & Profile Heuristics**:
   - **Email**: Regex `(?i)[a-z0-9._%+-]+@[a-z0-9.-]+\.[a-z]{2,}`
   - **Phone**: Regex `(?:\+?95|0)?9\d{7,9}|(?:\+?\d{1,3}[-.\s]?)?\(?\d{2,4}\)?[-.\s]?\d{3,4}[-.\s]?\d{3,4}`
   - **Candidate Name**: Filters common resume headings ("Curriculum Vitae", "Resume", "CV"), picks the first candidate title line.
   - **Experience Years**: Regex `(\d{1,2})\+?\s*(?:years?|yrs?)(?:\s+of)?\s+(?:experience|exp)`
   - **Skills**: Match against a predefined technical dictionary (`C#`, `.NET`, `React`, `TypeScript`, `SQL`, `PostgreSQL`, `Docker`, `Python`, `AWS`, `Azure`, `Git`, `REST API`, `Agile`, `Scrum`, `Figma`, etc.).

### Implementation Snippet

```csharp
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
        @"(?i)[a-z0-9._%+-]+@[a-z0-9.-]+\.[a-z]{2,}", RegexOptions.Compiled);

    private static readonly Regex PhoneRegex = new(
        @"(?:\+?95|0)?9\d{7,9}|(?:\+?\d{1,3}[-.\s]?)?\(?\d{2,4}\)?[-.\s]?\d{3,4}[-.\s]?\d{3,4}", RegexOptions.Compiled);

    private static readonly Regex ExpYearsRegex = new(
        @"(\d{1,2})\+?\s*(?:years?|yrs?)(?:\s+of)?\s+(?:experience|exp)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly string[] SkillKeywords = new[]
    {
        "C#", ".NET", "ASP.NET", "React", "TypeScript", "JavaScript", "SQL", "PostgreSQL",
        "Docker", "Kubernetes", "Python", "AWS", "Azure", "Git", "REST API", "GraphQL",
        "Agile", "Scrum", "CI/CD", "Figma", "HTML", "CSS", "Tailwind"
    };

    public DocumentTextExtractor(
        IMyanmarScriptNormalizer scriptNormalizer,
        ILogger<DocumentTextExtractor> logger)
    {
        _scriptNormalizer = scriptNormalizer;
        _logger = logger;
    }

    public async Task<DocumentExtractionResult> ExtractTextAsync(
        Stream stream, string fileName, string contentType, CancellationToken ct = default)
    {
        string extension = Path.GetExtension(fileName).ToLowerInvariant();
        string rawText = string.Empty;

        stream.Position = 0;

        switch (extension)
        {
            case ".pdf":
                rawText = ExtractFromPdf(stream);
                if (string.IsNullOrWhiteSpace(rawText))
                {
                    _logger.LogInformation("PDF digital text stream empty; attempting OCR fallback for {FileName}", fileName);
                    rawText = await ExtractFromImageOrScannedAsync(stream, fileName, ct);
                }
                break;

            case ".docx":
                rawText = ExtractFromDocx(stream);
                break;

            case ".png":
            case ".jpg":
            case ".jpeg":
                rawText = await ExtractFromImageOrScannedAsync(stream, fileName, ct);
                break;

            default:
                _logger.LogWarning("Unsupported extension {Extension} for text extraction", extension);
                break;
        }

        // Script Normalization (Zawgyi -> Unicode NFC)
        var normResult = _scriptNormalizer.Normalize(rawText);
        string normalizedText = normResult.NormalizedText;

        // Language detection heuristic
        string detectedLanguage = normResult.IsZawgyiDetected ? "my-Zawgyi"
            : normResult.DetectedEncoding == MyanmarEncoding.Unicode ? "my"
            : "en";

        // Extract contact info heuristics
        var parsedInfo = ExtractContactInfo(normalizedText);

        return new DocumentExtractionResult(
            ExtractedText: normalizedText,
            OriginalText: rawText,
            DetectedLanguage: detectedLanguage,
            IsZawgyiNormalized: normResult.IsZawgyiDetected,
            ParsedContactInfo: parsedInfo
        );
    }

    private static string ExtractFromPdf(Stream stream)
    {
        try
        {
            var builder = new StringBuilder();
            using var pdf = PdfDocument.Open(stream);
            foreach (var page in pdf.GetPages())
            {
                builder.AppendLine(page.Text);
            }
            return builder.ToString();
        }
        catch (Exception ex)
        {
            // Log warning & return empty string to trigger OCR fallback
            return string.Empty;
        }
    }

    private static string ExtractFromDocx(Stream stream)
    {
        try
        {
            using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            var entry = zip.GetEntry("word/document.xml");
            if (entry is null) return string.Empty;

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
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return string.Empty;
        }
    }

    private async Task<string> ExtractFromImageOrScannedAsync(Stream stream, string fileName, CancellationToken ct)
    {
        // OCR Engine local fallback / stub
        await Task.Yield();
        return $"[OCR Extracted Text for {fileName}]";
    }

    private static ParsedContactInfoDto ExtractContactInfo(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new ParsedContactInfoDto(null, null, null, null, new List<string>());
        }

        // Email
        var emailMatch = EmailRegex.Match(text);
        string? email = emailMatch.Success ? emailMatch.Value : null;

        // Phone
        var phoneMatch = PhoneRegex.Match(text);
        string? phone = phoneMatch.Success ? phoneMatch.Value : null;

        // Experience Years
        var expMatch = ExpYearsRegex.Match(text);
        int? yearsOfExperience = expMatch.Success && int.TryParse(expMatch.Groups[1].Value, out int yrs) ? yrs : null;

        // Candidate Name heuristic (first clean line that isn't a header)
        string? candidateName = null;
        var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.Length > 2 && trimmed.Length < 50 &&
                !trimmed.Equals("Resume", StringComparison.OrdinalIgnoreCase) &&
                !trimmed.Equals("Curriculum Vitae", StringComparison.OrdinalIgnoreCase) &&
                !trimmed.Equals("CV", StringComparison.OrdinalIgnoreCase) &&
                !trimmed.Contains("@"))
            {
                candidateName = trimmed;
                break;
            }
        }

        // Skills keyword match
        var foundSkills = SkillKeywords
            .Where(k => Regex.IsMatch(text, $@"\b{Regex.Escape(k)}\b", RegexOptions.IgnoreCase))
            .Distinct()
            .ToList();

        return new ParsedContactInfoDto(
            CandidateName: candidateName,
            Email: email,
            Phone: phone,
            YearsOfExperience: yearsOfExperience,
            Skills: foundSkills
        );
    }
}
```

---

## 5. Domain & Database Model Extensions

### 5.1 Entity Extension (`JobApplication.cs`)

Add the following properties to `backend/src/Domain/Entities/JobApplication.cs`:

```csharp
public string? ResumeFileKey { get; set; }
public string? ResumeFileName { get; set; }
public string? ResumeExtractedText { get; set; }
public DateTimeOffset? ResumeUploadedAt { get; set; }
```

---

## 6. Endpoints & Controller Implementation

### 6.1 `ApplicationsController.cs` Endpoint Specs

#### Endpoint 1: Single CV Upload & Ingest
- **Route**: `POST /api/applications/{id:guid}/resume`
- **Authorization**: `[Authorize(Policy = Policies.InternalUser)]`
- **Request Form**: `IFormFile file` (Single multipart file, max 10MB).
- **Supported File Types**: `.pdf`, `.docx`, `.png`, `.jpg`, `.jpeg`.
- **Validation**:
  - File presence check: 400 Bad Request if file missing or zero length.
  - File size check: 400 Bad Request if file size > 10,485,760 bytes (10MB).
  - Extension check: 400 Bad Request if extension not in allowed list.
- **Security & Access Control**:
  - Application existence check.
  - Department scoping check via `IDepartmentAccess` and `ICurrentUser.IsExcludedFromCandidateData`.
- **Response**: `200 OK` with `ResumeExtractionResultDto`.

#### Endpoint 2: Resume Download / Stream
- **Route**: `GET /api/applications/{id:guid}/resume`
- **Authorization**: `[Authorize(Policy = Policies.InternalUser)]`
- **Security & Access Control**: Department scoping & candidate exclusion verification.
- **Response**: `200 OK` `File(stream, contentType, fileName)` or 404 Not Found if missing.

### 6.2 Implementation Snippet for `ApplicationsController.cs`

```csharp
[HttpPost("{id:guid}/resume")]
[Consumes("multipart/form-data")]
public async Task<ActionResult<ResumeExtractionResultDto>> UploadResume(
    Guid id, IFormFile file, CancellationToken ct)
{
    if (file is null || file.Length == 0)
    {
        return BadRequest(new ProblemDetails { Title = "Invalid File", Detail = "File is empty or missing." });
    }

    if (file.Length > 10 * 1024 * 1024) // 10MB
    {
        return BadRequest(new ProblemDetails { Title = "File Too Large", Detail = "File size exceeds maximum limit of 10MB." });
    }

    var allowedExtensions = new[] { ".pdf", ".docx", ".png", ".jpg", ".jpeg" };
    var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
    if (!allowedExtensions.Contains(ext))
    {
        return BadRequest(new ProblemDetails { Title = "Unsupported Format", Detail = "Allowed formats are PDF, DOCX, PNG, JPG, JPEG." });
    }

    var result = await _resumeService.UploadAndExtractResumeAsync(id, file, ct);
    if (result is null)
    {
        return NotFound(new ProblemDetails { Title = "Not Found", Detail = "Application not found or unauthorized." });
    }

    return Ok(result);
}

[HttpGet("{id:guid}/resume")]
public async Task<IActionResult> GetResume(Guid id, CancellationToken ct)
{
    var fileResult = await _resumeService.GetResumeFileAsync(id, ct);
    if (fileResult is null)
    {
        return NotFound(new ProblemDetails { Title = "Not Found", Detail = "Resume not found or unauthorized." });
    }

    return File(fileResult.Value.Stream, fileResult.Value.ContentType, fileResult.Value.FileName);
}
```

---

## 7. Testing & Verification Strategy

### 7.1 Unit & Integration Test Design (`backend/tests/RecruitOps.Api.Tests/ResumeExtractionTests.cs`)

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using RecruitOps.Application.DTOs;
using Xunit;

namespace RecruitOps.Api.Tests;

public class ResumeExtractionTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;

    public ResumeExtractionTests(CustomWebAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task UploadResume_FileExceeding10MB_Returns400BadRequest()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.SchemeName, TestAuthHandler.AdminSubject);

        var content = new MultipartFormDataContent();
        var bytes = new byte[11 * 1024 * 1024]; // 11MB
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", "large_resume.pdf");

        var response = await client.PostAsync($"/api/applications/{Guid.NewGuid()}/resume", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadResume_InvalidFileExtension_Returns400BadRequest()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.SchemeName, TestAuthHandler.AdminSubject);

        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(new byte[] { 0x01, 0x02 });
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(fileContent, "file", "malicious.exe");

        var response = await client.PostAsync($"/api/applications/{Guid.NewGuid()}/resume", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadResume_NonExistentApplication_Returns404NotFound()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.SchemeName, TestAuthHandler.AdminSubject);

        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes("Test PDF content"));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", "sample.pdf");

        var response = await client.PostAsync($"/api/applications/{Guid.NewGuid()}/resume", content);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
```

---

## 8. Implementation Task Checklist

1. [ ] **Domain**: Extend `JobApplication` with `ResumeFileKey`, `ResumeFileName`, `ResumeExtractedText`, `ResumeUploadedAt`.
2. [ ] **Application Layer**:
   - Add `ParsedContactInfoDto` & `ResumeExtractionResultDto` in `backend/src/Application/DTOs/ResumeExtractionDtos.cs`.
   - Add `IDocumentTextExtractor.cs` in `backend/src/Application/Interfaces/IDocumentTextExtractor.cs`.
   - Add `IResumeService.cs` in `backend/src/Application/Interfaces/IResumeService.cs`.
3. [ ] **Infrastructure Layer**:
   - Add `DocumentTextExtractor.cs` under `backend/src/Infrastructure/Services/DocumentExtraction/`.
   - Add `ResumeService.cs` under `backend/src/Infrastructure/Services/`.
   - Register `IDocumentTextExtractor` & `IResumeService` in `DependencyInjection.cs`.
4. [ ] **Api Layer**:
   - Inject `IResumeService` in `ApplicationsController.cs`.
   - Add `POST /api/applications/{id}/resume` endpoint.
   - Add `GET /api/applications/{id}/resume` endpoint.
5. [ ] **Testing**:
   - Add `ResumeExtractionTests.cs` in `backend/tests/RecruitOps.Api.Tests/`.
   - Run `dotnet test backend/RecruitOps.sln` to ensure all existing and new tests pass cleanly.
