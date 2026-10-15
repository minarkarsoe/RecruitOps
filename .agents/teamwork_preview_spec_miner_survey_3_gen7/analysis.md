# Detailed Specification Extraction & ADR Requirements Analysis

**Target Scope:** RecruitOps Person A - Flow 1 (Milestone 2 & Milestone 3)  
**Authoritative Sources:** `ORIGINAL_REQUEST.md`, `docs/decisions/` (ADR-0008, ADR-0009, ADR-0013, ADR-0016), `PROJECT.md`, `CLAUDE.md`, `packages/types/src/index.ts`, `backend/src/`.  
**Agent:** `survey_3` (teamwork_preview_spec_miner)  
**Date:** 2026-08-08  

---

## 1. Executive Summary & Authoritative Source Traceability

This specification document formalizes the exact requirements, status enums, validation rules, data contracts, edge cases, and non-functional constraints for **Milestone 2 & Milestone 3 of Person A - Flow 1**.

### Authoritative Specification Sources:
1. **`ORIGINAL_REQUEST.md` (Follow-up 2026-08-08)**: Defines Milestones 2 & 3 scope.
   - **Milestone 2**: Bulk CV Upload Background Job (`POST /api/jobpostings/{jobPostingId}/resumes/bulk` up to 50 files, async job processing, progress summary endpoint `GET /api/jobpostings/{jobPostingId}/resumes/bulk/{batchId}`).
   - **Milestone 3**: Candidate 360 SlideOver CV Viewer & Parsed Profile UI (`CandidateSlideOver.tsx` update with CV & Documents tab, dropzone, progress bar, embedded text viewer, Parsed Profile Human Review panel side-by-side with editable fields, explicit recruiter confirmation, and Bulk CV Upload modal on `JobPostingDetailPage`).
2. **`ADR-0008` (Document Extraction & AI Profiling)**:
   - Phase 1 MVP in-process local document text extraction (PDF content stream, Word DOCX OpenXML, image OCR fallback, scanned PDF page rendering fallback).
   - Mandatory Human Confirmation Gate: Extracted PII is never written directly to candidate profile without recruiter review & confirmation.
   - Async bulk background processing (max 50 files) returning `Queued`, `Processing`, `Success`, `Skipped`, `Failed` statuses.
   - Permissive licensing constraint (MIT/Apache-2.0/BSD only; copyleft/AGPL strictly prohibited).
3. **`ADR-0009` (Myanmar Script Handling)**:
   - Ingest normalization: Detect Zawgyi encoding and convert Zawgyi -> Unicode (NFC) via `IMyanmarScriptNormalizer`.
   - Store both normalized Unicode text (canonical for search/display) and original raw text with detection metadata (`IsZawgyiNormalized`, `DetectedLanguage`).
   - Burmese OCR is deferred/optional; system must operate fully without it.
4. **`ADR-0013` (Infrastructure & Storage)**:
   - Storage abstraction via `IFileStorage` (Cloudflare R2 for cloud / MinIO for on-premise).
   - PostgreSQL JSONB for custom fields and dynamic schemas.
5. **`ADR-0016` (Auth & Login Protection)**:
   - Rate limiting & refresh token mechanics.
6. **Existing Backend / Shared Types Codebase**:
   - `backend/src/Application/DTOs/ResumeExtractionDtos.cs`
   - `backend/src/Application/Interfaces/IResumeService.cs`
   - `backend/src/Application/Interfaces/IDocumentTextExtractor.cs`
   - `backend/src/Application/Interfaces/IMyanmarScriptNormalizer.cs`
   - `packages/types/src/index.ts`

---

## 2. Features Discovered

| # | Category | Feature | Description | Inputs | Outputs | Error Behavior | Discovered Via |
|---|----------|---------|-------------|--------|---------|----------------|----------------|
| 1 | Backend API | Single CV Upload & Extraction API | Upload single candidate CV file, store via `IFileStorage`, extract text, normalize Zawgyi script, extract contact info heuristics. | Application ID (`Guid`), Form file (`IFormFile`, PDF/DOCX/PNG/JPG/JPEG, <=10MB) | `ResumeExtractionResultDto` (`fileKey`, `extractedText`, `detectedLanguage`, `isZawgyiNormalized`, `parsedContactInfo`) | 400 Bad Request if file >10MB or invalid MIME/extension; 404 if Application ID not found; 401/403 if unauthorized. | `ORIGINAL_REQUEST.md`, `ApplicationsController.cs`, `ADR-0008` |
| 2 | Backend API | Resume File Download API | Stream or download stored CV file for an application. | Application ID (`Guid`) | File Stream (`Stream`, `ContentType`, `FileName`) | 404 Not Found if resume or application missing/unauthorized. | `ORIGINAL_REQUEST.md`, `ApplicationsController.cs` |
| 3 | Backend API | Bulk CV Upload Endpoint | Accepts up to 50 CV files for async batch ingestion. | Job Posting ID (`Guid`), `files` (`IFormFileCollection`, 1-50 files) | `BulkUploadBatchResponseDto` (`batchId`, `jobPostingId`, `totalFiles`, `status`, `createdAt`) | 400 Bad Request if files count = 0 or >50; 404 if JobPosting not found. | `ORIGINAL_REQUEST.md` (R2), `ADR-0008` |
| 4 | Backend API | Bulk Batch Status Progress API | Returns summary status and per-file progress for a bulk CV upload batch. | Job Posting ID (`Guid`), Batch ID (`Guid`) | `BulkBatchStatusDto` (`batchId`, `jobPostingId`, `status`, `totalCount`, `processedCount`, `successCount`, `skippedCount`, `failedCount`, `fileStatuses[]`, `createdAt`, `completedAt`) | 404 Not Found if batch ID not found for job posting. | `ORIGINAL_REQUEST.md` (R2), `ADR-0008` |
| 5 | Backend Domain | Candidate Profile Confirmation API | Update candidate profile record after explicit recruiter review of parsed data. | Candidate ID (`Guid`), `UpdateCandidateProfileRequest` (`fullName`, `email`, `phone`, `yearsOfExperience`, `skills`) | `CandidateDetail` / `Candidate` updated entity | 400 Bad Request if validation fails; 404 if candidate not found. | `ADR-0008` (Guardrail 1), `ORIGINAL_REQUEST.md` (R3) |
| 6 | Ingest Pipeline | Local Document Text Extractor | Extract text in-process from PDF content streams, OpenXML `.docx`, and local OCR image fallback. | Raw file stream, file name, MIME/extension | `DocumentExtractionResult` (`extractedText`, `originalText`, `detectedLanguage`, `isZawgyiNormalized`, `parsedContactInfo`) | Corrupt or password-protected files log warning and return empty/partial text without throwing. | `ADR-0008`, `DocumentTextExtractor.cs` |
| 7 | Ingest Pipeline | Myanmar Script Normalizer | Converts Zawgyi text to canonical Unicode (NFC) using `IMyanmarScriptNormalizer`. | Raw extracted text | `MyanmarScriptNormalizationResult` (`normalizedText`, `originalText`, `isZawgyiDetected`, `confidenceScore`, `detectedEncoding`) | Returns clean original string if non-Myanmar or Unicode. | `ADR-0009`, `MyanmarScriptNormalizer.cs` |
| 8 | Frontend UI | Candidate 360 SlideOver CV & Documents Tab | Drag-and-drop file upload zone, progress bar, file download link, and embedded CV text viewer. | Selected Candidate Application object | Interactive CV upload zone & text viewer UI | Shows inline error alert/toast on upload error or size limit excess. | `CandidateSlideOver.tsx`, `ORIGINAL_REQUEST.md` (R3) |
| 9 | Frontend UI | Parsed Profile Human Review Panel | Side-by-side view showing extracted text next to editable candidate profile fields (Name, Email, Phone, Experience, Skills) with explicit "Confirm & Apply" button. | Extraction result DTO, Candidate profile | Editable candidate form requiring explicit submit | Requires recruiter confirmation click before updating candidate record. | `CandidateSlideOver.tsx`, `ADR-0008` |
| 10 | Frontend UI | Bulk CV Upload Modal | Modal component on `JobPostingDetailPage` allowing recruiters to select/drop up to 50 CVs with live progress indicators. | Target Job Posting ID | Multi-file dropzone, batch upload trigger, live polling progress bar | Displays file-level error indicators for skipped/failed files without failing batch view. | `ORIGINAL_REQUEST.md` (R3) |

---

## 3. Edge Cases & Boundary Conditions

| # | Feature | Input / Condition | Observed / Mandated Behavior |
|---|---------|-------------------|------------------------------|
| 1 | Single CV Upload | File size = 10.5 MB (>10MB limit) | HTTP 400 Bad Request (`"File size exceeds maximum limit of 10MB."`). Storage and extraction bypassed. |
| 2 | Single CV Upload | Unsupported extension (e.g. `.exe`, `.doc`, `.txt`) | HTTP 400 Bad Request (`"Allowed formats are PDF, DOCX, PNG, JPG, JPEG."`). |
| 3 | Bulk CV Upload | File count = 0 (empty collection) | HTTP 400 Bad Request (`"No files provided for bulk upload."`). |
| 4 | Bulk CV Upload | File count = 51 (>50 limit) | HTTP 400 Bad Request (`"Batch size exceeds maximum limit of 50 files."`). Batch rejected before job creation. |
| 5 | Text Extraction | Scanned PDF (empty text stream) | Detects empty text stream in PdfPig, renders pages to images, falls back to local OCR extractor. |
| 6 | Text Extraction | Corrupt or password-protected PDF/DOCX | Handles exception, logs warning, marks file item status as `Skipped` / `Failed` with message `"File is password-protected or corrupted"`. |
| 7 | Script Normalization | Zawgyi-encoded Burmese text | `IMyanmarScriptNormalizer` detects Zawgyi, converts to Unicode (NFC), sets `IsZawgyiNormalized = true`, `DetectedLanguage = "my-Zawgyi"`. |
| 8 | Bulk Job Execution | Partial batch failure (e.g. 2 out of 50 files corrupt) | Background runner processes remaining 48 files cleanly. Batch completes with status `Completed`, `successCount: 48, failedCount: 2, totalCount: 50`. |
| 9 | Human Confirmation Gate | Extracted contact info missing candidate name or phone | Form fields remain blank or pre-filled with parsed values. Recruiter edits fields manually and clicks "Confirm & Apply to Profile". Database only updates upon confirmation. |
| 10 | Storage Backend | Environment set to MinIO (Local) vs R2 (Cloud) | `IFileStorage` abstraction handles endpoint & bucket seamlessly. App layer uses identical `UploadAsync`/`DownloadAsync` calls. |

---

## 4. Status Enums & Domain Vocabulary

### 4.1 `PipelineStatus` (Candidate Stage)
- `Sourced`: Recruiter added candidate; applicant has not submitted form.
- `Applied`: Submitted application via posting form or public link.
- `Screening`: Initial review by recruiter.
- `Shortlisted`: Approved for hiring manager review.
- `Interview`: In active interview pipeline.
- `Offer`: Offer extended.
- `Hired`: Offer accepted & candidate hired.
- `Rejected`: Application declined.

### 4.2 `BulkFileStatus` (Per-file status in bulk batch)
- `Queued`: File received and queued for processing.
- `Processing`: Currently undergoing text extraction & storage.
- `Success`: Uploaded, text extracted, application created successfully.
- `Skipped`: Skipped due to format issue, corruption, duplicate, or password protection.
- `Failed`: Critical failure during processing.

### 4.3 `BulkBatchStatus` (Overall batch status)
- `Queued`: Batch created and waiting for background runner.
- `Processing`: Background runner is processing files.
- `Completed`: All files in batch have finished (success, skipped, or failed).
- `Failed`: Fatal batch error.

### 4.4 `MyanmarEncoding`
- `NonMyanmar` (0): Non-Burmese script (e.g. English text).
- `Unicode` (1): Standard Myanmar Unicode script.
- `Zawgyi` (2): Legacy Zawgyi-One encoded Myanmar script.

### 4.5 `SourceChannel`
- `Direct`, `Facebook`, `LinkedIn`, `Telegram`, `Referral`, `ExcelImport`

---

## 5. Detailed Data Contracts & Schemas

### 5.1 Single Resume Extraction DTOs (`RecruitOps.Application.DTOs`)
```csharp
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
    DateTimeOffset ProcessedAt
);
```

### 5.2 Bulk CV Upload DTOs (`RecruitOps.Application.DTOs`)
```csharp
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
    Guid? CreatedApplicationId,
    Guid? CreatedCandidateId
);

public record BulkUploadBatchResponseDto(
    Guid BatchId,
    Guid JobPostingId,
    int TotalFiles,
    string Status, // "Queued", "Processing", "Completed", "Failed"
    DateTimeOffset CreatedAt
);

public record BulkBatchStatusDto(
    Guid BatchId,
    Guid JobPostingId,
    string Status, // "Queued", "Processing", "Completed", "Failed"
    int TotalCount,
    int ProcessedCount,
    int SuccessCount,
    int SkippedCount,
    int FailedCount,
    List<BulkFileItemStatusDto> FileStatuses,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt
);
```

### 5.3 Candidate Profile Update / Confirmation DTO
```csharp
public record UpdateCandidateProfileRequest(
    string FullName,
    string? Email,
    string? Phone,
    int? YearsOfExperience,
    List<string>? Skills
);
```

### 5.4 Shared TypeScript API Types (`packages/types/src/index.ts`)
```typescript
export type BulkFileStatus = 'Queued' | 'Processing' | 'Success' | 'Skipped' | 'Failed';

export interface BulkFileItemStatus {
  fileName: string;
  fileSizeBytes: number;
  status: BulkFileStatus;
  errorMessage?: string | null;
  createdApplicationId?: string | null;
  createdCandidateId?: string | null;
}

export interface BulkUploadBatchResponse {
  batchId: string;
  jobPostingId: string;
  totalFiles: number;
  status: string;
  createdAt: string;
}

export interface BulkBatchStatus {
  batchId: string;
  jobPostingId: string;
  status: string;
  totalCount: number;
  processedCount: number;
  successCount: number;
  skippedCount: number;
  failedCount: number;
  fileStatuses: BulkFileItemStatus[];
  createdAt: string;
  completedAt?: string | null;
}

export interface ParsedContactInfo {
  candidateName?: string | null;
  email?: string | null;
  phone?: string | null;
  yearsOfExperience?: number | null;
  skills: string[];
}

export interface ResumeExtractionResult {
  applicationId: string;
  fileKey: string;
  fileName: string;
  fileSizeBytes: number;
  extractedText: string;
  originalText: string;
  detectedLanguage: string;
  isZawgyiNormalized: boolean;
  parsedContactInfo: ParsedContactInfo;
  processedAt: string;
}

export interface UpdateCandidateProfileRequest {
  fullName: string;
  email?: string | null;
  phone?: string | null;
  yearsOfExperience?: number | null;
  skills?: string[];
}
```

---

## 6. Validation Rules & Constraints Summary

1. **File Size Limit**:
   - Single CV file: Max 10 MB (`10 * 1024 * 1024` bytes).
   - Bulk upload: Max 10 MB per file.
2. **Batch File Limit**:
   - Maximum 50 files per `POST /api/jobpostings/{jobPostingId}/resumes/bulk` request.
3. **Allowed Extensions & MIME Types**:
   - `.pdf` -> `application/pdf`
   - `.docx` -> `application/vnd.openxmlformats-officedocument.wordprocessingml.document`
   - `.png` -> `image/png`
   - `.jpg`, `.jpeg` -> `image/jpeg`
4. **Myanmar Zawgyi Normalization**:
   - Automatic detection of Zawgyi code points on raw extracted text.
   - NFC Unicode conversion via `IMyanmarScriptNormalizer`.
   - Preserve original raw text alongside normalized text.
5. **Candidate Profile Confirmation Gate (ADR-0008 Guardrail 1)**:
   - Extracted PII MUST be presented in a human-review panel for recruiter inspection.
   - Explicit action ("Confirm & Apply to Profile") required to mutate `Candidate` entity fields (`FullName`, `Email`, `Phone`).
6. **Permissive Licensing Guardrail**:
   - Dependencies MUST be MIT, Apache-2.0, or BSD. AGPL / copyleft libraries forbidden.
7. **Storage Abstraction (ADR-0013)**:
   - All file storage operations MUST go through `IFileStorage`. Direct SDK calls prohibited.

---

## 7. Verification Criteria & Test Plan Baseline

- **Backend Test Baseline**: 349 passing tests (`dotnet test backend/RecruitOps.sln`).
- **Frontend Test Baseline**: 233 passing tests (`npm run test` in `frontend/internal`).
- **Typecheck Baseline**: 0 errors (`npm run typecheck`).
