# Handoff Report — Specification Mining for Milestones 2 & 3 (Person A - Flow 1)

**Agent:** `survey_3` (teamwork_preview_spec_miner)  
**Working Directory:** `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_spec_miner_survey_3_gen7`  
**Date:** 2026-08-08  

---

## 1. Observation

- **Dispatched Scope**: Mine precise specifications and ADR requirements for RecruitOps Person A - Flow 1 (Milestones 2 & 3: Bulk CV Upload Background Job & Candidate 360 CV Viewer / Parsed Data UI).
- **Mandatory Input**: `ORIGINAL_REQUEST.md` (lines 196–228: "Person A - Flow 1 (Milestone 2 & 3): Resume work to complete the remaining parts of Person A Flow 1...").
- **Key Architectural Decisions Inspected**:
  - `docs/decisions/ADR-0008-document-extraction-and-ai-profiling.md`: In-process local text extraction (Phase 1 MVP), human confirmation gate (Guardrail 1), background async job for bulk upload up to 50 files (Guardrail 3), permissive licensing requirement (MIT/Apache-2.0/BSD).
  - `docs/decisions/ADR-0009-myanmar-script-handling.md`: Ingest normalization of Zawgyi script to canonical Unicode (NFC) via `IMyanmarScriptNormalizer`, storing both raw and normalized text with detection metadata, optional/deferred Burmese OCR.
  - `docs/decisions/ADR-0013-infrastructure-and-storage.md`: Object storage abstraction via `IFileStorage` for S3-compatible backends (Cloudflare R2 for hosted / MinIO for on-premise), PostgreSQL JSONB for custom fields.
  - `docs/decisions/ADR-0016-login-brute-force-protection.md`: Two-axis login rate limiting & throttle policies, refresh token authentication mechanism.
- **Existing Codebase State**:
  - Backend: `IResumeService` and `ApplicationsController` currently implement single CV upload (`POST /api/applications/{id}/resume`) and download (`GET /api/applications/{id}/resume`) with `IDocumentTextExtractor` and `IMyanmarScriptNormalizer`. Baseline: 349 backend tests passing.
  - Frontend: `CandidateSlideOver.tsx` currently has basic tabs (Overview, CV Viewer, Stage History, Scorecards, Notes) but lacks the drag-and-drop upload zone, progress bar, embedded text viewer, and Parsed Profile Human Review panel. Baseline: 233 frontend tests passing, 0 TypeScript errors.

---

## 2. Logic Chain

1. **Observation**: `ORIGINAL_REQUEST.md` lines 207–218 specify two key remaining technical requirements for Flow 1:
   - R2 (Milestone 2): `POST /api/jobpostings/{jobPostingId}/resumes/bulk` accepting up to 50 files, processing asynchronously using a background runner, and returning progress summary via `GET /api/jobpostings/{jobPostingId}/resumes/bulk/{batchId}`.
   - R3 (Milestone 3): `CandidateSlideOver.tsx` update in `@recruitops/internal` adding "CV & Documents" tab with drag-and-drop zone, progress bar, embedded CV text viewer/download button, Parsed Profile Human Review panel requiring recruiter confirmation, and Bulk CV Upload modal on `JobPostingDetailPage`.
2. **Observation**: ADR-0008 Guardrail 1 mandates that AI/heuristic parsed PII must never be written directly to a candidate profile without explicit human confirmation.
3. **Inference**: The Candidate Profile Confirmation flow requires a dedicated data contract (`UpdateCandidateProfileRequest`) and UI state in `CandidateSlideOver.tsx` where extracted data (`CandidateName`, `Email`, `Phone`, `YearsOfExperience`, `Skills`) is pre-filled into editable fields side-by-side with extracted text, and only committed upon clicking "Confirm & Apply to Profile".
4. **Observation**: Validation rules across ADRs and codebase stipulate max 10MB per file, max 50 files per bulk upload batch, allowed MIME types (`.pdf`, `.docx`, `.png`, `.jpg`, `.jpeg`), and automatic Zawgyi->Unicode NFC script conversion.
5. **Conclusion**: All technical specifications, status enums (`PipelineStatus`, `BulkFileStatus`, `BulkBatchStatus`, `MyanmarEncoding`), data contracts (Single & Bulk DTOs, TypeScript interfaces), validation rules, edge cases, and verification criteria have been mined, formalized, and written to `analysis.md`.

---

## 3. Caveats

- **Burmese OCR Engine**: As noted in ADR-0009, Burmese OCR accuracy on scanned images is deferred; the system uses digital PDF stream parsing, OpenXML DOCX parsing, and basic local image OCR fallback, operating cleanly without relying on Burmese OCR accuracy.
- **AI Integration**: AI-assisted profiling (Phase 2 LLM structuring) remains optional behind an API key per ADR-0008; Phase 1 heuristic parsing is the baseline authority.

---

## 4. Conclusion

The specification mining for RecruitOps Person A - Flow 1 (Milestones 2 & 3) is complete. `analysis.md` provides an exhaustive reference covering:
- Features Discovered (10 distinct features across API, domain, pipeline, and UI).
- Edge Cases & Boundary Conditions (10 documented scenario behaviors).
- Complete Data Contracts (C# DTOs and TypeScript interfaces).
- Validation Rules (10MB file limit, 50 file batch limit, MIME types, Zawgyi normalization, confirmation gate).
- Status Enums (`PipelineStatus`, `BulkFileStatus`, `BulkBatchStatus`, `MyanmarEncoding`).
- Test Verification Criteria (349+ backend tests, 233+ frontend tests, 0 TS typecheck errors).

---

## 5. Verification Method

To verify the mined specifications:
1. Inspect `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_spec_miner_survey_3_gen7\analysis.md` for complete specification tables, data contracts, and validation rules.
2. Cross-reference against `ORIGINAL_REQUEST.md`, `docs/decisions/ADR-0008-document-extraction-and-ai-profiling.md`, `docs/decisions/ADR-0009-myanmar-script-handling.md`, and `docs/decisions/ADR-0013-infrastructure-and-storage.md`.
3. Confirm test suite baselines:
   - Backend: `dotnet test backend/RecruitOps.sln` (349 tests passing).
   - Frontend: `npm run test` in `frontend/internal` (233 tests passing).
   - Typecheck: `npm run typecheck` (0 errors).
