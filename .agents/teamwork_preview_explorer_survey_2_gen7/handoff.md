# Handoff Report: Milestone 3 Frontend Survey & Component Design

## 1. Observation
- **Candidate 360 SlideOver**:
  - File: `frontend/internal/src/features/pipeline/CandidateSlideOver.tsx` (300 lines).
  - Observed Tab 2 (lines 173–194): Static placeholder rendering `CV Document Preview` and `{candidate.candidateName}_Resume.pdf`.
  - Tab Triggers: Lines 109–121 render 5 tabs (`overview`, `cv`, `history`, `scorecards`, `notes`).
- **Job Posting Detail Page**:
  - File: `frontend/internal/src/pages/JobPostingDetailPage.tsx` (398 lines).
  - Candidate pipeline list rendered at lines 308–393 under `<Card> Pipeline · {pipeline.length} candidates </Card>`. Currently has no bulk upload trigger.
- **UI Primitives**:
  - `packages/ui/src/Sheet.tsx`: Exports `Sheet`, `SheetHeader`, `SheetTitle`, `SheetBody`, `SheetFooter`.
  - `packages/ui/src/Dialog.tsx`: Exports `Dialog`, `DialogHeader`, `DialogTitle`, `DialogDescription`, `DialogBody`, `DialogFooter`.
  - `packages/ui/src/Tabs.tsx`: Exports `Tabs`, `TabsList`, `TabsTrigger`, `TabsContent`.
  - `packages/ui/src/Button.tsx`, `Badge.tsx`, `StatusPill.tsx`, `Input.tsx`, `Select.tsx`.
- **Shared Types & API Layer**:
  - File: `packages/types/src/index.ts` (832 lines).
  - API Client: `frontend/internal/src/lib/api.ts` (152 lines) exports `apiFetch<T>` and `aiApi`.
- **Backend Enpoints (Milestone 1 & 2)**:
  - `POST /api/applications/{id}/resume` (Single CV upload & extraction, returns `ResumeExtractionResultDto`).
  - `GET /api/applications/{id}/resume` (Streams CV document file).
  - `POST /api/jobpostings/{jobPostingId}/resumes/bulk` (Accepts up to 50 CV files, returns batch tracking ID).
  - `GET /api/jobpostings/{jobPostingId}/resumes/bulk/{batchId}` (Returns batch status and per-file progress list).
- **Test Suite Status**:
  - Backend: 349 tests passing (`dotnet test backend/RecruitOps.sln`).
  - Frontend: 233 tests passing (`npm run test` in `frontend/internal`).
  - Typecheck: 0 errors across all workspaces (`npm run typecheck`).

---

## 2. Logic Chain
1. **Observation**: `CandidateSlideOver.tsx` Tab 2 ("cv") is currently a static placeholder without drag-and-drop file upload, progress bar, or text viewer.
   **Reasoning**: To fulfill Requirement R3, Tab 2 must be upgraded with `CvAndDocumentsTab` supporting single file drag-and-drop, progress indication during upload to `POST /api/applications/{id}/resume`, raw text rendering with a `Zawgyi → Unicode Normalized` badge, and download action via `GET /api/applications/{id}/resume`.
2. **Observation**: Recruiter review of extracted candidate data requires side-by-side verification before updating candidate records.
   **Reasoning**: A `ParsedProfileReviewPanel` needs to sit alongside or below the extracted text viewer. It prepopulates editable fields (`candidateName`, `email`, `phone`, `yearsOfExperience`, `skills`) from `ResumeExtractionResult.parsedContactInfo` and submits recruiter-confirmed data via `PUT /api/applications/{id}/profile`.
3. **Observation**: `JobPostingDetailPage.tsx` displays the application pipeline but lacks a bulk CV upload entry point.
   **Reasoning**: A "Bulk Upload CVs" button should be added to the Pipeline card header. Clicking it opens `BulkCvUploadModal` built using the `@recruitops/ui` `Dialog` primitive.
4. **Observation**: `apiFetch` in `frontend/internal/src/lib/api.ts` enforces `Content-Type: application/json`.
   **Reasoning**: `FormData` uploads (used for single and bulk CV multipart requests) require omitting the explicit `Content-Type` header so `fetch` automatically formats `multipart/form-data; boundary=...`. A dedicated `apiUpload` helper should be added to `api.ts`.
5. **Observation**: `@recruitops/types` needs DTO definitions mirroring backend single and bulk CV extraction models.
   **Reasoning**: Adding `ResumeExtractionResult`, `ParsedContactInfo`, `ConfirmParsedProfileRequest`, `BulkResumeUploadResponse`, `BulkResumeBatchStatus`, `BulkFileItemStatus`, and `BulkFileStatus` ensures strict TypeScript end-to-end type safety across packages.

---

## 3. Caveats
- **Read-Only Mode**: No source files (`.ts`, `.tsx`, `.cs`) were modified during this survey.
- **Backend Endpoint Availability**: Milestone 1 endpoints (`POST/GET /applications/{id}/resume`) are verified and passing tests. Milestone 2 bulk endpoints (`POST/GET /jobpostings/{id}/resumes/bulk`) are specified and ready for frontend integration.
- **Progress Bar Primitive**: `packages/ui` does not currently export a standalone `<Progress />` primitive, but Tailwind progress bars (`bg-line-200` container + `bg-primary-600` width fill) fulfill all UI needs cleanly without extra dependencies.

---

## 4. Conclusion
The survey confirms that the codebase is cleanly architected for Milestone 3 frontend implementation:
- `CandidateSlideOver.tsx` can be cleanly refactored by replacing the placeholder CV tab with `CvAndDocumentsTab` and `ParsedProfileReviewPanel`.
- `JobPostingDetailPage.tsx` can cleanly host `BulkCvUploadModal` using the existing `@recruitops/ui` `Dialog` primitive.
- `@recruitops/types` and `frontend/internal/src/lib/api.ts` have clear extension points for single and bulk CV upload APIs.
- Comprehensive analysis with technical specs, component layouts, and Vitest test strategy has been recorded in `analysis.md`.

---

## 5. Verification Method
1. **Inspect Analysis Report**: Verify `analysis.md` in `.agents/teamwork_preview_explorer_survey_2_gen7/analysis.md`.
2. **Verify Type Alignment**: Run `npm run typecheck` across all workspaces.
3. **Verify Baseline Test Suite**: Run `npm run test` in `frontend/internal` (233 passing tests) and `dotnet test backend/RecruitOps.sln` (349 passing tests).
4. **Invalidation Conditions**:
   - Any TypeScript error introduced when implementing proposed DTOs.
   - Any broken tests in existing frontend suite.
   - Failure of `BulkCvUploadModal` to handle multi-file drag-and-drop batches up to 50 files.
