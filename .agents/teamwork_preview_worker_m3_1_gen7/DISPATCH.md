## 2026-08-08T15:06:45Z
You are worker_m3_1 (teamwork_preview_worker) for RecruitOps Person A - Flow 1 (Milestone 3).

Working directory: c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_worker_m3_1_gen7

MANDATORY INTEGRITY WARNING:
DO NOT CHEAT. All implementations must be genuine. DO NOT hardcode test results, create dummy/facade implementations, or circumvent the intended task. A teamwork_preview_auditor will independently verify your work. Integrity violations WILL be detected and your work WILL be rejected.

MANDATORY INPUTS:
1. Original request: c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\ORIGINAL_REQUEST.md
2. Explorer blueprint: c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_explorer_m3_1_gen7\analysis.md
3. Spec miner analysis: c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_spec_miner_survey_3_gen7\analysis.md

YOUR TASK:
Implement Milestone 3 (Candidate 360 SlideOver CV Viewer & Parsed Profile UI, Bulk Upload Modal):

1. Update `packages/types/src/index.ts`:
   - Add interfaces: `BulkResumeUploadResponse`, `BulkResumeBatchStatus`, `BulkFileItemStatus`, `ResumeExtractionResult`, `ParsedContactInfo`, `ConfirmParsedProfileRequest`, and enum `BulkFileStatus`.

2. Update `frontend/internal/src/lib/api.ts`:
   - Implement `apiUpload<T>(path: string, formData: FormData)` helper for FormData uploads.
   - Add `resumeApi` methods: `uploadCandidateResume(applicationId: string, file: File)`, `getBulkResumeStatus(jobPostingId: string, batchId: string)`, `postBulkResumes(jobPostingId: string, files: File[])`, `confirmParsedProfile(applicationId: string, profileData: ConfirmParsedProfileRequest)`.

3. Refactor `frontend/internal/src/features/pipeline/CandidateSlideOver.tsx`:
   - Update "CV & Documents" tab:
     - Drag-and-drop single CV upload zone + progress bar during upload.
     - Embedded text viewer displaying raw extracted text with `Zawgyi → Unicode Normalized` badge when normalized.
     - Download button linking to `GET /api/applications/{id}/resume`.
   - Add "Parsed Profile Human Review" panel:
     - Side-by-side or stacked view displaying extracted text alongside editable fields (Candidate Name, Email, Phone, Years of Experience, Skills).
     - Explicit "Confirm & Apply to Profile" button that calls `confirmParsedProfile` upon click.

4. Create `frontend/internal/src/features/pipeline/BulkCvUploadModal.tsx` and update `frontend/internal/src/pages/JobPostingDetailPage.tsx`:
   - Add "Bulk Upload CVs" button in Pipeline card header on `JobPostingDetailPage`.
   - Implement `BulkCvUploadModal` using `@recruitops/ui` `Dialog` primitive.
   - Multi-file drag-and-drop zone (up to 50 files).
   - Calls `postBulkResumes(jobPostingId, files)`.
   - Polls `getBulkResumeStatus` every 1-2 seconds while batch is queued/processing.
   - Displays progress bar (`processedFiles / totalFiles`) and per-file status badges (`Queued`, `Processing`, `Success`, `Skipped`, `Failed`).

5. Unit Tests:
   - Create unit test files in `frontend/internal/src/features/pipeline/__tests__/CandidateSlideOver.test.tsx` and `frontend/internal/src/pages/__tests__/BulkCvUploadModal.test.tsx` testing the CV tab upload, parsed profile confirmation button, and bulk upload modal progress tracking.

6. Verification:
   - Run `npm run typecheck` across all workspaces (must pass with 0 errors).
   - Run `npm run test` in `frontend/internal` (must pass with 0 failures, all 233+ tests passing).

OUTPUT REQUIREMENTS:
Write your implementation report to `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_worker_m3_1_gen7\changes.md` and handoff report to `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_worker_m3_1_gen7\handoff.md`.
Send message to parent when complete.
