# Technical Investigation Analysis Report: Flow 1 (CV Upload & Local Text Extraction Flow)

## Executive Summary
This report presents the frontend architecture investigation for implementing **Flow 1: CV Upload & Local Text Extraction Flow** in `@recruitops/internal` and supporting packages (`@recruitops/ui`, `@recruitops/types`). The investigation examined the existing codebase, candidate slide-over drawer, job posting detail page, API client patterns, UI primitives, and Vitest test suite.

All existing quality baselines were verified:
- **TypeScript Typecheck (`npm run typecheck`)**: 0 errors across all workspace packages.
- **Frontend Test Suite (`npm run test`)**: **233 / 233 tests passing** cleanly in Vitest.

---

## 1. Candidate 360 Profile & Slide-Over Drawer Analysis
### File Location
`frontend/internal/src/features/pipeline/CandidateSlideOver.tsx`

### Current Props & Contract
```typescript
export interface CandidateSlideOverProps {
  candidate: PipelineItem | null;
  isOpen: boolean;
  onClose: () => void;
  stageHistory?: StageHistoryItem[];
  interviews?: Interview[];
  onOpenScorecard?: (interviewId: string) => void;
  onMoveStage?: (applicationId: string, toStatus: any) => Promise<void>;
  applicationFormFieldsJson?: string | null;
  initialTab?: string;
  className?: string;
}
```

### Existing Tab Structure
1. `overview`: Candidate Profile Summary, Cover Letter, Custom Form Answers.
2. `cv`: **Static placeholder preview** showing `{candidate.candidateName}_Resume.pdf` with static placeholder text (lines 172-194).
3. `history`: Timeline of recruitment stage transitions.
4. `scorecards`: Interview rounds & panel evaluation summaries.
5. `notes`: Candidate application notes & debrief thread.

### Required Enhancements for Flow 1
The `cv` tab must be expanded into a comprehensive **"CV & Documents"** and **"Parsed Profile Review"** surface with:
1. **Drag-and-Drop Dropzone**: For uploading CV files (PDF, DOCX, PNG, JPG up to 10MB) directly attached to the candidate application (`POST /api/applications/{id}/resume`).
2. **Upload Progress Bar**: Visual progress indicator showing upload status and extraction state (`Uploading`, `Extracting text`, `Done`, `Failed`).
3. **Embedded CV Text Viewer**: Render the extracted plain text with a badge indicating whether Zawgyi Myanmar script normalization (`isZawgyiNormalized: true`) was performed.
4. **Side-by-Side Parsed Profile Human Review Panel**:
   - **Left Column**: Extracted text viewer with key extracted fields highlighted (Full Name, Email, Phone, Skills, Experience summary).
   - **Right Column**: Editable candidate profile form pre-populated with extracted values.
   - **Confirmation Action**: Explicit "Confirm & Apply to Candidate Profile" button that updates the candidate record only after recruiter approval.

---

## 2. JobPostingDetailPage & Bulk CV Upload Analysis
### File Location
`frontend/internal/src/pages/JobPostingDetailPage.tsx`

### Current Page Layout
- **Header**: Back link, posting title, status pill, department, approved requisition link.
- **Advert Section**: Editable job posting details & custom form builder.
- **Publishing Section**: Public share link and status controls.
- **Pipeline Section**: List of candidates with stage movement dropdowns and interview debrief drawers.

### Required Enhancements for Flow 1
1. **Bulk Upload Action**: Add a `"Bulk CV Upload"` button in the Pipeline section header of `JobPostingDetailPage.tsx`.
2. **Bulk CV Upload Modal Component (`BulkCvUploadModal.tsx`)**:
   - Built using `@recruitops/ui` `Dialog` primitive (`size="xl"`).
   - Multi-file drag-and-drop dropzone supporting up to 50 files per batch (`POST /api/jobpostings/{jobPostingId}/resumes/bulk`).
   - Batch progress tracker displaying overall completion % bar and individual file progress rows (polling `GET /api/jobpostings/{jobPostingId}/resumes/bulk/{batchId}`).
   - Real-time status badges per file: `Queued`, `Processing`, `Success`, `Skipped`, `Failed`.

---

## 3. API Integration & Type Contracts
### API Client (`frontend/internal/src/lib/api.ts`)
- **Current Behavior**: `apiFetch<T>` automatically attaches `Authorization` header and defaults to `'Content-Type': 'application/json'`.
- **Requirement for Multipart Form Data**: Standard JSON fetch sets `application/json`. For file uploads (`FormData`), an `apiUpload<T>(path, formData)` helper must be used, which omits the default `'Content-Type'` header so the browser sets `multipart/form-data; boundary=...`.

### Type Definitions (`packages/types/src/index.ts`)
Existing AI DTOs cover `ParseResumeRequest` and `ParsedResumeResult`. For Flow 1 direct storage & bulk API, the following types must be defined in `@recruitops/types`:

```typescript
export interface ResumeExtractionResult {
  applicationId: string;
  fileName: string;
  contentType: string;
  fileSizeBytes: number;
  extractedText: string;
  detectedLanguage: string;
  isZawgyiNormalized: boolean;
  parsedContactInfo: ParsedContactInfo;
  uploadedAt: string;
}

export interface ParsedContactInfo {
  fullName?: string;
  email?: string;
  phone?: string;
  skills?: string[];
  experienceSummary?: string;
}

export interface BulkResumeUploadResponse {
  batchId: string;
  totalFiles: number;
  queuedFiles: number;
  message: string;
}

export interface BulkBatchStatusItem {
  fileName: string;
  status: 'Queued' | 'Processing' | 'Success' | 'Skipped' | 'Failed';
  applicationId?: string;
  candidateName?: string;
  errorMessage?: string;
}

export interface BulkBatchStatusResponse {
  batchId: string;
  totalFiles: number;
  processedCount: number;
  successCount: number;
  failedCount: number;
  isCompleted: boolean;
  items: BulkBatchStatusItem[];
}
```

---

## 4. UI Primitives Inventory & Gaps
### Available Primitives (`packages/ui`)
- `Sheet`, `SheetHeader`, `SheetTitle`, `SheetDescription`, `SheetBody`, `SheetFooter` (Slide-over drawer)
- `Dialog`, `DialogHeader`, `DialogTitle`, `DialogDescription`, `DialogBody`, `DialogFooter` (Modal dialog)
- `Tabs`, `TabsList`, `TabsTrigger`, `TabsContent` (Tab navigation)
- `Button`, `Input`, `Select`, `Card`, `Badge`, `StatusPill`, `Skeleton`

### Gaps to Bridge in UI Library / Components
1. **`Progress` / `ProgressBar`**: Currently missing from `packages/ui`. Need a linear progress bar component supporting percentage (`value={percentage}`) and color variants.
2. **`FileUploadZone`**: A reusable drag-and-drop file upload zone component with active hover states, drag-over highlights, format validation, and max file size warning.

---

## 5. Vitest Test Suite Baseline & Strategy
### Current Test Status
- Test runner: Vitest v2.1.9 with `jsdom` environment.
- Config: `vitest.config.ts` and setup file `src/test/setup.ts`.
- Execution command: `npm run test` in `frontend/internal`.
- Baseline result: **233 tests passing (25 test files)**.

### Target Test Coverage for Flow 1
Per task criteria (minimum 5 new frontend Vitest tests required):
1. **`CandidateSlideOver` CV Tab Upload Test**: Verify dropzone interaction, progress bar display, extracted text rendering, and Zawgyi normalization indicator.
2. **Parsed Profile Human Review Test**: Verify side-by-side editable form pre-fills from extracted text, allows recruiter edits, and triggers confirmation callback with updated fields.
3. **Bulk CV Upload Modal Opening & File Selection**: Verify modal opens from `JobPostingDetailPage`, accepts multiple files in dropzone, and calls bulk upload endpoint.
4. **Bulk Progress Polling & Batch Complete**: Verify batch status polling updates progress bar and lists processed files with status badges (`Success`, `Failed`).
5. **Error & Edge Case Handling**: Verify file size limit rejection (>10MB) and non-supported file extension warning.

---

## 6. Implementation Action Plan & Component Architecture Strategy

```
frontend/internal/src/
├── features/
│   └── pipeline/
│       ├── CandidateSlideOver.tsx          # Updated with enhanced CV & Documents tab
│       ├── CvUploadPanel.tsx               # New component: single CV dropzone, progress, text viewer
│       ├── ParsedProfileReviewPanel.tsx    # New component: side-by-side human review & confirmation
│       ├── BulkCvUploadModal.tsx           # New component: multi-file dropzone & batch status tracker
│       └── pipeline.test.tsx               # Expanded with Flow 1 tests
├── pages/
│   └── JobPostingDetailPage.tsx            # Updated with Bulk Upload button & modal trigger
├── lib/
│   └── api.ts                              # Updated with apiUpload & resume endpoint methods
packages/
├── ui/
│   ├── src/
│   │   ├── Progress.tsx                    # New UI primitive: progress bar
│   │   ├── FileUploadZone.tsx              # New UI primitive: drag-and-drop zone
│   │   └── index.ts                        # Re-exports new primitives
└── types/
    └── src/
        └── index.ts                        # Updated with Resume & Bulk DTO interfaces
```
