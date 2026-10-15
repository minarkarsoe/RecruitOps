# Handoff Report: Flow 1 Specification Mining (CV Upload & Text Extraction)

**Agent:** `teamwork_preview_spec_miner`  
**Working Directory:** `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_spec_miner_survey_3`  
**Date:** 2026-08-07

---

## 1. Observation

Direct observations from authoritative project documentation and repository source code:

1. **`ORIGINAL_REQUEST.md` (Lines 131–191)**:
   - Scope: "Person A - Flow 1: Build the complete CV Upload & Local Text Extraction Flow for RecruitOps."
   - Endpoint 1: `POST /api/applications/{id}/resume` — single CV upload (PDF/DOCX/PNG/JPG up to 10MB), stored via `IFileStorage`, extracts text.
   - Endpoint 2: `GET /api/applications/{id}/resume` — download/view stored CV file.
   - Endpoint 3: `POST /api/jobpostings/{jobPostingId}/resumes/bulk` — bulk upload accepting up to 50 files per batch.
   - Endpoint 4: `GET /api/jobpostings/{jobPostingId}/resumes/bulk/{batchId}` — progress tracking endpoint returning batch status (`Queued`, `Processing`, `Success`, `Skipped`, `Failed`).
   - Baseline backend test suite: **333 tests passing** (`dotnet test backend/RecruitOps.sln`). At least 8 new backend tests required.
   - Baseline frontend test suite: **233 tests passing** (`npm run test` in `frontend/internal`), 0 TypeScript errors (`npm run typecheck`). At least 5 new frontend Vitest tests required.

2. **`docs/decisions/ADR-0008-document-extraction-and-ai-profiling.md` (Lines 19–74)**:
   - "Phase 1 (MVP): local text extraction, no network. Extract text in-process, on the customer's server."
   - Extraction inputs: Digital PDF (stream), Word `.docx` (OpenXML), Images JPG/PNG (Local OCR), Scanned PDF (render pages to image -> Local OCR fallback).
   - Guardrail 1: "Human confirmation is mandatory. AI-extracted PII is never written straight into a candidate profile. Show the parse, let a person accept or correct it."
   - Guardrail 3: "Bulk (50 files) must be asynchronous. A background job with per-file status; the spec's Success / Skipped / Canceled summary is the job result."
   - Library selection rule: Disqualifies AGPL libraries (e.g. iText 7 AGPL, pdf2image GPL). Requires permissive (MIT / Apache-2.0 / BSD) licenses.

3. **`docs/decisions/ADR-0009-myanmar-script-handling.md` (Lines 31–39)**:
   - "Every text entry point detects encoding and converts Zawgyi → Unicode before storage... Store both: the normalized Unicode text ... and the original raw text plus a detected_encoding field."
   - Ingest integration: Use `IMyanmarScriptNormalizer` in-process service.

4. **`docs/decisions/ADR-0013-infrastructure-and-storage.md` (Lines 38–46)**:
   - "Storage must sit behind an S3-compatible abstraction: R2 for hosted installs, MinIO (or equivalent local S3) for on-premise. The application must never call R2 APIs directly."

5. **Existing Code Base Interfaces & Endpoints**:
   - `backend/src/Application/Interfaces/IFileStorage.cs` (UploadAsync, DownloadAsync, DeleteAsync, GetPresignedUrlAsync).
   - `backend/src/Application/Interfaces/IMyanmarScriptNormalizer.cs` (Normalize, IsZawgyi, returns `MyanmarScriptNormalizationResult`).
   - `frontend/internal/src/features/pipeline/CandidateSlideOver.tsx` (Candidate 360 drawer with "CV Viewer" tab).

---

## 2. Logic Chain

1. **Premise 1**: The user request and `ORIGINAL_REQUEST.md` mandate Flow 1 (CV Upload & Text Extraction) specification mining covering single CV upload API, resume viewing API, local text extraction (PDF, DOCX, Image OCR fallback), automatic Zawgyi script normalization, bulk CV background processing job (max 50 files), Candidate 360 SlideOver CV viewer tab, parsed profile human review panel, and bulk CV upload modal.
2. **Premise 2**: ADR-0008 enforces local in-process text extraction without external network dependencies, permissive package licensing (no AGPL), and strict human confirmation (parsed profile data pre-fills form, recruiter must explicitly confirm before saving to database).
3. **Premise 3**: ADR-0009 mandates passing all extracted text through `IMyanmarScriptNormalizer`, capturing `IsZawgyiDetected`, `OriginalText`, and `NormalizedText`.
4. **Premise 4**: ADR-0013 requires object storage through `IFileStorage`, supporting both Cloudflare R2 and MinIO without direct vendor SDK calls in API/Application logic.
5. **Conclusion**: Flow 1 specifications are fully mined, formalized, and detailed in `spec_analysis.md`, mapping inputs, outputs, error behaviors, edge cases, DTO schemas, and verification criteria for implementers.

---

## 3. Caveats

- **Burmese OCR Model**: Per ADR-0009, classical OCR on Burmese text is deferred/optional if accuracy is low. Digital PDF stream and DOCX text extraction, however, fully normalize Burmese Zawgyi script to Unicode NFC.
- **Legacy `.doc` Files**: Binary `.doc` files (pre-2007) are not supported by OpenXML parsers and should be rejected at validation with a clear HTTP 400 error message requesting `.docx` or `.pdf`.

---

## 4. Conclusion

Flow 1 specification mining is complete. All functional requirements, non-functional rules, API endpoint signatures, data transfer objects, edge cases, licensing constraints, and test verification criteria have been cataloged in `spec_analysis.md`.

---

## 5. Verification Method

To verify the completeness and compliance of these mined specifications against the codebase:

1. **Specification Report Verification**:
   - Inspect `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_spec_miner_survey_3\spec_analysis.md`.
   - Confirm all 9 discovered features are documented in the Features Discovered table.
   - Confirm all 9 edge cases are documented in the Edge Cases table.
   - Confirm DTO schemas, file limits (10MB), supported MIME types (PDF/DOCX/PNG/JPG), and bulk batch limits (50 files) match `ORIGINAL_REQUEST.md`.

2. **Baseline Verification Commands**:
   - `dotnet test backend/RecruitOps.sln` — 333 tests passing.
   - `npm run test` in `frontend/internal` — 233 tests passing.
   - `npm run typecheck` — 0 errors across workspace.
