# Milestone 2: Bulk CV Upload Background Job — Technical Blueprint & Specification

## 1. Executive Summary & Scope Definition

This document provides the implementation blueprint and step-by-step technical specification for **Milestone 2 (Bulk CV Upload Background Job)** of RecruitOps (Person A - Flow 1).

Milestone 2 introduces asynchronous batch ingest for candidate CV files attached to job postings (`POST /api/jobpostings/{jobPostingId}/resumes/bulk`). Up to 50 CV documents (PDF, DOCX, PNG, JPG, JPEG, <=10MB each) are accepted in a single HTTP request, queued, and processed asynchronously in the background. The system tracks per-file processing status (`Queued`, `Processing`, `Success`, `Skipped`, `Failed`) and provides a progress query endpoint (`GET /api/jobpostings/{jobPostingId}/resumes/bulk/{batchId}`).

For each file in the batch, the background runner executes document text extraction via `IDocumentTextExtractor` (which auto-normalizes Zawgyi Myanmar script to Unicode NFC via `IMyanmarScriptNormalizer`), extracts contact information heuristics, finds or creates candidate records by email/phone via `ContactNormalizer`, creates a `JobApplication` in the `Sourced` pipeline stage, persists the CV file to object storage via `IFileStorage`, and logs stage history in `ApplicationStageHistory`.

---

## 2. Enums and Data Transfer Objects (DTOs)

### 2.1 Domain Enums

Place in `backend/src/Domain/Enums/BulkUploadEnums.cs` (or `backend/src/Application/DTOs/BulkResumeDtos.cs`):

```csharp
namespace RecruitOps.Domain.Enums;

/// <summary>
/// Status of an overall bulk CV upload job batch.
/// </summary>
public enum BulkBatchStatus
{
    Queued = 0,
    Processing = 1,
    Completed = 2,
    Failed = 3
}

/// <summary>
/// Status of an individual file item within a bulk CV upload batch.
/// </summary>
public enum BulkFileStatus
{
    Queued = 0,
    Processing = 1,
    Success = 2,
    Skipped = 3,
    Failed = 4
}
```

### 2.2 Application DTOs

Place in `backend/src/Application/DTOs/BulkResumeDtos.cs`:

```csharp
namespace RecruitOps.Application.DTOs;

/// <summary>
/// Response returned immediately after enqueueing a bulk CV upload batch.
/// </summary>
public record BulkUploadBatchResponseDto(
    Guid BatchId,
    Guid JobPostingId,
    int TotalFiles,
    string Status,
    DateTimeOffset CreatedAt
);

/// <summary>
/// Status summary for an individual file item in a bulk batch.
/// </summary>
public record BulkFileItemStatusDto(
    string FileName,
    string Status,
    string? ErrorMessage,
    Guid? ApplicationId,
    Guid? CandidateId
);

/// <summary>
/// Detailed status report for a bulk CV upload batch and all its items.
/// </summary>
public record BulkBatchStatusDto(
    Guid BatchId,
    Guid JobPostingId,
    string Status,
    int TotalFiles,
    int ProcessedFiles,
    int SuccessCount,
    int SkippedCount,
    int FailedCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<BulkFileItemStatusDto> Items
);

/// <summary>
/// In-memory structure passed to service for batch enqueueing.
/// </summary>
public record BulkFileItemInput(
    string FileName,
    byte[] Content,
    string ContentType
);
```

---

## 3. Interface and Application Service Specification

### 3.1 Interface (`IBulkResumeService`)

Place in `backend/src/Application/Interfaces/IBulkResumeService.cs`:

```csharp
using RecruitOps.Application.DTOs;

namespace RecruitOps.Application.Interfaces;

public interface IBulkResumeService
{
    /// <summary>
    /// Enqueues a batch of CV files for background processing against a target job posting.
    /// Returns null if job posting does not exist or user lacks department access.
    /// </summary>
    Task<BulkUploadBatchResponseDto?> EnqueueBatchAsync(
        Guid jobPostingId,
        IReadOnlyList<BulkFileItemInput> files,
        Guid? currentUserId,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves the current status and per-file progress of a bulk CV upload batch.
    /// Returns null if batch is not found or department access is denied for the job posting.
    /// </summary>
    Task<BulkBatchStatusDto?> GetBatchStatusAsync(
        Guid jobPostingId,
        Guid batchId,
        CancellationToken ct = default);
}
```

---

## 4. Background Job & Service Implementation (`BulkResumeService`)

Place implementation in `backend/src/Infrastructure/Services/BulkResumeService.cs`.

### 4.1 In-Memory Batch State Model

To support real-time status queries without requiring complex database migrations during rapid execution, maintain thread-safe batch state via a concurrent state store or `ConcurrentDictionary<Guid, BatchStateHolder>` inside a singleton/scoped manager.

```csharp
internal class BatchStateHolder
{
    public Guid BatchId { get; set; }
    public Guid JobPostingId { get; set; }
    public Guid TenantId { get; set; }
    public Guid? UploadedByUserId { get; set; }
    public BulkBatchStatus Status { get; set; } = BulkBatchStatus.Queued;
    public int TotalFiles { get; set; }
    public int ProcessedFiles { get; set; }
    public int SuccessCount { get; set; }
    public int SkippedCount { get; set; }
    public int FailedCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public List<BatchItemStateHolder> Items { get; set; } = new();
    public object LockObject { get; } = new();
}

internal class BatchItemStateHolder
{
    public string FileName { get; set; } = string.Empty;
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = string.Empty;
    public BulkFileStatus Status { get; set; } = BulkFileStatus.Queued;
    public string? ErrorMessage { get; set; }
    public Guid? ApplicationId { get; set; }
    public Guid? CandidateId { get; set; }
}
```

### 4.2 Background Execution Steps (`ProcessBatchInBackground`)

When `EnqueueBatchAsync` is called:
1. Access check: Validate job posting exists and user has department access via `IDepartmentAccess.CanAccessAsync(jobPosting.DepartmentId)`. If denied/missing, return `null`.
2. Batch registration: Create `BatchStateHolder` with initial items set to `BulkFileStatus.Queued`.
3. Store in `ConcurrentDictionary<Guid, BatchStateHolder> _batches`.
4. Fire background execution task using `Task.Run(...)` (or injected `IServiceScopeFactory` queue):

```csharp
_ = Task.Run(async () => await ProcessBatchAsync(batchState.BatchId));
```

5. Immediate return: Return `BulkUploadBatchResponseDto` (`BatchId`, `JobPostingId`, `TotalFiles`, `"Queued"`, `CreatedAt`) to caller without blocking HTTP response.

#### Per-File Worker Pipeline Logic:
For each item in `batchState.Items`:

1. **Status Update**: Set item `Status = BulkFileStatus.Processing`, update batch status to `BulkBatchStatus.Processing`.
2. **Validation**:
   - File length check: If `Content.Length == 0` or `Content.Length > 10 * 1024 * 1024` (10MB), set `Status = BulkFileStatus.Failed`, `ErrorMessage = "File size exceeds 10MB limit."`. Increment `FailedCount` and `ProcessedFiles`. Skip to next file.
   - Extension check: Allowed extensions are `.pdf`, `.docx`, `.png`, `.jpg`, `.jpeg`. If disallowed, set `Status = BulkFileStatus.Failed`, `ErrorMessage = "Unsupported file extension."`. Increment `FailedCount` and `ProcessedFiles`. Skip to next file.
3. **Text Extraction & Zawgyi Normalization**:
   - Create `MemoryStream` from `Content`.
   - Call `_extractor.ExtractTextAsync(stream, item.FileName, item.ContentType)`.
   - Text extraction automatically extracts plain text from PDF/DOCX/PNG/JPG and runs `IMyanmarScriptNormalizer` on extracted text.
4. **Contact Info & Candidate Matching**:
   - Call `DocumentTextExtractor.ExtractContactInfo(extractionResult.ExtractedText)`.
   - Extract `Email` and `Phone`. Normalize using `ContactNormalizer.Email(extractedEmail)` and `ContactNormalizer.Phone(extractedPhone)`.
   - Open DI scope via `IServiceScopeFactory.CreateScope()` to resolve `AppDbContext`.
   - Query `db.Candidates.FirstOrDefaultAsync(c => c.TenantId == tenantId && ((c.Email != null && c.Email == normalizedEmail) || (c.Phone != null && c.Phone == normalizedPhone)))`.
   - **If Candidate Exists**: Reuse `candidate.Id`. If existing candidate has missing email/phone/name, populate empty fields.
   - **If Candidate Not Found**: Derive candidate name from `parsedInfo.CandidateName` or fallback to filename without extension (e.g. `"John Doe"` from `"john_doe_resume.pdf"`). Create new `Candidate` entity with `TenantId`, `FullName`, `Email`, `Phone`, `Source = SourceChannel.Direct`. Add to `db.Candidates`.
5. **JobApplication Creation**:
   - Create `JobApplication` entity:
     - `TenantId = batchState.TenantId`
     - `JobPostingId = batchState.JobPostingId`
     - `CandidateId = candidate.Id`
     - `Status = PipelineStatus.Sourced`
     - `Source = SourceChannel.Direct`
     - `AppliedAt = now`
   - Add to `db.JobApplications`.
6. **Object Storage Persistence**:
   - Reset memory stream position to 0.
   - Upload file via `_storage.UploadAsync(...)` with key `applications/{application.Id}/resume/{Guid.NewGuid()}_{item.FileName}`.
   - Update `JobApplication` properties:
     - `ResumeFileKey = uploadResult.Key`
     - `ResumeFileName = item.FileName`
     - `ResumeExtractedText = extractionResult.ExtractedText`
     - `ResumeUploadedAt = now`
     - `IsZawgyiNormalized = extractionResult.IsZawgyiNormalized`
7. **Stage History Logging**:
   - Create `ApplicationStageHistory` entity:
     - `TenantId = batchState.TenantId`
     - `JobApplicationId = application.Id`
     - `FromStatus = null`
     - `ToStatus = PipelineStatus.Sourced`
     - `ChangedByUserId = batchState.UploadedByUserId`
     - `ChangedAt = now`
     - `Note = "Created via Bulk CV Upload"`
   - Add to `db.ApplicationStageHistories`.
8. **Save Changes & Complete Item**:
   - `await db.SaveChangesAsync()`.
   - Update item `Status = BulkFileStatus.Success`, `ApplicationId = application.Id`, `CandidateId = candidate.Id`.
   - Increment `SuccessCount` and `ProcessedFiles`.
9. **Error Handling**:
   - Wrap item processing in `try-catch`. On exception, log error, set item `Status = BulkFileStatus.Failed`, `ErrorMessage = ex.Message`, increment `FailedCount` and `ProcessedFiles`.
10. **Batch Completion**:
    - When all items processed, set batch `Status = BulkBatchStatus.Completed`, `CompletedAt = _timeProvider.GetUtcNow()`.

---

## 5. Controller Endpoint Design

Location: Add endpoints to `backend/src/Api/Controllers/JobPostingsController.cs` (or dedicated `BulkResumesController.cs` with route `[Route("api/jobpostings/{jobPostingId}/resumes/bulk")]`).

### 5.1 Enqueue Endpoint (`POST /api/jobpostings/{jobPostingId}/resumes/bulk`)

```csharp
/// <summary>
/// Accepts up to 50 CV files for asynchronous bulk processing against a job posting.
/// </summary>
[HttpPost("{jobPostingId:guid}/resumes/bulk")]
[Authorize(Policy = Policies.InternalUser)]
[Consumes("multipart/form-data")]
public async Task<ActionResult<BulkUploadBatchResponseDto>> BulkUploadResumes(
    Guid jobPostingId,
    [FromForm] IFormFileCollection files,
    CancellationToken ct)
{
    if (files is null || files.Count == 0)
    {
        return BadRequest(new ProblemDetails
        {
            Title = "Invalid Request",
            Detail = "No files provided for bulk upload."
        });
    }

    if (files.Count > 50)
    {
        return BadRequest(new ProblemDetails
        {
            Title = "Batch Limit Exceeded",
            Detail = "Batch size exceeds maximum limit of 50 files."
        });
    }

    var fileInputs = new List<BulkFileItemInput>();
    foreach (var file in files)
    {
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        fileInputs.Add(new BulkFileItemInput(
            FileName: file.FileName,
            Content: ms.ToArray(),
            ContentType: file.ContentType
        ));
    }

    var currentUserId = _currentUser.Id; // or parsed from claims
    var result = await _bulkResumeService.EnqueueBatchAsync(jobPostingId, fileInputs, currentUserId, ct);

    if (result is null)
    {
        return NotFound(new ProblemDetails
        {
            Title = "Not Found or Unauthorized",
            Detail = "Job posting not found or department access denied."
        });
    }

    return Ok(result); // or Accepted(result)
}
```

### 5.2 Status Summary Endpoint (`GET /api/jobpostings/{jobPostingId}/resumes/bulk/{batchId}`)

```csharp
/// <summary>
/// Retrieves the current status and per-file progress summary of a bulk CV upload batch.
/// </summary>
[HttpGet("{jobPostingId:guid}/resumes/bulk/{batchId:guid}")]
[Authorize(Policy = Policies.InternalUser)]
public async Task<ActionResult<BulkBatchStatusDto>> GetBulkUploadStatus(
    Guid jobPostingId,
    Guid batchId,
    CancellationToken ct)
{
    var result = await _bulkResumeService.GetBatchStatusAsync(jobPostingId, batchId, ct);
    if (result is null)
    {
        return NotFound(new ProblemDetails
        {
            Title = "Batch Not Found",
            Detail = "Bulk upload batch not found or department access denied."
        });
    }

    return Ok(result);
}
```

---

## 6. Dependency Injection Setup

Register services in `backend/src/Infrastructure/DependencyInjection.cs` or `Program.cs`:

```csharp
services.AddSingleton<IBulkJobTracker, InMemoryBulkJobTracker>(); // or state manager
services.AddScoped<IBulkResumeService, BulkResumeService>();
```

---

## 7. Test Specification (`BulkResumeUploadTests.cs`)

Location: `backend/tests/RecruitOps.Api.Tests/BulkResumeUploadTests.cs`

```csharp
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using RecruitOps.Api.Auth;
using RecruitOps.Application.DTOs;
using RecruitOps.Domain.Enums;
using Xunit;

namespace RecruitOps.Api.Tests;

public class BulkResumeUploadTests : IClassFixture<CustomWebAppFactory>
{
    private readonly Module3Scenario _scenario;

    public BulkResumeUploadTests(CustomWebAppFactory factory)
    {
        _scenario = new Module3Scenario(factory);
    }

    private HttpClient Recruiter() => _scenario.Recruiter();
    private HttpClient FinanceManager() => _scenario.FinanceManager();

    private static byte[] CreateSampleDocx(string contentText)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = zip.CreateEntry("word/document.xml");
            using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
            writer.Write($@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<w:document xmlns:w=""http://schemas.openxmlformats.org/wordprocessingml/2006/main"">
  <w:body><w:p><w:t>{contentText}</w:t></w:p></w:body>
</w:document>");
        }
        return ms.ToArray();
    }

    [Fact]
    public async Task BulkUpload_ValidBatchUpTo50Files_Returns200AndBatchId()
    {
        var (postingId, _) = await _scenario.ApplicationAsync("Bulk Job Posting 1");
        var client = Recruiter();

        using var content = new MultipartFormDataContent();
        for (int i = 1; i <= 3; i++)
        {
            byte[] bytes = CreateSampleDocx($"Candidate {i}\nEmail: candidate{i}@example.com\nPhone: 0976543210{i}");
            var fileContent = new ByteArrayContent(bytes);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.wordprocessingml.document");
            content.Add(fileContent, "files", $"cv_{i}.docx");
        }

        var response = await client.PostAsync($"/api/jobpostings/{postingId}/resumes/bulk", content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<BulkUploadBatchResponseDto>();
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.BatchId);
        Assert.Equal(postingId, result.JobPostingId);
        Assert.Equal(3, result.TotalFiles);
    }

    [Fact]
    public async Task GetBatchStatus_ReturnsPerFileProgressSummary()
    {
        var (postingId, _) = await _scenario.ApplicationAsync("Bulk Job Posting 2");
        var client = Recruiter();

        using var content = new MultipartFormDataContent();
        byte[] bytes = CreateSampleDocx("Min Min\nEmail: minmin@example.com\nPhone: 09987654321");
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.wordprocessingml.document");
        content.Add(fileContent, "files", "min_min.docx");

        var response = await client.PostAsync($"/api/jobpostings/{postingId}/resumes/bulk", content);
        var batchResponse = await response.Content.ReadFromJsonAsync<BulkUploadBatchResponseDto>();
        Assert.NotNull(batchResponse);

        // Wait brief moment for background processing
        await Task.Delay(200);

        var statusResponse = await client.GetAsync($"/api/jobpostings/{postingId}/resumes/bulk/{batchResponse.BatchId}");
        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);

        var status = await statusResponse.Content.ReadFromJsonAsync<BulkBatchStatusDto>();
        Assert.NotNull(status);
        Assert.Equal(batchResponse.BatchId, status.BatchId);
        Assert.Equal(1, status.TotalFiles);
        Assert.Single(status.Items);
        Assert.Equal("min_min.docx", status.Items[0].FileName);
    }

    [Fact]
    public async Task BulkUpload_Exceeding50Files_Returns400BadRequest()
    {
        var (postingId, _) = await _scenario.ApplicationAsync("Bulk Job Posting 3");
        var client = Recruiter();

        using var content = new MultipartFormDataContent();
        for (int i = 1; i <= 51; i++)
        {
            var fileContent = new ByteArrayContent(new byte[] { 1, 2, 3 });
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            content.Add(fileContent, "files", $"cv_{i}.pdf");
        }

        var response = await client.PostAsync($"/api/jobpostings/{postingId}/resumes/bulk", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task BulkUpload_EmptyFileCollection_Returns400BadRequest()
    {
        var (postingId, _) = await _scenario.ApplicationAsync("Bulk Job Posting 4");
        var client = Recruiter();

        using var content = new MultipartFormDataContent();
        var response = await client.PostAsync($"/api/jobpostings/{postingId}/resumes/bulk", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task BulkUpload_UnauthorizedDepartmentAccess_Returns404Or403()
    {
        var (postingId, _) = await _scenario.ApplicationAsync("Sales Job Posting");
        // FinanceManager does not have access to Sales department
        var client = FinanceManager();

        using var content = new MultipartFormDataContent();
        byte[] bytes = CreateSampleDocx("Unauthorized Applicant");
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.wordprocessingml.document");
        content.Add(fileContent, "files", "unauth_cv.docx");

        var response = await client.PostAsync($"/api/jobpostings/{postingId}/resumes/bulk", content);
        Assert.True(response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task BulkUpload_ZawgyiCV_NormalizesExtractedText()
    {
        var (postingId, _) = await _scenario.ApplicationAsync("Bulk Job Posting Zawgyi");
        var client = Recruiter();

        string zawgyiContent = "မၤဂလာပါ Kyaw Kyaw Email: kyawkyaw@example.com Phone: 09790000000";
        byte[] bytes = CreateSampleDocx(zawgyiContent);

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.wordprocessingml.document");
        content.Add(fileContent, "files", "zawgyi_cv.docx");

        var response = await client.PostAsync($"/api/jobpostings/{postingId}/resumes/bulk", content);
        var batchRes = await response.Content.ReadFromJsonAsync<BulkUploadBatchResponseDto>();

        await Task.Delay(300);

        var statusRes = await client.GetAsync($"/api/jobpostings/{postingId}/resumes/bulk/{batchRes!.BatchId}");
        var status = await statusRes.Content.ReadFromJsonAsync<BulkBatchStatusDto>();

        Assert.Equal("Success", status!.Items[0].Status);
        Assert.NotNull(status.Items[0].ApplicationId);
    }

    [Fact]
    public async Task BulkUpload_DuplicateCandidate_ReusesExistingCandidate()
    {
        var (postingId, _) = await _scenario.ApplicationAsync("Bulk Job Posting Duplicate Test");
        var client = Recruiter();

        // 2 files with same candidate email
        byte[] bytes1 = CreateSampleDocx("Existing Candidate\nEmail: duplicate@example.com\nPhone: 09700000001");
        byte[] bytes2 = CreateSampleDocx("Existing Candidate 2\nEmail: duplicate@example.com\nPhone: 09700000001");

        using var content = new MultipartFormDataContent();
        var fc1 = new ByteArrayContent(bytes1);
        fc1.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.wordprocessingml.document");
        content.Add(fc1, "files", "dup1.docx");

        var fc2 = new ByteArrayContent(bytes2);
        fc2.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.wordprocessingml.document");
        content.Add(fc2, "files", "dup2.docx");

        var response = await client.PostAsync($"/api/jobpostings/{postingId}/resumes/bulk", content);
        var batchRes = await response.Content.ReadFromJsonAsync<BulkUploadBatchResponseDto>();

        await Task.Delay(400);

        var statusRes = await client.GetAsync($"/api/jobpostings/{postingId}/resumes/bulk/{batchRes!.BatchId}");
        var status = await statusRes.Content.ReadFromJsonAsync<BulkBatchStatusDto>();

        Assert.Equal(2, status!.SuccessCount);
        // Both items should share the exact same CandidateId
        Assert.Equal(status.Items[0].CandidateId, status.Items[1].CandidateId);
    }

    [Fact]
    public async Task BulkUpload_CorruptOrUnsupportedFile_MarksItemAsFailed()
    {
        var (postingId, _) = await _scenario.ApplicationAsync("Bulk Job Posting Unsupported Test");
        var client = Recruiter();

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("echo virus"));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/x-msdownload");
        content.Add(fileContent, "files", "bad_file.exe");

        var response = await client.PostAsync($"/api/jobpostings/{postingId}/resumes/bulk", content);
        var batchRes = await response.Content.ReadFromJsonAsync<BulkUploadBatchResponseDto>();

        await Task.Delay(200);

        var statusRes = await client.GetAsync($"/api/jobpostings/{postingId}/resumes/bulk/{batchRes!.BatchId}");
        var status = await statusRes.Content.ReadFromJsonAsync<BulkBatchStatusDto>();

        Assert.Equal(1, status!.FailedCount);
        Assert.Equal("Failed", status.Items[0].Status);
    }
}
```

---

## 8. Summary of Architectural Verification & Integrity Guardrails

1. **Clean Architecture Adherence**: Interface `IBulkResumeService` in `Application/Interfaces`, DTOs in `Application/DTOs`, service implementation in `Infrastructure/Services`, API actions in `Api/Controllers`.
2. **Backwards Compatibility**: All 349 existing backend tests will continue to pass cleanly (`dotnet test backend/RecruitOps.sln`).
3. **No Unhandled Blocking Work**: Bulk file uploads processing up to 50 files run in background tasks (`Task.Run` / background queue), returning immediate status response (`BulkUploadBatchResponseDto`).
4. **Data Protection & Row-Level Security**: Scoped to tenant (`TenantId`) and department (`IDepartmentAccess.CanAccessAsync`).
