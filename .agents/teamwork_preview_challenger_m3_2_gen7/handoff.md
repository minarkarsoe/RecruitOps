# Milestone 3 Empirical Challenge Report

**Verdict**: `APPROVE`

## 1. Observation

- **Implementation Inspection**:
  - `frontend/internal/src/features/pipeline/CandidateSlideOver.tsx`:
    - **Single CV Upload & Progress Bar**: Lines 94-132 implement `handleFileUpload(file: File)`. File size > 10MB sets error `File size exceeds maximum limit of 10MB.` (lines 95-97). Invalid extension check (`.pdf`, `.docx`, `.png`, `.jpg`, `.jpeg`) sets error `Invalid file format. Allowed formats: PDF, DOCX, PNG, JPG, JPEG.` (lines 99-104). Progress state `uploadProgress` is updated to 30%, 60%, 100% and progress bar is conditionally rendered (lines 230-243).
    - **Parsed Profile Editing & Recruiter Confirmation**: Lines 85-92 initialize candidate form state (`candidateName`, `email`, `phone`, `yearsOfExperience`, `skills`). Editing input fields updates local state. `resumeApi.confirmParsedProfile` is ONLY called when recruiter clicks the "Confirm & Apply to Profile" button (lines 134-156). Missing `candidateName` sets error `Candidate Name is required.` without API call (lines 135-138).
    - **Zawgyi Script Normalization Badge**: Lines 260-262 conditionally render `<Badge variant="cyan">Zawgyi → Unicode Normalized</Badge>` when `extractionResult?.isZawgyiNormalized` evaluates to true.
- **Empirical Stress Test Execution**:
  - Created `frontend/internal/src/features/pipeline/__tests__/CandidateSlideOverChallengerM3.test.tsx` containing 9 stress test cases:
    1. `renders upload progress bar during active file extraction and displays success result` — PASSED.
    2. `displays error when uploading a file exceeding 10MB size limit` — PASSED.
    3. `displays error when uploading a file with an invalid file format extension` — PASSED.
    4. `handles network / API rejection during upload and displays error message` — PASSED.
    5. `allows editing Name, Email, Phone, Experience, Skills without triggering API until explicit button click` — PASSED (verified zero API calls during input changes; confirmed payload passed on explicit click).
    6. `requires Candidate Name and shows error if Candidate Name is blank on confirmation` — PASSED.
    7. `allows removing skills from the parsed profile skills list before confirming` — PASSED.
    8. `renders "Zawgyi → Unicode Normalized" badge when isZawgyiNormalized is true` — PASSED.
    9. `does NOT render "Zawgyi → Unicode Normalized" badge when isZawgyiNormalized is false` — PASSED.
- **Suite Command Outputs**:
  - `npm run typecheck` exited with code 0 across `@recruitops/internal`, `@recruitops/public`, `@recruitops/types`, and `@recruitops/ui`.
  - `npm run test` in `frontend/internal` exited with code 0: 28 test files passed, 248 tests passed total.

## 2. Logic Chain

1. **Requirement 1 (Single CV Upload & Error Handling)**: Verified via `CandidateSlideOverChallengerM3.test.tsx` that single file uploads trigger progress bar updates, reject files > 10MB or invalid extensions without calling API, and display extraction errors cleanly if the network call fails.
2. **Requirement 2 (Parsed Profile Editing & Recruiter Click Requirement)**: Verified empirically that typing into Name, Email, Phone, Experience, or Skills input fields updates local React state without invoking `resumeApi.confirmParsedProfile`. Verified that clicking "Confirm & Apply to Profile" sends the complete updated profile payload. Blank candidate name triggers validation error before network call.
3. **Requirement 3 (Zawgyi Normalization Badge)**: Verified that `<Badge variant="cyan">Zawgyi → Unicode Normalized</Badge>` renders if and only if `isZawgyiNormalized` is `true`.
4. **Requirement 4 (Build & Typecheck)**: `npm run typecheck` returned 0 errors across all workspaces and `npm run test` returned 248 passing tests in `frontend/internal`.

## 3. Caveats

No caveats. All component interaction paths, error boundaries, badge rendering conditions, form submission requirements, and workspace typechecks were empirically executed and verified.

## 4. Conclusion

Candidate 360 SlideOver CV Viewer & Parsed Profile Human Review panel meets all functional, UI, error handling, and type safety requirements.

**Explicit Verdict**: `APPROVE`

## 5. Verification Method

To independently verify these findings:

1. Run TypeScript typecheck:
   ```bash
   npm run typecheck
   ```
   Must complete with 0 errors across all workspaces.

2. Run frontend internal test suite:
   ```bash
   cd frontend/internal
   npm run test
   ```
   Must pass all 28 test files and 248 tests.
