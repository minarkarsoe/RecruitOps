# Specification Mining Analysis: Flow 1 (CV Upload & Text Extraction)

**Agent:** `teamwork_preview_spec_miner`  
**Working Directory:** `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_spec_miner_survey_3`  
**Target Domain:** RecruitOps — Flow 1 (CV Upload, Storage, Local Document Text Extraction, Zawgyi Script Normalization, Bulk Async Job, SlideOver CV Viewer, and Parsed Profile Human Review UI)  
**Date:** 2026-08-07

---

## 1. Executive Summary & Authoritative Source Traceability

This specification document mines, synthesizes, and formalizes all requirements, architectural decisions, interface contracts, error behaviors, and verification criteria for **Flow 1 (CV Upload & Text Extraction)** in RecruitOps.

### Authoritative Sources Analyzed
1. **`ORIGINAL_REQUEST.md`** (Flow 1 Prompt — 2026-08-07): Establishes the exact scope, API endpoints, file limits (10MB), supported MIME types (PDF, DOCX, PNG, JPG), bulk batch limits (50 files), background job requirements, and UI component specifications.
2. **`ADR-0008-document-extraction-and-ai-profiling.md`**: Defines Phase 1 MVP in-process local text extraction without external network calls, mandatory human confirmation gate (never auto-write parsed PII into database without recruiter approval), provenance persistence, permissive licensing requirement (disqualifying AGPL/copyleft libraries), and scanned PDF local OCR fallback.
3. **`ADR-0009-myanmar-script-handling.md`**: Mandates automatic Zawgyi→Unicode (NFC) script normalization at all text ingest boundaries via `IMyanmarScriptNormalizer`. Requires storing both raw text and normalized text with detection metadata.
4. **`ADR-0013-infrastructure-and-storage.md`**: Mandates storing uploaded CV files via the `IFileStorage` S3-compatible abstraction (Cloudflare R2 for hosted / MinIO for on-premise).
5. **`CLAUDE.md`**: Outlines Clean Architecture guidelines (Domain / Application / Infrastructure / Api), package licensing restrictions, RBAC authorization policies (`Policies.RecruitmentStaff`, `Policies.InternalUser`), and test suite baseline expectations.

---

## 2. System Architecture & Requirements Breakdown

### 2.1 Technical Architecture Overview
Flow 1 spans the backend modular monolith (`backend/src/`) and frontend internal SPA (`frontend/internal/src/features/pipeline/` and `pages/`):

```
                        [ User / Recruiter ]
                                 │
           ┌─────────────────────┴─────────────────────┐
           ▼                                           ▼
[ CandidateSlideOver.tsx ]               [ JobPostingDetailPage.tsx ]
(CV Viewer & Human Review)              (Bulk Upload Modal - <=50 files)
           │                                           │
           └─────────────────────┬─────────────────────┘
                                 │ HTTP Multi-Part Form Data
                                 ▼
                     [ API Controllers ]
     POST /api/applications/{id}/resume  |  POST /api/jobpostings/{id}/resumes/bulk
                                 │
                                 ▼
                     [ Application Layer ]
            (IResumeService / IBulkResumeJobRunner)
                   │                       │
      ┌────────────┴────────────┐          ▼
      ▼                         ▼     [ Object Storage ]
[ IDocumentTextExtractor ] [ IMyanmarScriptNormalizer ] ---> (IFileStorage: S3/R2/MinIO)
(PDF Stream / DOCX XML /    (Zawgyi -> Unicode NFC)
 Image OCR Fallback)
```

---

## 3. Features Discovered

| # | Category | Feature | Description | Inputs | Outputs | Error Behavior | Discovered Via |
|---|----------|---------|-------------|--------|---------|----------------|----------------|
| 1 | Backend API | Single CV Upload & Extraction API | Endpoint `POST /api/applications/{id}/resume` uploads a single candidate CV file, stores it via `IFileStorage`, extracts text, normalizes Zawgyi script, and extracts basic contact info. | Application ID (`Guid`), Form file (`IFormFile`, PDF/DOCX/PNG/JPG, <=10MB) | `ResumeExtractionResultDto` (`fileKey`, `extractedText`, `detectedLanguage`, `isZawgyiNormalized`, `parsedContactInfo`) | Returns 400 Bad Request if file size >10MB or invalid format; returns 404 if Application ID not found; 401/403 if unauthorized. | `ORIGINAL_REQUEST.md` (R1), `ADR-0008`, `ADR-0013` |
| 2 | Backend API | Resume Download / View API | Endpoint `GET /api/applications/{id}/resume` streams or returns presigned URL for downloading stored CV file. | Application ID (`Guid`) | Binary file stream or presigned URL redirect with original Content-Type | Returns 404 Not Found if resume or application does not exist. | `ORIGINAL_REQUEST.md` (R1), `ADR-0013` |
| 3 | Ingest Pipeline | Local Document Text Extractor | Extract plain text in-process without network calls. Parses PDF content streams, Word `.docx` OpenXML, images via local OCR, and scanned PDFs via page rendering + OCR fallback. | Raw file stream & file extension (`.pdf`, `.docx`, `.png`, `.jpg`, `.jpeg`) | Extracted plain text string | Corrupt or password-protected files return empty text / flag extraction error; does not throw unhandled exception. | `ADR-0008`, `ORIGINAL_REQUEST.md` (R1) |
| 4 | Ingest Pipeline | Myanmar Script Normalizer Ingestion | Normalizes all extracted text using `IMyanmarScriptNormalizer`. Converts Zawgyi to Unicode (NFC), sets `isZawgyiNormalized: true` and detects encoding. | Raw extracted text string | `MyanmarScriptNormalizationResult` (`NormalizedText`, `OriginalText`, `IsZawgyiDetected`, `ConfidenceScore`, `DetectedEncoding`) | Returns original text cleanly if input is non-Burmese or pure Unicode. | `ADR-0009`, `ORIGINAL_REQUEST.md` (R1) |
| 5 | Background Job | Bulk CV Upload Endpoint | Endpoint `POST /api/jobpostings/{jobPostingId}/resumes/bulk` accepts up to 50 CV files in a single batch for background ingestion. | Job Posting ID (`Guid`), Form file list (`IFormFileCollection`, max 50 files) | `BulkUploadBatchResponseDto` (`batchId`, `totalFiles`, `status: "Queued"`, `createdAt`) | Returns 400 Bad Request if file count exceeds 50; 404 if posting not found. | `ORIGINAL_REQUEST.md` (R2), `ADR-0008` (Guardrail 3) |
| 6 | Background Job | Bulk Job Status Tracking API | Endpoint `GET /api/jobpostings/{jobPostingId}/resumes/bulk/{batchId}` returns real-time progress for a bulk upload batch. | Job Posting ID (`Guid`), Batch ID (`Guid`) | `BulkBatchStatusDto` (`batchId`, `status`, `totalCount`, `processedCount`, `successCount`, `skippedCount`, `failedCount`, `fileStatuses[]`) | Returns 404 Not Found if batch ID does not exist for the posting. | `ORIGINAL_REQUEST.md` (R2) |
| 7 | Frontend UI | Candidate 360 SlideOver CV Tab | "CV Viewer & Documents" tab inside `CandidateSlideOver.tsx` featuring drag-and-drop file upload zone, upload progress bar, document preview link, and extracted text viewer. | Selected candidate application object | Interactive UI tab with file drop zone, progress state, and text viewer | Shows inline toast / alert error message on upload failure or file size validation. | `ORIGINAL_REQUEST.md` (R3), `CandidateSlideOver.tsx` |
| 8 | Frontend UI | Parsed Profile Human Review Panel | Side-by-side or structured review panel displaying extracted contact info (Name, Email, Phone, Experience, Skills) with editable input fields and an explicit "Confirm & Apply to Profile" button. | Parsed extraction DTO | Editable profile form state | Requires explicit click on "Confirm & Apply" before updating application candidate record (ADR-0008 Guardrail 1). | `ORIGINAL_REQUEST.md` (R3), `ADR-0008` |
| 9 | Frontend UI | Bulk CV Upload Modal | Modal component on `JobPostingDetailPage` allowing recruiters to drag-and-drop up to 50 CV files, displaying live per-file upload and processing progress bars. | Target Job Posting ID | Modal with file selection, multi-progress bar, batch status polling | Displays file-level error indicators for skipped/failed files without breaking the batch view. | `ORIGINAL_REQUEST.md` (R3) |

---

## 4. Edge Cases & Boundary Conditions

| # | Feature | Input / Condition | Observed / Mandated Behavior |
|---|---------|-------------------|------------------------------|
| 1 | Single CV Upload | File size = 10.5 MB (>10MB limit) | Endpoint rejects request immediately with HTTP 400 Bad Request (`"File size exceeds maximum limit of 10MB."`). File is not stored. |
| 2 | Single CV Upload | Extension `.exe`, `.doc` (legacy binary Word), or `.txt` | Rejects with HTTP 400 Bad Request (`"Unsupported file format. Allowed formats: PDF, DOCX, PNG, JPG, JPEG."`). |
| 3 | Text Extraction | Scanned PDF (digital text stream is empty) | Text extractor detects empty content stream, renders PDF pages to images in-memory, and routes through local OCR engine. |
| 4 | Text Extraction | Corrupt or password-protected PDF/DOCX | Extraction handles exception gracefully, logs warning, returns empty/partial text, and marks file status as `Skipped` with error detail (`"File is password-protected or corrupted"`). |
| 5 | Script Normalization | Zawgyi-encoded Burmese text in PDF/DOCX | `IMyanmarScriptNormalizer` detects `MyanmarEncoding.Zawgyi`, converts text to canonical Unicode NFC, sets `IsZawgyiDetected = true`, and populates both `NormalizedText` and `OriginalText`. |
| 6 | Bulk CV Ingestion | Request containing 51 files (>50 limit) | Endpoint rejects entire request prior to job creation with HTTP 400 Bad Request (`"Batch size exceeds maximum limit of 50 files."`). |
| 7 | Bulk Job Processing | 1 file out of 50 fails (e.g. corrupt image) | Processing continues for remaining 49 files. The failed file updates status to `Failed` or `Skipped`. The overall batch status reaches `Completed` with summary count `failedCount: 1, successCount: 49`. |
| 8 | Human Review UI | Extracted candidate name is blank or inaccurate | Parsed Profile Human Review panel pre-fills editable text inputs. Recruiter edits fields manually before clicking "Confirm & Apply". Candidate profile is ONLY updated with recruiter-approved values. |
| 9 | Storage Abstraction | Application running in Local Docker vs Cloud | In local dev/Docker, `IFileStorage` routes upload to MinIO container. In production, routes to Cloudflare R2. Code remains 100% agnostic. |

---

## 5. Detailed Data Contracts & Schemas

### 5.1 Backend DTOs (`RecruitOps.Application.DTOs`)

#### `ResumeExtractionResultDto`
```csharp
namespace RecruitOps.Application.DTOs;

public record ParsedContactInfoDto(
    string? CandidateName,
    string? Email,
    string? Phone,
    int? YearsOfExperience,
    List<string> Skills
);

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
    DateTime ProcessedAt
);
```

#### `BulkUploadBatchDtos`
```csharp
namespace RecruitOps.Application.DTOs;

public enum BulkFileStatus
{
    Queued,
    Processing,
    Success,
    Skipped,
    Failed
}

public record BulkFileItemStatusDto(
    string FileName,
    long FileSizeBytes,
    BulkFileStatus Status,
    string? ErrorMessage,
    Guid? CreatedApplicationId
);

public record BulkUploadBatchResponseDto(
    Guid BatchId,
    Guid JobPostingId,
    int TotalFiles,
    string Status, // "Queued", "Processing", "Completed", "Failed"
    DateTime CreatedAt
);

public record BulkBatchStatusDto(
    Guid BatchId,
    Guid JobPostingId,
    string Status,
    int TotalCount,
    int ProcessedCount,
    int SuccessCount,
    int SkippedCount,
    int FailedCount,
    List<BulkFileItemStatusDto> FileStatuses,
    DateTime CreatedAt,
    DateTime? CompletedAt
);
```

---

## 6. Non-Functional & Architectural Constraints

1. **Permissive Licensing Mandatory**: All .NET packages used for PDF parsing, OpenXML, and OCR MUST be permissively licensed (MIT, Apache-2.0, BSD). Copyleft or AGPL packages (such as iText 7 AGPL or pdf2image GPL) are strictly forbidden due to closed-source commercial delivery rules (ADR-0008).
2. **In-Process Local MVP**: Processing must not depend on cloud AI APIs for baseline extraction. Local text extraction and local OCR fallback satisfy offline on-premise installation constraints (ADR-0008).
3. **Storage Isolation**: Storage operations MUST use `IFileStorage` abstraction. Direct calls to AWS SDK / MinIO SDK / Cloudflare R2 outside `Infrastructure/Services/FileStorage` are prohibited (ADR-0013).
4. **Mandatory Script Normalization**: Every extracted text string must pass through `IMyanmarScriptNormalizer.Normalize()` before saving or displaying, converting Zawgyi code points to Unicode NFC (ADR-0009).
5. **Human Gate Constraint**: Unreviewed extraction results must NEVER automatically mutate candidate records without recruiter confirmation (ADR-0008 Guardrail 1).

---

## 7. Verification Criteria & Test Specifications

### Backend Verification (`dotnet test backend/RecruitOps.sln`)
- Current baseline: **333 passing backend tests**.
- Requirements for Flow 1:
  1. `POST /api/applications/{id}/resume` stores file in `IFileStorage` and extracts readable text.
  2. PDF extraction tests for digital text streams and scanned image fallback.
  3. DOCX OpenXML extraction tests for body text parsing.
  4. Zawgyi normalization test on extracted document text verifying `IsZawgyiNormalized == true` and output is valid Unicode NFC.
  5. File size boundary test (>10MB rejected with 400 Bad Request).
  6. Unsupported format test (`.doc` or `.exe` rejected with 400 Bad Request).
  7. Bulk upload test (`POST /api/jobpostings/{id}/resumes/bulk`) accepting 1-50 files and returning valid `BatchId`.
  8. Bulk batch status polling test (`GET .../bulk/{batchId}`) returning correct file-level counts (`successCount`, `skippedCount`, `failedCount`).
  9. Minimum **8 new backend unit/integration tests** added to maintain test suite green status.

### Frontend Verification (`npm run test` & `npm run typecheck`)
- Current baseline: **233 passing Vitest tests**, **0 TypeScript errors**.
- Requirements for Flow 1:
  1. `CandidateSlideOver.tsx` renders "CV Viewer" tab with dropzone and extracted text view.
  2. Parsed Profile Human Review panel updates state on input change and triggers save on "Confirm & Apply".
  3. Bulk upload modal on `JobPostingDetailPage` handles multi-file selection (<=50) and renders progress per file.
  4. Minimum **5 new Vitest tests** covering CV upload and review panel interactions.
  5. Clean `npm run typecheck` across `@recruitops/internal`, `@recruitops/public`, and `@recruitops/types`.
