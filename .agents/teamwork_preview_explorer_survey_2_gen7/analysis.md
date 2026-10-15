# Milestone 3 Survey & Architectural Analysis Report: Candidate 360 CV Viewer, Parsed Profile Review Panel, and Bulk CV Upload Modal

## 1. Executive Summary & Objective Overview

This analysis provides a comprehensive survey and detailed component architecture design for **Milestone 3 (Person A - Flow 1)** of the RecruitOps platform.
The target features comprise:
1. **CV & Documents Tab in `CandidateSlideOver.tsx`**: Replacing the static placeholder with a drag-and-drop file upload zone, upload progress bar, embedded extracted text viewer (with Zawgyi→Unicode normalization indicator), and direct CV download link.
2. **Parsed Profile Human Review Panel**: A side-by-side UI section allowing recruiters to compare raw extracted text against editable candidate profile fields (Name, Email, Phone, Experience, Skills) with an explicit confirmation button before updating candidate records.
3. **Bulk CV Upload Modal on `JobPostingDetailPage.tsx`**: A multi-file drag-and-drop modal accepting up to 50 CV files per batch, displaying real-time per-file processing progress (`Queued`, `Processing`, `Success`, `Skipped`, `Failed`).

---

## 2. Codebase Survey & Current State

### 2.1. Candidate 360 SlideOver (`frontend/internal/src/features/pipeline/CandidateSlideOver.tsx`)
- **Current State**: Rendered via `@recruitops/ui` `Sheet` primitive (`size="xl"`). Includes 5 top tab triggers: Overview, CV Viewer, Stage History, Scorecards, Notes & Debrief.
- **Tab 2 (`cv`) Placeholder**: Currently renders static text (`{candidate.candidateName}_Resume.pdf` and `"CV Document Preview"` placeholder).
- **Line Reference**: Lines 173–194 in `CandidateSlideOver.tsx`.
- **Integration Strategy**: Replace lines 173–194 with interactive `CvAndDocumentsTab` component containing drag-and-drop upload, upload progress, text viewer, download link, and human review panel.

### 2.2. Job Posting Detail Page (`frontend/internal/src/pages/JobPostingDetailPage.tsx`)
- **Current State**: Manages Job Posting Details, Public Job Link, and Candidate Pipeline list.
- **Line Reference**: Lines 308–393 render the Candidate Pipeline section.
- **Integration Strategy**: Add a **"Bulk Upload CVs"** button to the section header of Card line 309, next to `Pipeline · {pipeline.length} candidates`. Clicking the button opens `BulkCvUploadModal`.

### 2.3. Shared UI Primitives (`packages/ui/src` & `@recruitops/ui`)
- **Primitives Available**:
  - `Sheet`, `SheetHeader`, `SheetTitle`, `SheetBody`, `SheetFooter` (`packages/ui/src/Sheet.tsx`)
  - `Dialog`, `DialogHeader`, `DialogTitle`, `DialogDescription`, `DialogBody`, `DialogFooter` (`packages/ui/src/Dialog.tsx`)
  - `Tabs`, `TabsList`, `TabsTrigger`, `TabsContent` (`packages/ui/src/Tabs.tsx`)
  - `Button` (`packages/ui/src/Button.tsx`)
  - `Badge` (`packages/ui/src/Badge.tsx`)
  - `StatusPill` (`packages/ui/src/StatusPill.tsx`)
  - `Input` (`packages/ui/src/Input.tsx`)
  - `Select` (`packages/ui/src/Select.tsx`)
- **Utility Requirements**: Progress bars can be styled using Tailwind flex/bg-primary-600 bars (`h-2 rounded-full bg-primary-600 transition-all`).

### 2.4. Shared Types & API Layer (`packages/types` and `frontend/internal/src/lib/api.ts`)
- **`packages/types/src/index.ts`**: Currently contains types for Auth, Requisitions, Job Postings, Pipeline, Interviews, Scorecards, Notes, and AI endpoints.
- **Missing Types**: Requires additions for single CV upload extraction DTOs (`ResumeExtractionResult`, `ParsedContactInfo`, `ConfirmParsedProfileRequest`) and bulk CV processing DTOs (`BulkResumeUploadResponse`, `BulkResumeBatchStatus`, `BulkFileItemStatus`, `BulkFileStatus`).
- **`frontend/internal/src/lib/api.ts`**:
  - `apiFetch<T>` hardcodes `'Content-Type': 'application/json'`.
  - For `FormData` uploads, `Content-Type` header must be omitted so the browser automatically sets `multipart/form-data; boundary=...`.
  - Need dedicated helper functions or options in `apiFetch` for `FormData` file uploads.

### 2.5. Vitest Setup (`frontend/internal/vitest.config.ts`)
- Configured with `jsdom`, `@testing-library/react`, and `@testing-library/user-event`.
- Baseline suite: 233 passing tests in `frontend/internal`.

---

## 3. Detailed Component & UI Architecture Specifications

### 3.1. CV & Documents Tab (`CandidateSlideOver.tsx`)

#### Component Architecture: `CvAndDocumentsTab`
Located in `frontend/internal/src/features/pipeline/CvAndDocumentsTab.tsx` (or integrated into `CandidateSlideOver.tsx`).

#### State Management:
- `file`: `File | null` (selected file pending upload)
- `uploading`: `boolean`
- `uploadProgress`: `number` (0 to 100)
- `extractionResult`: `ResumeExtractionResult | null`
- `error`: `string | null`
- `dragOver`: `boolean`

#### Props Interface:
```tsx
export interface CvAndDocumentsTabProps {
  applicationId: string;
  candidateName: string;
  onProfileConfirmed?: () => void;
}
```

#### Detailed Layout & Behavior:
1. **Upload / Drop Zone**:
   - Accepts `.pdf`, `.docx`, `.png`, `.jpg`, `.jpeg` (max 10MB).
   - Drag events (`onDragOver`, `onDragLeave`, `onDrop`) toggle active visual ring state (`border-primary-500 bg-primary-50/50`).
   - Trigger file selector on click.
2. **Progress Bar & Action Buttons**:
   - Shows progress bar during upload (`<div className="w-full bg-line-200 h-2 rounded-full"><div className="bg-primary-600 h-2 rounded-full transition-all" style={{ width: `${progress}%` }} /></div>`).
3. **Extracted Text Viewer**:
   - Formatted scrollable box (`max-h-72 overflow-y-auto whitespace-pre-wrap font-mono text-xs bg-surface-50 p-4 border border-line-200 rounded-md`).
   - Displays **Zawgyi Normalization Badge** if `isZawgyiNormalized === true` (`<Badge variant="cyan">Zawgyi → Unicode Normalized</Badge>`).
   - Language badge (`Detected Language: EN / MY`).
4. **Download Button**:
   - Direct action button to fetch `/api/applications/${applicationId}/resume` as blob download.

---

### 3.2. Parsed Profile Human Review Panel

#### Component Architecture: `ParsedProfileReviewPanel`
Integrated side-by-side or stacked under the extracted text viewer in `CandidateSlideOver.tsx`.

#### State & Form Management:
- Local state initialized from `extractionResult.parsedContactInfo` or fallback candidate properties:
  - `name`: `string`
  - `email`: `string`
  - `phone`: `string`
  - `yearsOfExperience`: `number | string`
  - `skills`: `string[]` (with pill tag editor to add/remove skills)
- `isDirty`: `boolean`
- `confirming`: `boolean`
- `confirmed`: `boolean`

#### Proposed Layout:
```
+------------------------------------------+------------------------------------------+
| Extracted Raw CV Text (Read-Only)        | Editable Profile Fields (Human Review)   |
| ---------------------------------------- | ---------------------------------------- |
| [Zawgyi Normalized Badge]                | Full Name:   [ Jane Doe                ] |
|                                          | Email:       [ jane.doe@example.com    ] |
| John Doe                                 | Phone:       [ +95 912345678           ] |
| Senior Software Engineer                 | Experience:  [ 5 ] years                 |
| Email: john@example.com                  | Skills:      [ React x ] [ C# x ] [+Add] |
| Skills: React, C#, .NET, PostgreSQL      |                                          |
|                                          | [ Confirm & Update Profile Button ]      |
+------------------------------------------+------------------------------------------+
```

#### Review & Confirmation Workflow:
1. Recruiter edits fields if regex parsing missed details or had formatting errors.
2. Recruiter clicks **"Confirm & Update Candidate Profile"**.
3. Triggers API PUT request `apiFetch('/applications/${applicationId}/profile', { method: 'PUT', body: JSON.stringify(confirmedData) })`.
4. Shows success notification banner ("Profile verified and updated successfully").
5. Invokes parent `onProfileConfirmed` callback to refresh PipelineKanban board state.

---

### 3.3. Bulk CV Upload Modal (`JobPostingDetailPage.tsx`)

#### Component Architecture: `BulkCvUploadModal`
Located in `frontend/internal/src/features/pipeline/BulkCvUploadModal.tsx` or `frontend/internal/src/components/BulkCvUploadModal.tsx`.

#### Props Interface:
```tsx
export interface BulkCvUploadModalProps {
  jobPostingId: string;
  jobPostingTitle: string;
  isOpen: boolean;
  onClose: () => void;
  onUploadComplete?: () => void;
}
```

#### Multi-file Drag & Drop & Batch Management State:
- `files`: `File[]` (up to 50 files)
- `batchId`: `string | null`
- `uploading`: `boolean`
- `batchStatus`: `BulkResumeBatchStatus | null`
- `pollingInterval`: `NodeJS.Timeout | null`
- `error`: `string | null`

#### UI & Processing Flow:
1. **Modal Container**:
   - Uses `Dialog` from `@recruitops/ui` (`size="xl"`).
2. **Drag & Drop Zone**:
   - Supports selecting/dropping up to 50 CV files (`.pdf`, `.docx`, `.png`, `.jpg`).
   - Displays file count chip (`"Selected 24 / 50 files"`).
   - Validates file limit (errors if >50 files).
3. **Start Bulk Processing**:
   - User clicks **"Upload & Extract 24 CVs"**.
   - Constructs `FormData` with multiple `files` entries and posts to `/api/jobpostings/${jobPostingId}/resumes/bulk`.
   - Receives `batchId` response.
4. **Real-time Status Polling & Visual Progress**:
   - Polls `GET /api/jobpostings/${jobPostingId}/resumes/bulk/${batchId}` every 2 seconds.
   - Top overall progress bar: `processedCount / totalFiles` (e.g. "18/24 completed").
   - File status list with individual status badges:
     - `Queued`: Gray badge
     - `Processing`: Primary spinner badge
     - `Success`: Success green badge (shows extracted candidate name)
     - `Skipped`: Warning amber badge
     - `Failed`: Red danger badge (shows error message)
5. **Completion**:
   - Stops polling when `status === 'Completed'` or `status === 'PartialSuccess'` or `status === 'Failed'`.
   - Displays summary toast / banner ("Batch processing finished: 22 succeeded, 2 failed").
   - Triggers `onUploadComplete` to refresh the pipeline candidate list on `JobPostingDetailPage`.

---

## 4. Data Contracts & Type Extensions

### 4.1. Additions to `packages/types/src/index.ts`
```ts
// ── CV Upload & Text Extraction DTOs (Milestones 1-3) ─────────────────────
export interface ParsedContactInfo {
  candidateName?: string | null;
  email?: string | null;
  phone?: string | null;
  yearsOfExperience?: number | null;
  skills?: string[];
}

export interface ResumeExtractionResult {
  applicationId: string;
  fileKey: string;
  fileName: string;
  fileSizeBytes: number;
  extractedText: string;
  originalText?: string | null;
  detectedLanguage: string;
  isZawgyiNormalized: boolean;
  parsedContactInfo: ParsedContactInfo;
  processedAt: string;
}

export interface ConfirmParsedProfileRequest {
  candidateName: string;
  email?: string | null;
  phone?: string | null;
  yearsOfExperience?: number | null;
  skills?: string[];
}

// ── Bulk CV Upload & Batch Job DTOs (Milestones 2-3) ─────────────────────
export type BulkFileStatus = 'Queued' | 'Processing' | 'Success' | 'Skipped' | 'Failed';

export interface BulkFileItemStatus {
  fileId?: string;
  fileName: string;
  fileSizeBytes: number;
  status: BulkFileStatus;
  errorMessage?: string | null;
  applicationId?: string | null;
  candidateName?: string | null;
}

export interface BulkResumeUploadResponse {
  batchId: string;
  jobPostingId: string;
  totalFiles: number;
  status: 'Queued' | 'Processing' | 'Completed' | 'PartialSuccess' | 'Failed';
  createdAt: string;
}

export interface BulkResumeBatchStatus {
  batchId: string;
  jobPostingId: string;
  totalFiles: number;
  processedCount: number;
  successCount: number;
  failedCount: number;
  status: 'Queued' | 'Processing' | 'Completed' | 'PartialSuccess' | 'Failed';
  files: BulkFileItemStatus[];
  createdAt: string;
  completedAt?: string | null;
}
```

### 4.2. API Client Helpers in `frontend/internal/src/lib/api.ts`
```ts
/** Multipart FormData upload helper that omits Content-Type header so fetch sets boundary */
export async function apiUpload<T>(path: string, formData: FormData): Promise<T> {
  const session = auth.get();
  const BASE = import.meta.env.VITE_API_BASE_URL ?? '/api';
  const res = await fetch(`${BASE}${path}`, {
    method: 'POST',
    headers: {
      ...(session ? { Authorization: `Bearer ${session.accessToken}` } : {}),
      ...(session?.activeTenantId ? { 'X-Tenant-Id': session.activeTenantId } : {}),
    },
    body: formData,
  });
  if (!res.ok) {
    throw new ApiError(res.status, await res.text());
  }
  return (await res.json()) as T;
}

export const resumeApi = {
  uploadResume: (applicationId: string, file: File): Promise<ResumeExtractionResult> => {
    const formData = new FormData();
    formData.append('file', file);
    return apiUpload<ResumeExtractionResult>(`/applications/${applicationId}/resume`, formData);
  },

  getResume: async (applicationId: string): Promise<Blob> => {
    const BASE = import.meta.env.VITE_API_BASE_URL ?? '/api';
    const session = auth.get();
    const res = await fetch(`${BASE}/applications/${applicationId}/resume`, {
      headers: session ? { Authorization: `Bearer ${session.accessToken}` } : {},
    });
    if (!res.ok) throw new ApiError(res.status, 'Failed to download resume');
    return await res.blob();
  },

  confirmProfile: (applicationId: string, req: ConfirmParsedProfileRequest): Promise<void> =>
    apiFetch<void>(`/applications/${applicationId}/profile`, {
      method: 'PUT',
      body: JSON.stringify(req),
    }),

  bulkUpload: (jobPostingId: string, files: File[]): Promise<BulkResumeUploadResponse> => {
    const formData = new FormData();
    files.forEach((f) => formData.append('files', f));
    return apiUpload<BulkResumeUploadResponse>(`/jobpostings/${jobPostingId}/resumes/bulk`, formData);
  },

  getBulkStatus: (jobPostingId: string, batchId: string): Promise<BulkResumeBatchStatus> =>
    apiFetch<BulkResumeBatchStatus>(`/jobpostings/${jobPostingId}/resumes/bulk/${batchId}`),
};
```

---

## 5. Vitest Verification Strategy

To guarantee zero regression and verify new UI interactions, 5+ comprehensive test cases should be added to `frontend/internal/src/features/pipeline/cvUpload.test.tsx` (or `pipeline.test.tsx`):

1. **Single CV Upload & Text Extraction**:
   - Test drag-and-drop or file input selection in `CandidateSlideOver`.
   - Verify upload progress bar appears during POST request.
   - Verify extracted text viewer displays extracted content and Zawgyi normalization badge when `isZawgyiNormalized: true`.
2. **Parsed Profile Human Review Panel**:
   - Verify extracted contact info prepopulates form fields.
   - Edit Name, Email, Experience, and Skills.
   - Click "Confirm & Update Profile" and verify PUT API payload matches updated values.
3. **Bulk CV Upload Drag-and-Drop Limit**:
   - Open `BulkCvUploadModal` on `JobPostingDetailPage`.
   - Attempt dropping 55 files -> expect validation error message ("Maximum 50 files allowed per batch").
   - Drop 10 files -> verify file count badge updates to "10 files selected".
4. **Bulk CV Real-Time Batch Progress & Status Polling**:
   - Mock POST `/jobpostings/job-1/resumes/bulk` returning `batchId: 'batch-100'`.
   - Mock GET `/jobpostings/job-1/resumes/bulk/batch-100` returning queued, processing, and completed statuses.
   - Verify per-file list updates from `Queued` -> `Processing` -> `Success`/`Failed`.
5. **Download Resume Action**:
   - Click "Download Original CV" button and verify download URL / blob trigger is invoked cleanly.

---

## 6. Summary of Architectural Compliance
- **Read-Only Investigation**: All proposals strictly respect existing design system tokens, Tailwind conventions, and Clean Architecture layer boundaries.
- **Type Safety**: Full TypeScript alignment across `@recruitops/types`, `@recruitops/ui`, and `@recruitops/internal`.
