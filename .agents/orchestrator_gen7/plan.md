# Plan: Person A - Flow 1 (Milestones 2 & 3)

## Objective
Execute remaining milestones for Person A - Flow 1:
- Milestone 2: Bulk CV Upload Background Job (Backend API & Background Processing Service)
- Milestone 3: Candidate 360 SlideOver CV Viewer & Parsed Profile UI + Bulk Upload Modal (Frontend)
- Final Verification & Integrity Audit (349+ backend tests, 233+ frontend tests, 0 typecheck errors)

## Milestones Summary

### Milestone 1: CV Resume Storage & Extraction Backend API
- **Status**: COMPLETE (349 backend tests passing)

### Milestone 2: Bulk CV Upload Background Job (Backend)
- **Scope**:
  - `POST /api/jobpostings/{jobPostingId}/resumes/bulk`: Accept up to 50 CV files in a single request multipart batch. Return batch ID.
  - Asynchronous background job runner (Background Service / Queue) processing queued files without blocking HTTP response.
  - File extraction, Zawgyi script normalization, storage in `IFileStorage`, candidate application creation/linking.
  - Per-file status tracking (`Queued`, `Processing`, `Success`, `Skipped`, `Failed`).
  - `GET /api/jobpostings/{jobPostingId}/resumes/bulk/{batchId}`: Return batch summary and per-file progress status.
  - Unit/Integration tests for bulk upload and status endpoints.

### Milestone 3: Candidate 360 SlideOver CV Viewer & Parsed Profile UI (Frontend)
- **Scope**:
  - Update `CandidateSlideOver.tsx` in `@recruitops/internal`:
    - "CV & Documents" tab/section: Drag-and-drop upload zone, progress bar, embedded text viewer, download link/button.
    - "Parsed Profile Human Review" panel: Side-by-side display of extracted text vs editable fields (Name, Email, Phone, Experience, Skills), explicit "Confirm & Save Profile" button.
  - Bulk CV Upload modal on `JobPostingDetailPage`: Drag-and-drop multi-file upload zone (up to 50 files), per-file progress bars linked to batch status API polling.
  - Vitest unit tests for CandidateSlideOver CV tab, Parsed Profile panel, and Bulk Upload modal.
  - `npm run typecheck` 0 errors across `@recruitops/types`, `@recruitops/internal`, `@recruitops/public`.

### Final Verification Gate & Audit
- All backend tests passing (`dotnet test backend/RecruitOps.sln` >= 349)
- All frontend tests passing (`npm run test` in `frontend/internal` >= 233)
- `npm run typecheck` passing with 0 errors
- Forensic Auditor integrity check (CLEAN)
