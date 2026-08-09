## 2026-08-08T08:05:52Z
You are explorer_m3_1 (teamwork_preview_explorer) for RecruitOps Person A - Flow 1 (Milestone 3).

Working directory: c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_explorer_m3_1_gen7

Your task is to produce the concrete code blueprint and step-by-step implementation specification for Milestone 3: Candidate 360 SlideOver CV Viewer & Parsed Profile UI and Bulk Upload Modal.

MANDATORY INPUTS:
1. Original request: c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\ORIGINAL_REQUEST.md
2. Frontend survey analysis: c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_explorer_survey_2_gen7\analysis.md
3. Spec miner analysis: c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_spec_miner_survey_3_gen7\analysis.md

INVESTIGATE & DESIGN:
1. Shared types in `packages/types/src/index.ts`:
   - Define `BulkResumeUploadResponse`, `BulkResumeBatchStatus`, `BulkFileItemStatus`, `ResumeExtractionResult`, `ParsedContactInfo`, `ConfirmParsedProfileRequest`.
2. API Client in `frontend/internal/src/lib/api.ts`:
   - `apiUpload` helper for FormData requests.
   - `uploadCandidateResume`, `getBulkResumeStatus`, `postBulkResumes`, `confirmParsedProfile`.
3. `CandidateSlideOver.tsx` (`frontend/internal/src/features/pipeline/CandidateSlideOver.tsx`):
   - CV & Documents Tab: Drag-and-drop single file upload zone, upload progress bar, embedded text viewer with `Zawgyi → Unicode Normalized` badge, download button calling `GET /api/applications/{id}/resume`.
   - Parsed Profile Human Review Panel: Side-by-side or stacked view displaying extracted text alongside editable profile fields (Name, Email, Phone, Experience, Skills), requiring explicit recruiter click on "Confirm & Apply to Profile" button before calling `confirmParsedProfile`.
4. `BulkCvUploadModal` on `JobPostingDetailPage.tsx` (`frontend/internal/src/pages/JobPostingDetailPage.tsx`):
   - Add "Bulk Upload CVs" button in Pipeline card header.
   - `BulkCvUploadModal` component using `@recruitops/ui` `Dialog` primitive.
   - Multi-file drag-and-drop upload zone (up to 50 files).
   - Calls `postBulkResumes(jobPostingId, files)`.
   - Polls `getBulkResumeStatus` while batch is queued/processing.
   - Displays progress bar and per-file status badges (`Queued`, `Processing`, `Success`, `Skipped`, `Failed`).
5. Vitest Unit Test Strategy in `frontend/internal/src/features/pipeline/__tests__/CandidateSlideOver.test.tsx` and `frontend/internal/src/pages/__tests__/JobPostingDetailPage.test.tsx`:
   - Testing CV tab drag-and-drop upload & progress bar.
   - Testing parsed profile editing and recruiter confirmation button click.
   - Testing Bulk CV Upload modal multi-file selection, upload trigger, status polling, and progress bar rendering.

OUTPUT REQUIREMENTS:
Write detailed implementation blueprint to `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_explorer_m3_1_gen7\analysis.md` and handoff report to `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_explorer_m3_1_gen7\handoff.md`.
Send message to parent when done. Do NOT edit source code directly.
