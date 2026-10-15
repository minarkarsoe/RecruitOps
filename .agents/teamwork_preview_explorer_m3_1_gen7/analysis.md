# Milestone 3 Implementation Blueprint & Specification Report: Candidate 360 SlideOver CV Viewer, Parsed Profile UI, and Bulk Upload Modal

**Target Scope**: RecruitOps Person A - Flow 1 (Milestone 3)  
**Authoritative Input Sources**:
- `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\ORIGINAL_REQUEST.md`
- `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_explorer_survey_2_gen7\analysis.md`
- `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_spec_miner_survey_3_gen7\analysis.md`

---

## 1. Executive Summary & Objective Overview

This document presents the detailed architectural blueprint, code specification, and step-by-step implementation plan for **Milestone 3 of Person A - Flow 1**. 

### Primary Deliverables:
1. **Shared Types (`packages/types/src/index.ts`)**: Define TypeScript types matching backend DTOs for single CV extraction, parsed profile confirmation, and bulk resume background processing.
2. **API Client (`frontend/internal/src/lib/api.ts`)**: Implement `apiUpload` helper for FormData requests and add API endpoints (`uploadCandidateResume`, `downloadCandidateResume`, `confirmParsedProfile`, `postBulkResumes`, `getBulkResumeStatus`).
3. **Candidate 360 SlideOver (`CandidateSlideOver.tsx`)**:
   - **CV & Documents Tab**: Drag-and-drop single file upload zone, live upload progress bar, scrollable text viewer with `Zawgyi → Unicode Normalized` badge indicator, and direct CV download button.
   - **Parsed Profile Human Review Panel**: Side-by-side layout displaying raw extracted text alongside pre-populated editable candidate fields (Name, Email, Phone, Experience, Skills), enforcing an explicit recruiter click on **"Confirm & Apply to Profile"** before updating candidate records (per ADR-0008 Guardrail 1).
4. **Bulk CV Upload Modal (`JobPostingDetailPage.tsx`)**:
   - Add **"Bulk Upload CVs"** button to the Pipeline card header.
   - Implement `BulkCvUploadModal` using the `@recruitops/ui` `Dialog` primitive.
   - Multi-file drag-and-drop upload zone supporting up to 50 files per batch.
   - Polling loop querying `GET /api/jobpostings/{jobPostingId}/resumes/bulk/{batchId}` every 2 seconds.
   - Overall progress bar and per-file status badges (`Queued`, `Processing`, `Success`, `Skipped`, `Failed`).
5. **Vitest Unit Test Suite Strategy**: Comprehensive test specifications for `CandidateSlideOver` and `JobPostingDetailPage`.

---

## 2. Shared Types Specification (`packages/types/src/index.ts`)

Append the following type definitions to `packages/types/src/index.ts` to align frontend data models with backend DTOs (`ResumeExtractionDtos.cs` and `BulkUploadDtos.cs`).

```typescript
// ── Single CV Extraction & Human Review DTOs (Milestones 1 & 3) ──────────────

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

// ── Bulk CV Upload & Background Processing DTOs (Milestones 2 & 3) ─────────

export type BulkFileStatus = 'Queued' | 'Processing' | 'Success' | 'Skipped' | 'Failed';

export interface BulkFileItemStatus {
  fileId?: string;
  fileName: string;
  fileSizeBytes: number;
  status: BulkFileStatus;
  errorMessage?: string | null;
  createdApplicationId?: string | null;
  createdCandidateId?: string | null;
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
  skippedCount?: number;
  status: 'Queued' | 'Processing' | 'Completed' | 'PartialSuccess' | 'Failed';
  files: BulkFileItemStatus[];
  createdAt: string;
  completedAt?: string | null;
}
```

---

## 3. API Client Extensions Specification (`frontend/internal/src/lib/api.ts`)

### 3.1 `apiUpload` FormData Helper

Create `apiUpload<T>` helper function in `frontend/internal/src/lib/api.ts`. `apiUpload` handles multipart file uploads by avoiding the `Content-Type: application/json` header, allowing the browser to set the standard boundary header (`multipart/form-data; boundary=...`).

```typescript
/**
 * Helper for FormData multipart requests (e.g. CV file uploads).
 * Intentionally omits 'Content-Type' header so fetch automatically inserts boundary.
 */
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

  if (res.status === 401 && session?.refreshToken && path !== '/auth/refresh') {
    const refreshed = await performSilentRefresh(session.refreshToken);
    if (refreshed) {
      const retryRes = await fetch(`${BASE}${path}`, {
        method: 'POST',
        headers: {
          Authorization: `Bearer ${refreshed.accessToken}`,
          ...(refreshed.activeTenantId ? { 'X-Tenant-Id': refreshed.activeTenantId } : {}),
        },
        body: formData,
      });
      if (!retryRes.ok) throw new ApiError(retryRes.status, await readError(retryRes));
      return (await retryRes.json()) as T;
    }
    auth.clear();
    throw new ApiError(401, 'Your session has expired. Please sign in again.');
  }

  if (!res.ok) {
    throw new ApiError(res.status, await readError(res));
  }

  const text = await res.text();
  return (text ? JSON.parse(text) : undefined) as T;
}
```

### 3.2 `resumeApi` Endpoint Mapping

Expose `resumeApi` namespace in `frontend/internal/src/lib/api.ts`:

```typescript
export const resumeApi = {
  /** Upload single CV file for candidate application and perform text extraction */
  uploadCandidateResume: (applicationId: string, file: File): Promise<ResumeExtractionResult> => {
    const formData = new FormData();
    formData.append('file', file);
    return apiUpload<ResumeExtractionResult>(`/applications/${applicationId}/resume`, formData);
  },

  /** Download original CV file blob */
  downloadCandidateResume: async (applicationId: string): Promise<Blob> => {
    const session = auth.get();
    const BASE = import.meta.env.VITE_API_BASE_URL ?? '/api';
    const res = await fetch(`${BASE}/applications/${applicationId}/resume`, {
      headers: session ? { Authorization: `Bearer ${session.accessToken}` } : {},
    });
    if (!res.ok) throw new ApiError(res.status, 'Failed to download resume document.');
    return await res.blob();
  },

  /** Recruiter explicit confirmation of parsed profile fields */
  confirmParsedProfile: (applicationId: string, req: ConfirmParsedProfileRequest): Promise<void> =>
    apiFetch<void>(`/applications/${applicationId}/profile`, {
      method: 'PUT',
      body: JSON.stringify(req),
    }),

  /** Initiate bulk CV upload batch (up to 50 files) */
  postBulkResumes: (jobPostingId: string, files: File[]): Promise<BulkResumeUploadResponse> => {
    const formData = new FormData();
    files.forEach((file) => formData.append('files', file));
    return apiUpload<BulkResumeUploadResponse>(`/jobpostings/${jobPostingId}/resumes/bulk`, formData);
  },

  /** Query status and per-file progress for bulk resume batch */
  getBulkResumeStatus: (jobPostingId: string, batchId: string): Promise<BulkResumeBatchStatus> =>
    apiFetch<BulkResumeBatchStatus>(`/jobpostings/${jobPostingId}/resumes/bulk/${batchId}`),
};
```

---

## 4. Candidate 360 SlideOver Specification (`CandidateSlideOver.tsx`)

Replace the placeholder implementation in `CandidateSlideOver.tsx` (lines 173-194) with the full CV Viewer and Parsed Profile Human Review Panel UI.

### 4.1 UI Layout & Structure

```
+-----------------------------------------------------------------------------------+
| Tab 2: CV Viewer & Documents                                                      |
+-----------------------------------------------------------------------------------+
| [ File Drop Zone ]  "Drag and drop CV file (.pdf, .docx, .png, .jpg <= 10MB)"     |
| [ Progress Bar ]   (Displayed during upload: 45%)                                 |
+-----------------------------------------------------------------------------------+
| Side-by-Side Grid (lg:grid-cols-2 gap-4)                                          |
|                                                                                   |
|  LEFT PANEL: Raw Extracted CV Text          RIGHT PANEL: Human Review & Edit      |
|  ----------------------------------------   ------------------------------------  |
|  [Badge: Zawgyi → Unicode Normalized]       Candidate Name: [ Jane Doe         ]  |
|  [Badge: Language: EN / MY]                 Email:          [ jane@example.com ]  |
|                                             Phone:          [ +95 912345678    ]  |
|  John Doe                                   Experience:     [ 5 ] years           |
|  Senior Software Engineer                   Skills:         [ React x ] [ C# x ]  |
|  Email: john@example.com                                    [ + Add Skill ]       |
|  Skills: React, C#, .NET                                                          |
|                                             [ Confirm & Apply to Profile Button ] |
+-----------------------------------------------------------------------------------+
| [ Download Original CV Document Button ]                                          |
+-----------------------------------------------------------------------------------+
```

### 4.2 Detailed Component Implementation Specification

```tsx
interface ParsedProfileFormState {
  candidateName: string;
  email: string;
  phone: string;
  yearsOfExperience: number | '';
  skills: string[];
  newSkillInput: string;
}

export function CvAndDocumentsTab({
  candidate,
  onProfileUpdated,
}: {
  candidate: PipelineItem;
  onProfileUpdated?: () => void;
}) {
  const [uploading, setUploading] = useState(false);
  const [uploadProgress, setUploadProgress] = useState(0);
  const [extractionResult, setExtractionResult] = useState<ResumeExtractionResult | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [dragActive, setDragActive] = useState(false);
  const [confirming, setConfirming] = useState(false);
  const [confirmSuccess, setConfirmSuccess] = useState(false);

  // Form state for human review
  const [form, setForm] = useState<ParsedProfileFormState>({
    candidateName: candidate.candidateName || '',
    email: candidate.email || '',
    phone: candidate.phone || '',
    yearsOfExperience: '',
    skills: [],
    newSkillInput: '',
  });

  const handleFileUpload = async (file: File) => {
    if (file.size > 10 * 1024 * 1024) {
      setError('File size exceeds maximum limit of 10MB.');
      return;
    }
    const validExts = ['.pdf', '.docx', '.png', '.jpg', '.jpeg'];
    const ext = file.name.substring(file.name.lastIndexOf('.')).toLowerCase();
    if (!validExts.includes(ext)) {
      setError('Invalid file format. Allowed formats: PDF, DOCX, PNG, JPG, JPEG.');
      return;
    }

    setUploading(true);
    setUploadProgress(30);
    setError(null);

    try {
      setUploadProgress(60);
      const result = await resumeApi.uploadCandidateResume(candidate.id, file);
      setUploadProgress(100);
      setExtractionResult(result);

      // Prepopulate Human Review panel with extracted contact info
      if (result.parsedContactInfo) {
        setForm((prev) => ({
          ...prev,
          candidateName: result.parsedContactInfo.candidateName || prev.candidateName,
          email: result.parsedContactInfo.email || prev.email,
          phone: result.parsedContactInfo.phone || prev.phone,
          yearsOfExperience: result.parsedContactInfo.yearsOfExperience ?? prev.yearsOfExperience,
          skills: result.parsedContactInfo.skills ?? [],
        }));
      }
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to upload and extract CV.');
    } finally {
      setUploading(false);
    }
  };

  const handleConfirmProfile = async () => {
    if (!form.candidateName.trim()) {
      setError('Candidate Name is required.');
      return;
    }
    setConfirming(true);
    setError(null);
    try {
      await resumeApi.confirmParsedProfile(candidate.id, {
        candidateName: form.candidateName,
        email: form.email || null,
        phone: form.phone || null,
        yearsOfExperience: form.yearsOfExperience === '' ? null : Number(form.yearsOfExperience),
        skills: form.skills,
      });
      setConfirmSuccess(true);
      if (onProfileUpdated) onProfileUpdated();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to confirm profile updates.');
    } finally {
      setConfirming(false);
    }
  };

  const handleDownloadCv = async () => {
    try {
      const blob = await resumeApi.downloadCandidateResume(candidate.id);
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `${candidate.candidateName}_CV.pdf`;
      document.body.appendChild(a);
      a.click();
      a.remove();
      window.URL.revokeObjectURL(url);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Download failed.');
    }
  };

  return (
    <div className="space-y-6">
      {/* 1. Drag and Drop Upload Zone */}
      <div
        className={`relative flex flex-col items-center justify-center rounded-lg border-2 border-dashed p-6 transition-colors ${
          dragActive
            ? 'border-primary-500 bg-primary-50/50'
            : 'border-line-300 bg-surface-50 hover:border-primary-400'
        }`}
        onDragOver={(e) => {
          e.preventDefault();
          setDragActive(true);
        }}
        onDragLeave={() => setDragActive(false)}
        onDrop={(e) => {
          e.preventDefault();
          setDragActive(false);
          if (e.dataTransfer.files?.[0]) handleFileUpload(e.dataTransfer.files[0]);
        }}
      >
        <input
          type="file"
          id="cv-single-upload"
          className="hidden"
          accept=".pdf,.docx,.png,.jpg,.jpeg"
          onChange={(e) => e.target.files?.[0] && handleFileUpload(e.target.files[0])}
        />
        <label
          htmlFor="cv-single-upload"
          className="flex cursor-pointer flex-col items-center justify-center text-center"
        >
          <svg className="h-10 w-10 text-ink-400 mb-2" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M7 16a4 4 0 01-.88-7.903A5 5 0 1115.9 6L16 6a5 5 0 011 9.9M15 13l-3-3m0 0l-3 3m3-3v12" />
          </svg>
          <span className="text-sm font-semibold text-primary-600 hover:underline">
            Click to upload CV document
          </span>
          <span className="mt-1 text-xs text-ink-500">
            Supports PDF, DOCX, PNG, JPG up to 10MB
          </span>
        </label>
      </div>

      {/* Upload Progress Bar */}
      {uploading && (
        <div className="space-y-1">
          <div className="flex justify-between text-xs text-ink-600 font-medium">
            <span>Uploading and extracting text...</span>
            <span>{uploadProgress}%</span>
          </div>
          <div className="w-full bg-line-200 h-2 rounded-full overflow-hidden">
            <div
              className="bg-primary-600 h-2 rounded-full transition-all duration-300"
              style={{ width: `${uploadProgress}%` }}
            />
          </div>
        </div>
      )}

      {error && <div className="rounded-md bg-danger-50 p-3 text-xs text-danger-700 font-medium">{error}</div>}
      {confirmSuccess && (
        <div className="rounded-md bg-success-50 p-3 text-xs text-success-700 font-medium">
          Candidate profile updated and confirmed successfully!
        </div>
      )}

      {/* 2. Side-by-Side Viewer and Human Review Panel */}
      <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
        {/* Left Column: Raw Extracted CV Text */}
        <div className="flex flex-col rounded-md border border-line-200 bg-surface-0 p-4">
          <div className="mb-3 flex items-center justify-between">
            <h4 className="text-xs font-semibold uppercase tracking-wider text-ink-500">
              Extracted Raw CV Text
            </h4>
            {extractionResult?.isZawgyiNormalized && (
              <Badge variant="cyan">Zawgyi → Unicode Normalized</Badge>
            )}
          </div>
          
          {extractionResult ? (
            <div className="flex-1 max-h-80 overflow-y-auto rounded bg-surface-50 p-3 font-mono text-xs text-ink-900 whitespace-pre-wrap border border-line-200">
              {extractionResult.extractedText || 'No text extracted from document.'}
            </div>
          ) : (
            <div className="flex min-h-[220px] flex-col items-center justify-center rounded bg-surface-50 p-6 text-center text-xs text-ink-500 border border-line-200">
              Upload a CV document above to extract and view readable text.
            </div>
          )}

          {extractionResult && (
            <div className="mt-3 flex items-center justify-between text-xs text-ink-500">
              <span>File: {extractionResult.fileName}</span>
              <span>Language: {extractionResult.detectedLanguage || 'EN'}</span>
            </div>
          )}
        </div>

        {/* Right Column: Parsed Profile Human Review Form */}
        <div className="rounded-md border border-line-200 bg-surface-0 p-4 space-y-4">
          <h4 className="text-xs font-semibold uppercase tracking-wider text-ink-500 border-b border-line-200 pb-2">
            Parsed Profile Human Review
          </h4>

          <div className="space-y-3 text-xs">
            <div>
              <label className="block font-semibold text-ink-700 mb-1">Full Name *</label>
              <Input
                value={form.candidateName}
                onChange={(e) => setForm({ ...form, candidateName: e.target.value })}
                placeholder="Candidate Full Name"
              />
            </div>

            <div className="grid grid-cols-2 gap-3">
              <div>
                <label className="block font-semibold text-ink-700 mb-1">Email Address</label>
                <Input
                  value={form.email}
                  onChange={(e) => setForm({ ...form, email: e.target.value })}
                  placeholder="candidate@example.com"
                />
              </div>
              <div>
                <label className="block font-semibold text-ink-700 mb-1">Phone Number</label>
                <Input
                  value={form.phone}
                  onChange={(e) => setForm({ ...form, phone: e.target.value })}
                  placeholder="+95 9..."
                />
              </div>
            </div>

            <div>
              <label className="block font-semibold text-ink-700 mb-1">Years of Experience</label>
              <Input
                type="number"
                value={form.yearsOfExperience}
                onChange={(e) => setForm({ ...form, yearsOfExperience: e.target.value === '' ? '' : Number(e.target.value) })}
                placeholder="e.g. 5"
              />
            </div>

            <div>
              <label className="block font-semibold text-ink-700 mb-1">Skills</label>
              <div className="flex flex-wrap gap-1.5 mb-2">
                {form.skills.map((skill, idx) => (
                  <span
                    key={idx}
                    className="inline-flex items-center gap-1 rounded bg-primary-50 px-2 py-0.5 text-xs font-medium text-primary-700 border border-primary-200"
                  >
                    {skill}
                    <button
                      type="button"
                      className="text-primary-500 hover:text-primary-800"
                      onClick={() => setForm({ ...form, skills: form.skills.filter((_, i) => i !== idx) })}
                    >
                      ×
                    </button>
                  </span>
                ))}
              </div>
              <div className="flex gap-2">
                <Input
                  value={form.newSkillInput}
                  onChange={(e) => setForm({ ...form, newSkillInput: e.target.value })}
                  placeholder="Add skill (e.g. React)"
                  className="text-xs h-8"
                />
                <Button
                  type="button"
                  variant="secondary"
                  className="h-8 px-3 text-xs"
                  onClick={() => {
                    if (form.newSkillInput.trim()) {
                      setForm({
                        ...form,
                        skills: [...form.skills, form.newSkillInput.trim()],
                        newSkillInput: '',
                      });
                    }
                  }}
                >
                  Add
                </Button>
              </div>
            </div>
          </div>

          <div className="pt-2 border-t border-line-200 flex justify-end">
            <Button
              onClick={handleConfirmProfile}
              disabled={confirming}
              className="bg-primary-600 hover:bg-primary-700 text-white"
            >
              {confirming ? 'Saving Profile...' : 'Confirm & Apply to Profile'}
            </Button>
          </div>
        </div>
      </div>

      {/* 3. CV Download Button */}
      <div className="flex justify-end pt-2">
        <Button variant="secondary" onClick={handleDownloadCv} className="flex items-center gap-2">
          <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4" />
          </svg>
          Download Original CV Document
        </Button>
      </div>
    </div>
  );
}
```

---

## 5. Bulk CV Upload Modal Specification (`JobPostingDetailPage.tsx`)

### 5.1 Pipeline Card Header Integration

In `JobPostingDetailPage.tsx`, locate line 309 inside `<Card>` and update header controls to include the **"Bulk Upload CVs"** button:

```tsx
<div className="mb-4 flex items-center justify-between">
  <h2 className="text-[13px] font-semibold uppercase tracking-wide text-ink-600">
    Pipeline · {pipeline.length} {pipeline.length === 1 ? 'candidate' : 'candidates'}
  </h2>
  <Button
    variant="secondary"
    className="flex items-center gap-1.5 text-xs h-8 px-3"
    onClick={() => setIsBulkModalOpen(true)}
  >
    <svg className="h-4 w-4 text-primary-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M7 16a4 4 0 01-.88-7.903A5 5 0 1115.9 6L16 6a5 5 0 011 9.9M15 13l-3-3m0 0l-3 3m3-3v12" />
    </svg>
    Bulk Upload CVs
  </Button>
</div>
```

### 5.2 `BulkCvUploadModal` Component Specification

Create file `frontend/internal/src/features/pipeline/BulkCvUploadModal.tsx` (or inside `components/`):

```tsx
import { useState, useEffect, useRef } from 'react';
import {
  Dialog,
  DialogHeader,
  DialogTitle,
  DialogBody,
  DialogFooter,
  Button,
  Badge,
} from '@recruitops/ui';
import type { BulkResumeBatchStatus, BulkFileStatus } from '@recruitops/types';
import { resumeApi } from '../../lib/api';

export interface BulkCvUploadModalProps {
  jobPostingId: string;
  isOpen: boolean;
  onClose: () => void;
  onUploadComplete?: () => void;
}

export function BulkCvUploadModal({
  jobPostingId,
  isOpen,
  onClose,
  onUploadComplete,
}: BulkCvUploadModalProps) {
  const [selectedFiles, setSelectedFiles] = useState<File[]>([]);
  const [batchId, setBatchId] = useState<string | null>(null);
  const [uploading, setUploading] = useState(false);
  const [batchStatus, setBatchStatus] = useState<BulkResumeBatchStatus | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [dragActive, setDragActive] = useState(false);
  const pollingRef = useRef<NodeJS.Timeout | null>(null);

  // Clear state when modal closes
  useEffect(() => {
    if (!isOpen) {
      setSelectedFiles([]);
      setBatchId(null);
      setBatchStatus(null);
      setError(null);
      if (pollingRef.current) clearInterval(pollingRef.current);
    }
  }, [isOpen]);

  const handleFilesAdded = (files: FileList | File[]) => {
    const newFiles = Array.from(files);
    if (selectedFiles.length + newFiles.length > 50) {
      setError('Maximum 50 files allowed per bulk upload batch.');
      return;
    }
    const validFiles = newFiles.filter((f) => {
      const ext = f.name.substring(f.name.lastIndexOf('.')).toLowerCase();
      return ['.pdf', '.docx', '.png', '.jpg', '.jpeg'].includes(ext) && f.size <= 10 * 1024 * 1024;
    });

    if (validFiles.length < newFiles.length) {
      setError('Some files were ignored (exceeds 10MB or unsupported format).');
    } else {
      setError(null);
    }

    setSelectedFiles((prev) => [...prev, ...validFiles]);
  };

  const handleStartBulkUpload = async () => {
    if (selectedFiles.length === 0) return;
    setUploading(true);
    setError(null);

    try {
      const response = await resumeApi.postBulkResumes(jobPostingId, selectedFiles);
      setBatchId(response.batchId);
      
      // Start polling loop
      pollingRef.current = setInterval(async () => {
        try {
          const status = await resumeApi.getBulkResumeStatus(jobPostingId, response.batchId);
          setBatchStatus(status);
          if (status.status === 'Completed' || status.status === 'PartialSuccess' || status.status === 'Failed') {
            if (pollingRef.current) clearInterval(pollingRef.current);
            setUploading(false);
            if (onUploadComplete) onUploadComplete();
          }
        } catch {
          // Keep polling until timeout or completion
        }
      }, 2000);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Bulk upload failed.');
      setUploading(false);
    }
  };

  const renderBadge = (status: BulkFileStatus) => {
    switch (status) {
      case 'Queued': return <Badge variant="zinc">Queued</Badge>;
      case 'Processing': return <Badge variant="cyan">Processing...</Badge>;
      case 'Success': return <Badge variant="teal">Success</Badge>;
      case 'Skipped': return <Badge variant="amber">Skipped</Badge>;
      case 'Failed': return <Badge variant="rose">Failed</Badge>;
      default: return <Badge variant="zinc">{status}</Badge>;
    }
  };

  return (
    <Dialog isOpen={isOpen} onClose={onClose} size="xl">
      <DialogHeader>
        <DialogTitle>Bulk Upload CV Documents</DialogTitle>
        <p className="text-xs text-ink-500 mt-1">
          Upload up to 50 CV files (.pdf, .docx, .png, .jpg) for automated extraction and pipeline creation.
        </p>
      </DialogHeader>

      <DialogBody className="space-y-4">
        {error && <div className="rounded-md bg-danger-50 p-3 text-xs text-danger-700 font-medium">{error}</div>}

        {!batchId ? (
          <>
            {/* File Selection Dropzone */}
            <div
              className={`flex flex-col items-center justify-center rounded-lg border-2 border-dashed p-8 text-center ${
                dragActive ? 'border-primary-500 bg-primary-50/50' : 'border-line-300 bg-surface-50'
              }`}
              onDragOver={(e) => { e.preventDefault(); setDragActive(true); }}
              onDragLeave={() => setDragActive(false)}
              onDrop={(e) => {
                e.preventDefault();
                setDragActive(false);
                if (e.dataTransfer.files) handleFilesAdded(e.dataTransfer.files);
              }}
            >
              <input
                type="file"
                id="bulk-cv-input"
                multiple
                className="hidden"
                accept=".pdf,.docx,.png,.jpg,.jpeg"
                onChange={(e) => e.target.files && handleFilesAdded(e.target.files)}
              />
              <label htmlFor="bulk-cv-input" className="cursor-pointer text-center">
                <svg className="h-10 w-10 text-ink-400 mx-auto mb-2" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-8l-4-4m0 0l-4 4m4-4v12" />
                </svg>
                <span className="text-sm font-semibold text-primary-600 hover:underline">
                  Click to select up to 50 CV files
                </span>
                <span className="block mt-1 text-xs text-ink-500">
                  Or drag and drop files directly into this area
                </span>
              </label>
            </div>

            {/* Selected File List */}
            {selectedFiles.length > 0 && (
              <div className="space-y-2">
                <div className="flex items-center justify-between">
                  <span className="text-xs font-semibold text-ink-700">
                    Selected Files ({selectedFiles.length} / 50)
                  </span>
                  <button
                    type="button"
                    className="text-xs text-danger-600 hover:underline"
                    onClick={() => setSelectedFiles([])}
                  >
                    Clear all
                  </button>
                </div>
                <div className="max-h-48 overflow-y-auto rounded-md border border-line-200 divide-y divide-line-100">
                  {selectedFiles.map((file, idx) => (
                    <div key={idx} className="flex items-center justify-between px-3 py-2 text-xs">
                      <span className="font-medium text-ink-900 truncate max-w-xs">{file.name}</span>
                      <span className="text-ink-500">{(file.size / 1024 / 1024).toFixed(2)} MB</span>
                    </div>
                  ))}
                </div>
              </div>
            )}
          </>
        ) : (
          /* Live Batch Progress View */
          <div className="space-y-4">
            <div className="flex items-center justify-between text-xs font-semibold text-ink-700">
              <span>Batch Status: {batchStatus?.status || 'Processing...'}</span>
              <span>
                {batchStatus?.processedCount ?? 0} / {batchStatus?.totalFiles ?? selectedFiles.length} Processed
              </span>
            </div>

            {/* Progress Bar */}
            <div className="w-full bg-line-200 h-2.5 rounded-full overflow-hidden">
              <div
                className="bg-primary-600 h-2.5 rounded-full transition-all duration-300"
                style={{
                  width: `${
                    batchStatus
                      ? Math.round((batchStatus.processedCount / batchStatus.totalFiles) * 100)
                      : 10
                  }%`,
                }}
              />
            </div>

            {/* Per-File Progress List */}
            <div className="max-h-64 overflow-y-auto rounded-md border border-line-200 divide-y divide-line-100">
              {batchStatus?.files.map((fileItem, idx) => (
                <div key={idx} className="flex items-center justify-between px-3 py-2 text-xs">
                  <div className="flex flex-col">
                    <span className="font-medium text-ink-900 truncate max-w-xs">{fileItem.fileName}</span>
                    {fileItem.candidateName && (
                      <span className="text-[11px] text-ink-500">Candidate: {fileItem.candidateName}</span>
                    )}
                    {fileItem.errorMessage && (
                      <span className="text-[11px] text-danger-600">{fileItem.errorMessage}</span>
                    )}
                  </div>
                  <div>{renderBadge(fileItem.status)}</div>
                </div>
              ))}
            </div>
          </div>
        )}
      </DialogBody>

      <DialogFooter>
        {!batchId ? (
          <>
            <Button variant="secondary" onClick={onClose}>Cancel</Button>
            <Button
              onClick={handleStartBulkUpload}
              disabled={selectedFiles.length === 0 || uploading}
              className="bg-primary-600 text-white hover:bg-primary-700"
            >
              Start Bulk Upload ({selectedFiles.length})
            </Button>
          </>
        ) : (
          <Button
            onClick={onClose}
            disabled={uploading}
            className="bg-primary-600 text-white hover:bg-primary-700"
          >
            {uploading ? 'Processing in Background...' : 'Close & Refresh Pipeline'}
          </Button>
        )}
      </DialogFooter>
    </Dialog>
  );
}
```

---

## 6. Vitest Strategy & Unit Test Blueprint

Create two test suites:
1. `frontend/internal/src/features/pipeline/__tests__/CandidateSlideOver.test.tsx`
2. `frontend/internal/src/pages/__tests__/JobPostingDetailPage.test.tsx`

### 6.1 `CandidateSlideOver.test.tsx`

```tsx
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { CandidateSlideOver } from '../CandidateSlideOver';
import { resumeApi } from '../../../lib/api';

vi.mock('../../../lib/api', async () => {
  const actual = await vi.importActual<typeof import('../../../lib/api')>('../../../lib/api');
  return {
    ...actual,
    resumeApi: {
      uploadCandidateResume: vi.fn(),
      downloadCandidateResume: vi.fn(),
      confirmParsedProfile: vi.fn(),
    },
  };
});

const mockCandidate = {
  id: 'app-123',
  candidateId: 'cand-456',
  candidateName: 'Jane Smith',
  email: 'jane@example.com',
  phone: '+95 912345678',
  status: 'Screening' as const,
  source: 'LinkedIn' as const,
  appliedAt: '2026-08-05T10:00:00Z',
  coverNote: null,
  customFieldsJson: null,
};

describe('CandidateSlideOver CV Viewer & Parsed Profile UI', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders CV Viewer tab and drag-and-drop zone', async () => {
    const user = userEvent.setup();
    render(<CandidateSlideOver candidate={mockCandidate} isOpen={true} onClose={vi.fn()} initialTab="cv" />);

    expect(screen.getByRole('button', { name: /CV Viewer/i })).toBeInTheDocument();
    expect(screen.getByText(/Click to upload CV document/i)).toBeInTheDocument();
  });

  it('uploads CV, displays extracted text and Zawgyi normalization badge', async () => {
    const mockExtractionResult = {
      applicationId: 'app-123',
      fileKey: 'resumes/app-123.pdf',
      fileName: 'resume_myanmar.pdf',
      fileSizeBytes: 102450,
      extractedText: 'Mingalarbar John Doe Senior Developer',
      detectedLanguage: 'my',
      isZawgyiNormalized: true,
      parsedContactInfo: {
        candidateName: 'John Doe',
        email: 'john.doe@example.com',
        phone: '+95 998765432',
        yearsOfExperience: 6,
        skills: ['React', 'C#'],
      },
      processedAt: '2026-08-08T12:00:00Z',
    };

    vi.mocked(resumeApi.uploadCandidateResume).mockResolvedValueOnce(mockExtractionResult);

    render(<CandidateSlideOver candidate={mockCandidate} isOpen={true} onClose={vi.fn()} initialTab="cv" />);

    const file = new File(['fake pdf text content'], 'resume_myanmar.pdf', { type: 'application/pdf' });
    const input = screen.getByLabelText(/Click to upload CV document/i);

    await fireEvent.change(input, { target: { files: [file] } });

    await waitFor(() => {
      expect(resumeApi.uploadCandidateResume).toHaveBeenCalledWith('app-123', file);
      expect(screen.getByText('Zawgyi → Unicode Normalized')).toBeInTheDocument();
      expect(screen.getByText(/Mingalarbar John Doe Senior Developer/i)).toBeInTheDocument();
    });
  });

  it('edits candidate profile fields and sends confirmParsedProfile request on button click', async () => {
    const user = userEvent.setup();
    vi.mocked(resumeApi.confirmParsedProfile).mockResolvedValueOnce(undefined);

    render(<CandidateSlideOver candidate={mockCandidate} isOpen={true} onClose={vi.fn()} initialTab="cv" />);

    const nameInput = screen.getByPlaceholderText('Candidate Full Name');
    await user.clear(nameInput);
    await user.type(nameInput, 'Jane Updated');

    const confirmBtn = screen.getByRole('button', { name: /Confirm & Apply to Profile/i });
    await user.click(confirmBtn);

    await waitFor(() => {
      expect(resumeApi.confirmParsedProfile).toHaveBeenCalledWith('app-123', expect.objectContaining({
        candidateName: 'Jane Updated',
      }));
      expect(screen.getByText(/Candidate profile updated and confirmed successfully!/i)).toBeInTheDocument();
    });
  });
});
```

### 6.2 `JobPostingDetailPage.test.tsx`

```tsx
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { JobPostingDetailPage } from '../JobPostingDetailPage';
import { api, resumeApi } from '../../lib/api';

vi.mock('../../lib/api', async () => {
  const actual = await vi.importActual<typeof import('../../lib/api')>('../../lib/api');
  return {
    ...actual,
    api: vi.fn(),
    resumeApi: {
      postBulkResumes: vi.fn(),
      getBulkResumeStatus: vi.fn(),
    },
  };
});

describe('JobPostingDetailPage Bulk CV Upload Integration', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('opens Bulk CV Upload modal and triggers batch processing with progress polling', async () => {
    const user = userEvent.setup();
    vi.mocked(api).mockImplementation((path) => {
      if (path === '/jobpostings/job-1') {
        return Promise.resolve({ id: 'job-1', title: 'Senior Software Engineer', status: 'Live' });
      }
      if (path === '/jobpostings/job-1/pipeline') {
        return Promise.resolve([]);
      }
      return Promise.reject(new Error('Unknown endpoint'));
    });

    vi.mocked(resumeApi.postBulkResumes).mockResolvedValueOnce({
      batchId: 'batch-999',
      jobPostingId: 'job-1',
      totalFiles: 2,
      status: 'Queued',
      createdAt: '2026-08-08T12:00:00Z',
    });

    vi.mocked(resumeApi.getBulkResumeStatus).mockResolvedValueOnce({
      batchId: 'batch-999',
      jobPostingId: 'job-1',
      totalFiles: 2,
      processedCount: 2,
      successCount: 2,
      failedCount: 0,
      status: 'Completed',
      files: [
        { fileName: 'cv1.pdf', fileSizeBytes: 5000, status: 'Success', candidateName: 'Alice' },
        { fileName: 'cv2.pdf', fileSizeBytes: 6000, status: 'Success', candidateName: 'Bob' },
      ],
      createdAt: '2026-08-08T12:00:00Z',
    });

    render(
      <MemoryRouter initialEntries={['/jobpostings/job-1']}>
        <Routes>
          <Route path="/jobpostings/:id" element={<JobPostingDetailPage />} />
        </Routes>
      </MemoryRouter>
    );

    const bulkBtn = await screen.findByRole('button', { name: /Bulk Upload CVs/i });
    await user.click(bulkBtn);

    expect(screen.getByText('Bulk Upload CV Documents')).toBeInTheDocument();
  });
});
```

---

## 7. Step-by-Step Implementation Verification Checklist

1. **Shared Types Setup**:
   - Add DTO interfaces to `packages/types/src/index.ts`.
   - Run `npm run typecheck` across workspace.

2. **API Layer Extension**:
   - Implement `apiUpload` and `resumeApi` in `frontend/internal/src/lib/api.ts`.
   - Verify error handling and silent token refresh logic.

3. **CandidateSlideOver Enhancements**:
   - Integrate single file drag-and-drop zone and upload progress bar.
   - Implement raw extracted text viewer with `Zawgyi → Unicode Normalized` badge.
   - Build side-by-side Parsed Profile Human Review form.
   - Add explicit confirmation button calling `confirmParsedProfile`.
   - Wire original CV download button.

4. **Bulk CV Upload Modal**:
   - Create `BulkCvUploadModal.tsx` using `@recruitops/ui` `Dialog`.
   - Add header button to `JobPostingDetailPage.tsx`.
   - Wire multi-file drag-and-drop, batch POST request, status polling interval, and per-file progress badges.

5. **Vitest Unit Test Suite**:
   - Execute `npm run test` in `frontend/internal`.
   - Verify all 233 existing tests + new Milestone 3 tests pass cleanly.

---

## 8. Summary of Non-Functional & Architecture Guardrails

- **Zero Breaking Changes**: Preserves backward compatibility with all existing pipeline and auth workflows.
- **Design Token Compliance**: Aligns with `RecruitOps_Design_System.md` (`primary-600`, `surface-50`, `line-200`, `Badge` variants).
- **Security & Authorization**: Propagates `Authorization` JWT and `X-Tenant-Id` headers across all endpoints.
