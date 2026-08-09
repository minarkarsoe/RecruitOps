# Milestone 2: Bulk CV Upload Background Job — Technical Survey & Architecture Analysis

## 1. Executive Summary

This report provides a comprehensive survey and architectural design for implementing **Milestone 2 (Bulk CV Upload Background Job)** in the RecruitOps backend solution (`backend/RecruitOps.sln`). 

Milestone 1 established a solid foundation with single CV upload (`POST /api/applications/{id}/resume`), S3/MinIO object storage (`IFileStorage`), document text extraction (`IDocumentTextExtractor`), and Zawgyi-to-Unicode script normalization (`IMyanmarScriptNormalizer`). Milestone 2 expands this functionality to support batch CV processing for job postings via non-blocking background execution, progress monitoring, and candidate/application creation.

---

## 2. Existing Endpoints, Services, and M1 Components

### 2.1 API Controllers & Routes
- **`ApplicationsController.cs`** (`backend/src/Api/Controllers/ApplicationsController.cs`):
  - `POST /api/applications/{id}/resume`: Accepts single `IFormFile` (PDF/DOCX/PNG/JPG/JPEG, max 10MB). Uses `IResumeService.UploadAndExtractResumeAsync` to extract text, convert Zawgyi to Unicode, upload to `IFileStorage`, and record resume metadata on `JobApplication`.
  - `GET /api/applications/{id}/resume`: Downloads candidate resume stream using `IResumeService.GetResumeFileAsync`.
- **`JobPostingsController.cs`** (`backend/src/Api/Controllers/JobPostingsController.cs`):
  - Manages job vacancies (`POST /api/jobpostings`, `GET /api/jobpostings/{id}`, `POST /api/jobpostings/{id}/publish`, `GET /api/jobpostings/{id}/pipeline`).
  - Currently decorated with `[Authorize(Policy = Policies.InternalUser)]`.
  - Serves as the natural controller location for the bulk CV upload routes:
    - `POST /api/jobpostings/{jobPostingId}/resumes/bulk`
    - `GET /api/jobpostings/{jobPostingId}/resumes/bulk/{batchId}`

### 2.2 Application Services & Interfaces
- **`IResumeService` / `ResumeService`** (`backend/src/Infrastructure/Services/ResumeService.cs`):
  - Orchestrates single file upload, text extraction via `IDocumentTextExtractor`, object storage via `IFileStorage`, and updates `JobApplication` entity fields (`ResumeFileKey`, `ResumeFileName`, `ResumeExtractedText`, `ResumeUploadedAt`, `IsZawgyiNormalized`).
- **`IFileStorage` / `S3FileStorage`** (`backend/src/Infrastructure/Services/FileStorage/S3FileStorage.cs`):
  - S3-compatible storage abstraction (supports MinIO for dev and Cloudflare R2 for production).
  - Methods: `UploadAsync`, `DownloadAsync`, `DeleteAsync`, `GetPresignedUrlAsync`, `ExistsAsync`, `GetMetadataAsync`.
- **`IDocumentTextExtractor` / `DocumentTextExtractor`** (`backend/src/Infrastructure/Services/DocumentExtraction/DocumentTextExtractor.cs`):
  - Accepts stream, file name, and content type. Extracts plain text from PDF (text stream reader), DOCX (OpenXML document body parsing), and image fallback.
  - Automatically runs `IMyanmarScriptNormalizer` on all extracted text.
  - Parses contact info heuristics (`ExtractContactInfo`): candidate name, email, phone, years of experience, and skills list.
- **`IMyanmarScriptNormalizer` / `MyanmarScriptNormalizer`** (`backend/src/Infrastructure/Services/MyanmarScript/MyanmarScriptNormalizer.cs`):
  - Detects Zawgyi encoding and converts text to canonical Unicode NFC in-process.

### 2.3 Scoping, Authentication & Authorization
- **`ICurrentTenant` / `ICurrentUser`**: Provides request tenant context (`TenantId`) and user claims.
- **`IDepartmentAccess` / `DepartmentAccess`**: Enforces department-level row security (ADR-0003). Check: `await _access.CanAccessAsync(jobPosting.DepartmentId)`.
- **`IApplicationAccess` / `ApplicationAccess`**: Enforces application reach and panel participant access (ADR-0017 / ADR-0018).

---

## 3. Domain Entities & Data Model Analysis

### 3.1 Relevant Existing Entities
- **`JobPosting`**: Contains `TenantId`, `DepartmentId`, `RequisitionId`, `Title`, `Status`.
- **`Candidate`**:
  - Properties: `TenantId`, `FullName`, `Email`, `Phone`, `Source`, `MergedIntoCandidateId`.
  - Indexed on `(TenantId, Email)` and `(TenantId, Phone)` for duplicate lookup.
- **`JobApplication`**:
  - Properties: `TenantId`, `JobPostingId`, `CandidateId`, `Status`, `Source`, `AppliedAt`, `ResumeFileKey`, `ResumeFileName`, `ResumeExtractedText`, `ResumeUploadedAt`, `IsZawgyiNormalized`.
- **`ApplicationStageHistory`**: Append-only log tracking pipeline stage movements.

### 3.2 Required Data Models for Bulk Processing
To support background execution and status tracking (`GET /api/jobpostings/{jobPostingId}/resumes/bulk/{batchId}`), we need a batch tracking state model.

**Per-File Status Enum**:
```csharp
public enum BulkFileStatus
{
    Queued = 0,
    Processing = 1,
    Success = 2,
    Skipped = 3,
    Failed = 4
}
```

**Batch Status Enum**:
```csharp
public enum BulkBatchStatus
{
    Queued = 0,
    Processing = 1,
    Completed = 2,
    Failed = 3
}
```

**Proposed In-Memory / Database State Structure**:
Whether implemented as an in-memory thread-safe state repository (`IBulkJobTracker`) or persisted via EF Core (`BulkResumeBatch` and `BulkResumeItem` entities), the data structure must track:
- `BatchId` (Guid)
- `JobPostingId` (Guid)
- `TenantId` (Guid)
- `Status` (`BulkBatchStatus`)
- `TotalFiles` (int)
- `ProcessedCount` (int), `SuccessCount` (int), `SkippedCount` (int), `FailedCount` (int)
- `CreatedAt` (DateTimeOffset), `CompletedAt` (DateTimeOffset?)
- `Items`: List of file tracking items:
  - `FileName` (string)
  - `FileSizeBytes` (long)
  - `Status` (`BulkFileStatus`)
  - `ErrorMessage` (string?)
  - `ApplicationId` (Guid?)
  - `CandidateId` (Guid?)
  - `CandidateName` (string?)
  - `ProcessedAt` (DateTimeOffset?)

---

## 4. Milestone 2 Architecture & Endpoint Design

### 4.1 Endpoint 1: `POST /api/jobpostings/{jobPostingId}/resumes/bulk`

- **Route**: `POST /api/jobpostings/{jobPostingId}/resumes/bulk`
- **Content-Type**: `multipart/form-data`
- **Request Parameters**:
  - `jobPostingId` (Route parameter, Guid)
  - `files` (Form payload, `IReadOnlyList<IFormFile>`)
- **Validation Rules**:
  1. `files` must not be null or empty (return `400 BadRequest` if empty).
  2. `files.Count` must not exceed 50 (return `400 BadRequest` if `files.Count > 50`).
  3. Validate job posting exists and user has department access via `IDepartmentAccess.CanAccessAsync(posting.DepartmentId)` (return `404 NotFound` / `403 Forbidden` if denied).
  4. Each file length <= 10MB, extension in `.pdf`, `.docx`, `.png`, `.jpg`, `.jpeg`.
- **Response**: `202 Accepted` (or `200 OK`) returning `BulkResumeBatchResponseDto`:
  ```json
  {
    "batchId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "jobPostingId": "7c62247a-2b76-4e24-bb32-6223781d69f6",
    "totalFiles": 12,
    "status": "Queued",
    "createdAt": "2026-08-08T14:57:53Z"
  }
  ```

### 4.2 Endpoint 2: `GET /api/jobpostings/{jobPostingId}/resumes/bulk/{batchId}`

- **Route**: `GET /api/jobpostings/{jobPostingId}/resumes/bulk/{batchId}`
- **Authorization**: Department access check on `jobPostingId`.
- **Response**: `200 OK` returning `BulkResumeBatchStatusDto`:
  ```json
  {
    "batchId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "jobPostingId": "7c62247a-2b76-4e24-bb32-6223781d69f6",
    "status": "Processing",
    "totalFiles": 12,
    "processedCount": 5,
    "successCount": 4,
    "skippedCount": 0,
    "failedCount": 1,
    "createdAt": "2026-08-08T14:57:53Z",
    "completedAt": null,
    "items": [
      {
        "fileName": "john_doe_resume.pdf",
        "fileSizeBytes": 245000,
        "status": "Success",
        "errorMessage": null,
        "applicationId": "d1234567-89ab-cdef-0123-456789abcdef",
        "candidateId": "c1234567-89ab-cdef-0123-456789abcdef",
        "candidateName": "John Doe",
        "processedAt": "2026-08-08T14:57:55Z"
      },
      {
        "fileName": "corrupted_cv.docx",
        "fileSizeBytes": 1200,
        "status": "Failed",
        "errorMessage": "Unsupported file header or document extraction failed.",
        "applicationId": null,
        "candidateId": null,
        "candidateName": null,
        "processedAt": "2026-08-08T14:57:56Z"
      }
    ]
  }
  ```

### 4.3 Background Job Execution Architecture

To avoid blocking HTTP requests while processing up to 50 documents (which involves PDF parsing, OpenXML DOCX parsing, OCR, and S3 uploads), an asynchronous queue pattern is recommended.

**Architecture Options**:
1. **`System.Threading.Channels.Channel<T>` + `BackgroundService` (`IHostedService`)**:
   - `Channel<BulkResumeTask>` delivers jobs to a background queue processor singleton.
   - Thread-safe in-memory or EF DB status updates (`IBulkJobTracker`).
   - Advantage: Clean, native .NET 10 background service execution with zero external queue dependencies (Celery/RabbitMQ/Redis not required).
2. **`Task.Run` background processing with scoped service factory**:
   - Spawns background worker loop for the batch items using `IServiceScopeFactory`.
   - Suitable for light/medium loads with in-memory status tracking.

**Per-File Asynchronous Processing Pipeline**:
For each file in the batch:
1. Mark file status as `Processing`.
2. Read file bytes/stream into memory.
3. Perform document text extraction via `IDocumentTextExtractor.ExtractTextAsync` (handles PDF, DOCX, image fallback, and Myanmar script Zawgyi->Unicode normalization).
4. Parse contact info (`ParsedContactInfo`).
5. **Candidate Matching / Creation**:
   - Extract candidate email and phone using `ContactNormalizer.Email` and `ContactNormalizer.Phone`.
   - Determine full name from `ParsedContactInfo.CandidateName` or fallback to filename without extension (e.g., "John Doe" from `john_doe_resume.pdf`).
   - Search existing `Candidate` in DB matching `(TenantId, Email)` or `(TenantId, Phone)`.
   - If candidate exists: update empty candidate details. If not found: create new `Candidate`.
6. **Application Creation**:
   - Create `JobApplication` entity: `TenantId`, `JobPostingId`, `CandidateId`, `Status = PipelineStatus.Sourced`, `Source = SourceChannel.Direct`, `AppliedAt = Now`.
7. **Storage Upload**:
   - Upload stream to `IFileStorage` with key `applications/{applicationId}/resume/{Guid}_{fileName}`.
   - Update `JobApplication` resume fields (`ResumeFileKey`, `ResumeFileName`, `ResumeExtractedText`, `ResumeUploadedAt`, `IsZawgyiNormalized`).
8. **Stage History**:
   - Record `ApplicationStageHistory` with `FromStatus = null`, `ToStatus = PipelineStatus.Sourced`, `Note = "Bulk CV Upload"`.
9. **Commit & Update Status**:
   - Save DB changes.
   - Update file status to `Success` with `applicationId`, `candidateId`, `candidateName`.
   - Increment `ProcessedCount` and `SuccessCount`.
10. **Error Handling**:
    - If file extraction or storage upload fails: catch exception, log error, mark file status as `Failed` with `errorMessage`, increment `FailedCount`.

---

## 5. Backend Test Suite Analysis & Milestone 2 Testing Strategy

### 5.1 Test Suite Structure
- Location: `backend/tests/RecruitOps.Api.Tests`
- Infrastructure: `CustomWebAppFactory` (uses EF Core InMemory provider, custom test headers `X-Test-Tenant`, `X-Test-Roles`, `X-Test-UserId`).
- Helpers: `Module3Scenario` (boots test postings, applications, requisitions).
- Storage: `InMemoryFileStorage` registered for unit/integration tests.

### 5.2 Required Tests for Milestone 2
At least 8+ new backend tests should be added to `RecruitOps.Api.Tests/BulkResumeUploadTests.cs`:
1. `BulkUpload_ValidBatch_Returns202AndBatchId`: Uploads 3 valid CV files (.pdf, .docx, .png), verifies batch ID returned.
2. `BulkUpload_Exceeds50Files_Returns400BadRequest`: Attempts upload of 51 files, verifies 400 error response.
3. `BulkUpload_EmptyBatch_Returns400BadRequest`: Sends empty file collection, verifies 400 error.
4. `GetBatchStatus_ValidBatchId_ReturnsStatusAndItemProgress`: Queries batch progress endpoint during and after completion, verifies counts and per-file statuses.
5. `BulkUpload_ZawgyiResume_NormalizesExtractedText`: Includes Zawgyi Burmese text CV in bulk upload, verifies output candidate/application has normalized Unicode text and `IsZawgyiNormalized == true`.
6. `BulkUpload_InvalidFileExtension_MarksItemAsFailedOrSkipped`: Uploads batch containing a disallowed file format (.exe), verifies batch completes with item marked as Failed.
7. `BulkUpload_DuplicateCandidate_ReusesExistingCandidate`: Uploads CV with an email matching an existing candidate, verifies new `JobApplication` is created under the existing `Candidate`.
8. `GetBatchStatus_NonExistentBatchId_Returns404NotFound`: Queries status for a random batch Guid, verifies 404 response.

---

## 6. Summary of Architectural Recommendations for Implementers

1. **Keep Clean Architecture Layering**:
   - Interface `IBulkResumeService` (or similar) in `Application/Interfaces`.
   - DTOs (`BulkResumeBatchResponseDto`, `BulkResumeBatchStatusDto`, `BulkResumeItemStatusDto`) in `Application/DTOs`.
   - Implementations in `Infrastructure/Services/BulkResumeService.cs`.
   - Expose endpoints on `JobPostingsController.cs` to maintain route consistency under `/api/jobpostings/{jobPostingId}/resumes/bulk`.
2. **Reuse Existing Components**:
   - Leverage `IDocumentTextExtractor` for parsing PDF/DOCX/PNG/JPG and Zawgyi normalization.
   - Leverage `IFileStorage` for S3 object persistence.
   - Leverage `ContactNormalizer` for email/phone formatting and candidate matching.
3. **Async Non-blocking Design**:
   - Use `BackgroundService` or `IServiceScopeFactory` queue to ensure `POST` HTTP request returns immediately with `202 Accepted` / `200 OK`.
4. **Preserve Green Test Suite**:
   - Ensure all 349 existing backend tests continue to pass cleanly (`dotnet test backend/RecruitOps.sln`).
