# Handoff Report: Milestone 3 Candidate 360 SlideOver CV Viewer & Bulk Upload Modal Blueprint

**Agent ID**: `explorer_m3_1` (teamwork_preview_explorer)  
**Working Directory**: `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_explorer_m3_1_gen7`  
**Milestone**: Milestone 3 (Person A - Flow 1)  
**Date**: 2026-08-08  

---

## 1. Observation

1. **Mandatory Inputs**:
   - `ORIGINAL_REQUEST.md`: Milestone 3 scope defines Candidate 360 SlideOver CV Viewer & Parsed Profile UI, drag-and-drop upload zone, progress bar, embedded text viewer, Parsed Profile Human Review panel requiring explicit recruiter confirmation, and Bulk CV Upload modal on `JobPostingDetailPage`.
   - `teamwork_preview_explorer_survey_2_gen7/analysis.md`: Line reference analysis of `CandidateSlideOver.tsx` (lines 173–194 placeholder tab content), `JobPostingDetailPage.tsx` (lines 308–393 pipeline card container), and `@recruitops/ui` primitives (`Sheet`, `Dialog`, `Badge`, `Tabs`, `Button`, `Input`).
   - `teamwork_preview_spec_miner_survey_3_gen7/analysis.md`: Detailed DTO schemas, status enums (`Queued`, `Processing`, `Success`, `Skipped`, `Failed`), validation limits (max 10MB per file, max 50 files per bulk upload batch), and ADR requirements (ADR-0008 human confirmation gate, ADR-0009 Zawgyi normalization badge indicator).

2. **Existing Codebase Inspection**:
   - `packages/types/src/index.ts` (lines 1-832): Shared type definitions currently lack single CV extraction DTOs (`ResumeExtractionResult`, `ParsedContactInfo`, `ConfirmParsedProfileRequest`) and bulk processing DTOs (`BulkResumeUploadResponse`, `BulkResumeBatchStatus`, `BulkFileItemStatus`, `BulkFileStatus`).
   - `frontend/internal/src/lib/api.ts` (lines 1-152): `apiFetch` hardcodes `'Content-Type': 'application/json'`. File upload requests require `apiUpload` to omit `Content-Type` so `fetch` automatically sets `multipart/form-data; boundary=...`. Missing `resumeApi` namespace methods.
   - `frontend/internal/src/features/pipeline/CandidateSlideOver.tsx` (lines 173-194): Currently contains static placeholder for CV Viewer tab (`{candidate.candidateName}_Resume.pdf`).
   - `frontend/internal/src/pages/JobPostingDetailPage.tsx` (lines 308-393): Card header displays `Pipeline · {pipeline.length} candidates` without bulk CV upload action button.
   - `@recruitops/ui` primitives in `packages/ui/src/index.ts`: Exports `Dialog`, `DialogHeader`, `DialogTitle`, `DialogDescription`, `DialogBody`, `DialogFooter`, `Sheet`, `Tabs`, `Badge`, `Button`, `Input`, `Select`, `StatusPill`.
   - `frontend/internal/src/features/pipeline/pipeline.test.tsx`: Existing Vitest suite baseline passes cleanly.

---

## 2. Logic Chain

1. **Observation 1 & 2**: `CandidateSlideOver.tsx` currently renders a placeholder block in the `cv` tab. ADR-0008 requires an in-process document text extractor preview and an explicit human review confirmation gate.
2. **Logic Step 1**: Replacing lines 173-194 in `CandidateSlideOver.tsx` with `CvAndDocumentsTab` introduces drag-and-drop single-file upload, upload progress indicator, scrollable text viewer with a `Zawgyi → Unicode Normalized` badge, and a side-by-side editable Parsed Profile Human Review form.
3. **Logic Step 2**: To ensure recruiters inspect parsed fields before candidate entity mutation (per ADR-0008 Guardrail 1), the "Confirm & Apply to Profile" button must explicitly trigger `resumeApi.confirmParsedProfile` (`PUT /api/applications/{id}/profile`), rather than auto-applying raw parsed data upon upload.
4. **Observation 1 & 2**: `JobPostingDetailPage.tsx` pipeline card header needs bulk ingest capabilities for up to 50 CVs per batch.
5. **Logic Step 3**: Adding a "Bulk Upload CVs" button to line 309 in `JobPostingDetailPage.tsx` opens `BulkCvUploadModal`. This modal uses the `@recruitops/ui` `Dialog` primitive, enforces a 50-file boundary, initiates batch processing via `postBulkResumes`, and polls `getBulkResumeStatus` every 2 seconds to render live per-file status badges (`Queued`, `Processing`, `Success`, `Skipped`, `Failed`).
6. **Observation 2**: `apiFetch` cannot handle FormData because `'Content-Type': 'application/json'` is set by default.
7. **Logic Step 4**: Adding `apiUpload<T>` helper and `resumeApi` methods in `frontend/internal/src/lib/api.ts` enables clean FormData handling with silent refresh token support.

---

## 3. Caveats

- **Network Dependency**: OCR scanning fallback for image CVs operates in-process via local document extractor; no external paid OCR cloud API is invoked.
- **Polling Interval**: Bulk status polling is set to 2 seconds (`setInterval`), which balances real-time UI updates with server load. Polling stops automatically when batch status reaches a terminal state (`Completed`, `PartialSuccess`, or `Failed`).

---

## 4. Conclusion

The specification and code blueprint documented in `analysis.md` provide a complete, robust, and zero-regression design for Milestone 3 (Candidate 360 CV Viewer & Parsed Profile UI and Bulk CV Upload Modal). All shared DTO contracts, API helpers, UI component layouts, confirmation workflows, and Vitest test strategies are fully defined and ready for implementation.

---

## 5. Verification Method

To verify the implementation once applied by the implementer:

1. **Type Checking**:
   - Command: `npm run typecheck` across all workspaces.
   - Invalidation Condition: Any TypeScript error in `@recruitops/types`, `@recruitops/ui`, or `@recruitops/internal`.

2. **Frontend Unit Test Suite**:
   - Command: `npm run test` in `frontend/internal`.
   - Invalidation Condition: Any failing test out of the existing 233 baseline tests or new Milestone 3 tests in `CandidateSlideOver.test.tsx` and `JobPostingDetailPage.test.tsx`.

3. **Files to Inspect**:
   - `packages/types/src/index.ts`
   - `frontend/internal/src/lib/api.ts`
   - `frontend/internal/src/features/pipeline/CandidateSlideOver.tsx`
   - `frontend/internal/src/features/pipeline/BulkCvUploadModal.tsx`
   - `frontend/internal/src/pages/JobPostingDetailPage.tsx`
   - `frontend/internal/src/features/pipeline/__tests__/CandidateSlideOver.test.tsx`
   - `frontend/internal/src/pages/__tests__/JobPostingDetailPage.test.tsx`
