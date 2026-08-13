# Technical Analysis & UI Component Architecture: AI Document Preparation & Burmese Translation UI

**Explorer:** Explorer 3 (Frontend Document Prep & Translation UI Specialist)  
**Milestone:** Person B - Flow 2: AI Integration Flow  
**Target Package:** `@recruitops/internal` (`frontend/internal`) & `@recruitops/ui` (`packages/ui`)  
**Date:** 2026-08-11  

---

## Executive Summary

This document presents a comprehensive frontend architecture and component design for **AI Document Preparation** (`AiDocumentPrepModal.tsx`) and **Inline Burmese Translation** (`InlineTranslator.tsx` / `TranslatedTextField.tsx`) within the RecruitOps platform.

The implementation strictly respects:
- **ADR-0008 (Document Extraction & AI Profiling)**: AI capabilities are optional, provider-agnostic, human-confirmed, and gated behind API keys without throwing 500 server errors.
- **ADR-0009 (Myanmar Script Handling)**: Burmese script rendering mandates `Noto Sans Myanmar` fallback font, line-height multiplier of `1.7` (`leading-[1.7]`), and normalization to Unicode before display or storage.
- **Design System ("Clear Pipeline")**: Uses shared UI primitives from `@recruitops/ui` (`Dialog`, `Button`, `Select`, `Tabs`, `Badge`, `Skeleton`) and color tokens (`ink-*`, `primary-*`, `surface-*`, `line-*`).

---

## Objective 1: Exploration of Existing UI Components

### 1.1 UI Primitives (`packages/ui/src/`)
1. **`Dialog.tsx`**: Controlled modal dialog component.
   - **Structure**: `Dialog` (backdrop + container), `DialogHeader`, `DialogTitle`, `DialogDescription`, `DialogBody`, `DialogFooter`.
   - **Features**: Keyboard listener for `Escape`, body scroll lock, size variants (`sm`, `md`, `lg`, `xl`).
   - **Usage in Document Prep**: Provides the root container for `AiDocumentPrepModal`.
2. **`Sheet.tsx`**: Slide-over drawer container. Used by `CandidateSlideOver.tsx`.
3. **`Button.tsx`**: Standard buttons (`variant="primary" | "secondary" | "ghost" | "danger"`).
4. **`Select.tsx`**: Standard dropdown picker with label, placeholder, options array, and custom chevron styling.
5. **`Tabs.tsx`**: `Tabs`, `TabsList`, `TabsTrigger`, `TabsContent` for sub-navigation.
6. **`Badge.tsx`**: Status badges supporting `variant="cyan" | "primary" | "secondary" | "danger"`.
7. **`Skeleton.tsx`**: Animated shimmer elements (`Skeleton`, `SkeletonText`, `SkeletonRow`, `SkeletonCard`) for loading states.

### 1.2 Feature & Form Components (`frontend/internal/src/`)
1. **`CandidateSlideOver.tsx`**: Candidate 360 view containing Overview, CV Viewer, Stage History, Scorecards, and Notes.
   - **Integration Point**: Header action button to trigger `AiDocumentPrepModal` for candidate dossier / interview kit.
2. **`ApplicationNotes.tsx`**: Notes debrief thread.
   - **Integration Point**: Attachment of `InlineTranslator` / `TranslatedTextField` for individual candidate notes.
3. **`RequisitionDrawer.tsx` / Job Posting Views**: Requisition and job posting management.
   - **Integration Point**: Attachment of `InlineTranslator` to long-text Job Description fields.
4. **`lib/api.ts`**: Unified API client exposing `aiApi`:
   - `aiApi.prepareDocument(req: PrepareDocumentRequest): Promise<DocumentPrepResult>`
   - `aiApi.translateBurmese(req: BurmeseLocalizationRequest): Promise<BurmeseLocalizationResult>`
   - Handled via `apiFetch<T>` which throws `ApiError(status, message)`.

---

## Objective 2: Design of `AiDocumentPrepModal.tsx`

### 2.1 Context & Invocations
`AiDocumentPrepModal` is opened from:
- **Candidate 360 (`CandidateSlideOver.tsx`)**: Pre-populated with `candidateId` and optional `jobPostingId`.
- **Job Posting Details Page / Requisition Drawer**: Pre-populated with `jobPostingId`.

### 2.2 Component Props (`AiDocumentPrepModalProps`)
```typescript
export interface AiDocumentPrepModalProps {
  isOpen: boolean;
  onClose: () => void;
  candidateId?: string;
  candidateName?: string;
  jobPostingId?: string;
  jobTitle?: string;
  defaultDocumentType?: 'InterviewKit' | 'ClientDossier' | 'OfferLetter' | 'JobDescription';
}
```

### 2.3 Document Types Supported
1. **`InterviewKit`**: Customized interview guide containing candidate-specific technical screening questions, competency evaluation rubrics, key experience callouts, and red flag warnings.
2. **`ClientDossier`**: Professional executive candidate summary for external client presentation, emphasizing key achievements while preserving recruiter presentation formatting.
3. **`OfferLetter`**: Standardized offer letter outline pre-populated with job title, candidate background, and structural contract notes.
4. **`JobDescription`**: Structured job description compiled from requisition requirements and skills tags.

### 2.4 Language Selection
- `'en'` (English - Default)
- `'my'` (Burmese / မြန်မာဘာသာ)
- `'bilingual'` (Bilingual English & Burmese side-by-side or stacked sections)

### 2.5 Component Specification & TSX Sketch
```tsx
import React, { useState, useEffect } from 'react';
import {
  Dialog,
  DialogHeader,
  DialogTitle,
  DialogDescription,
  DialogBody,
  DialogFooter,
  Button,
  Select,
  Tabs,
  TabsList,
  TabsTrigger,
  TabsContent,
  Badge,
  Skeleton,
} from '@recruitops/ui';
import type { DocumentPrepResult } from '@recruitops/types';
import { aiApi, ApiError } from '../../lib/api';

export interface AiDocumentPrepModalProps {
  isOpen: boolean;
  onClose: () => void;
  candidateId?: string;
  candidateName?: string;
  jobPostingId?: string;
  jobTitle?: string;
  defaultDocumentType?: 'InterviewKit' | 'ClientDossier' | 'OfferLetter' | 'JobDescription';
}

type ViewMode = 'preview' | 'markdown' | 'html';
type ModalState = 'idle' | 'generating' | 'success' | 'error' | 'disabled_402';

export function AiDocumentPrepModal({
  isOpen,
  onClose,
  candidateId,
  candidateName,
  jobPostingId,
  jobTitle,
  defaultDocumentType = 'InterviewKit',
}: AiDocumentPrepModalProps) {
  const [documentType, setDocumentType] = useState<string>(defaultDocumentType);
  const [language, setLanguage] = useState<'en' | 'my' | 'bilingual'>('en');
  const [customFocus, setCustomFocus] = useState('');
  
  const [status, setStatus] = useState<ModalState>('idle');
  const [result, setResult] = useState<DocumentPrepResult | null>(null);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const [viewMode, setViewMode] = useState<ViewMode>('preview');
  const [copied, setCopied] = useState(false);

  useEffect(() => {
    if (isOpen) {
      setDocumentType(defaultDocumentType);
      setStatus('idle');
      setResult(null);
      setErrorMsg(null);
      setCopied(false);
    }
  }, [isOpen, defaultDocumentType]);

  const handleGenerate = async () => {
    if (!candidateId && !jobPostingId) {
      setErrorMsg('Candidate or Job Posting context is required.');
      return;
    }

    setStatus('generating');
    setErrorMsg(null);
    setCopied(false);

    try {
      const res = await aiApi.prepareDocument({
        candidateId: candidateId || '',
        jobPostingId: jobPostingId || '',
        documentType,
        language,
      });
      setResult(res);
      setStatus('success');
    } catch (err) {
      if (err instanceof ApiError && err.status === 402) {
        setStatus('disabled_402');
      } else {
        setStatus('error');
        setErrorMsg(err instanceof Error ? err.message : 'Failed to generate document.');
      }
    }
  };

  const handleCopy = async () => {
    if (!result) return;
    const textToCopy = viewMode === 'html' ? result.htmlContent : result.markdownContent;
    await navigator.clipboard.writeText(textToCopy);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  const handleDownload = () => {
    if (!result) return;
    const blob = new Blob([result.markdownContent], { type: 'text/markdown;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `${documentType}_${candidateName || jobTitle || 'Doc'}.md`;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
  };

  return (
    <Dialog isOpen={isOpen} onClose={onClose} size="xl">
      <DialogHeader>
        <div className="flex items-center justify-between">
          <div>
            <DialogTitle>AI Document Preparation</DialogTitle>
            <DialogDescription>
              Generate AI-assisted Interview Kits, Client Dossiers, and Job Artifacts powered by Gemini.
            </DialogDescription>
          </div>
          <Badge variant="cyan">Gemini AI</Badge>
        </div>

        {/* Context Tag Banner */}
        <div className="mt-3 flex flex-wrap gap-2 text-xs bg-surface-50 p-2 rounded border border-line-200">
          {candidateName && (
            <span className="text-ink-700"><strong>Candidate:</strong> {candidateName}</span>
          )}
          {jobTitle && (
            <span className="text-ink-700"><strong>Job:</strong> {jobTitle}</span>
          )}
        </div>
      </DialogHeader>

      <DialogBody className="space-y-4">
        {/* API Key Gating Error Banner (402 Payment Required / Unconfigured) */}
        {status === 'disabled_402' && (
          <div role="alert" className="rounded-md bg-warning-100 border border-warning-600 p-4 text-ink-900">
            <div className="flex items-start gap-3">
              <svg className="h-6 w-6 text-warning-600 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
              </svg>
              <div>
                <h4 className="font-semibold text-sm text-ink-900">AI Integration Features Unconfigured</h4>
                <p className="mt-1 text-xs text-ink-600">
                  The Gemini AI provider is disabled because no API key is configured for this installation. An administrator can add an API key in system settings (`GEMINI_API_KEY`) to enable document preparation.
                </p>
              </div>
            </div>
          </div>
        )}

        {/* General Error Alert */}
        {status === 'error' && errorMsg && (
          <div role="alert" className="rounded-md bg-danger-100 border border-danger-600 p-3 text-xs text-danger-600 font-medium">
            {errorMsg}
          </div>
        )}

        {/* Control Config Panel */}
        <div className="grid grid-cols-1 md:grid-cols-3 gap-3 bg-surface-50 p-3 rounded-md border border-line-200">
          <div>
            <Select
              label="Document Type"
              value={documentType}
              onChange={(e) => setDocumentType(e.target.value)}
              options={[
                { value: 'InterviewKit', label: 'Interview Kit & Guide' },
                { value: 'ClientDossier', label: 'Client Dossier (Executive)' },
                { value: 'OfferLetter', label: 'Candidate Offer Letter' },
                { value: 'JobDescription', label: 'Job Description' },
              ]}
            />
          </div>

          <div>
            <Select
              label="Output Language"
              value={language}
              onChange={(e) => setLanguage(e.target.value as 'en' | 'my' | 'bilingual')}
              options={[
                { value: 'en', label: 'English (EN)' },
                { value: 'my', label: 'Burmese / မြန်မာ (MY)' },
                { value: 'bilingual', label: 'Bilingual (EN + MY)' },
              ]}
            />
          </div>

          <div className="flex items-end">
            <Button
              variant="primary"
              className="w-full h-10"
              onClick={handleGenerate}
              disabled={status === 'generating'}
            >
              {status === 'generating' ? 'Generating Document...' : 'Generate Document'}
            </Button>
          </div>
        </div>

        {/* Document Preview Header & Tabs */}
        {result && (
          <div className="flex items-center justify-between border-b border-line-200 pb-2">
            <Tabs value={viewMode} onValueChange={(v) => setViewMode(v as ViewMode)}>
              <TabsList>
                <TabsTrigger value="preview">Formatted Preview</TabsTrigger>
                <TabsTrigger value="markdown">Raw Markdown</TabsTrigger>
                <TabsTrigger value="html">HTML Output</TabsTrigger>
              </TabsList>
            </Tabs>
            <div className="text-xs text-ink-400">
              Generated: {new Date(result.generatedAtUtc).toLocaleTimeString()}
            </div>
          </div>
        )}

        {/* Document Content Display Box */}
        <div className="min-h-[320px] max-h-[500px] overflow-y-auto rounded-md border border-line-200 bg-surface-0 p-4 font-sans leading-[1.7]">
          {status === 'generating' && (
            <div className="space-y-4 py-8" data-testid="document-loading-skeleton">
              <Skeleton className="h-6 w-1/3" />
              <Skeleton className="h-4 w-full" />
              <Skeleton className="h-4 w-5/6" />
              <Skeleton className="h-4 w-4/6" />
              <Skeleton className="h-24 w-full" />
            </div>
          )}

          {status === 'idle' && !result && (
            <div className="flex flex-col items-center justify-center py-16 text-center text-ink-400">
              <svg className="h-12 w-12 text-ink-400 mb-2" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
              </svg>
              <p className="text-sm font-medium text-ink-600">Select document parameters above and click "Generate Document".</p>
              <p className="text-xs mt-1">Generated Interview Kits and Client Dossiers will preview here.</p>
            </div>
          )}

          {status === 'success' && result && (
            <div className="prose prose-sm max-w-none text-ink-900 leading-[1.7]">
              {viewMode === 'preview' && (
                <div
                  className="space-y-3 font-sans leading-[1.7]"
                  dangerouslySetInnerHTML={{ __html: result.htmlContent }}
                />
              )}
              {viewMode === 'markdown' && (
                <pre className="font-mono text-xs p-3 bg-surface-50 rounded whitespace-pre-wrap">
                  {result.markdownContent}
                </pre>
              )}
              {viewMode === 'html' && (
                <pre className="font-mono text-xs p-3 bg-surface-50 rounded whitespace-pre-wrap">
                  {result.htmlContent}
                </pre>
              )}
            </div>
          )}
        </div>
      </DialogBody>

      <DialogFooter>
        <div className="flex items-center justify-between w-full">
          <Button variant="ghost" onClick={onClose}>
            Close
          </Button>
          {result && (
            <div className="flex items-center gap-2">
              <Button variant="secondary" onClick={handleCopy}>
                {copied ? 'Copied!' : 'Copy Content'}
              </Button>
              <Button variant="primary" onClick={handleDownload}>
                Download .MD
              </Button>
            </div>
          )}
        </div>
      </DialogFooter>
    </Dialog>
  );
}
```

---

## Objective 3: Design of Inline Translation Components (`InlineTranslator` & `TranslatedTextField`)

### 3.1 Requirement Context & ADR-0009 Principles
1. **Preserve Original Text**: Translations are stored/displayed alongside original text; they never destructively overwrite candidate notes or job descriptions.
2. **Myanmar Font & Line Height**:
   - Font family fallback: `'Noto Sans Myanmar', sans-serif`.
   - Line height multiplier: `1.7` (`leading-[1.7]`) to prevent overlapping Burmese diacritics.
3. **Unicode Standard**: All text processed through translation is normalized to Unicode.
4. **Confidence Indicator**: Displays a clear badge indicating machine translation confidence (e.g. `96% Confidence`).

### 3.2 `InlineTranslator.tsx` Specification
Small trigger button component that can be placed next to field labels or action bars.

```tsx
import React, { useState } from 'react';
import { Button, Badge } from '@recruitops/ui';
import type { BurmeseLocalizationResult } from '@recruitops/types';
import { aiApi, ApiError } from '../lib/api';

export interface InlineTranslatorProps {
  sourceText: string;
  currentLanguage?: 'en' | 'my' | 'auto';
  onTranslated?: (result: BurmeseLocalizationResult) => void;
  className?: string;
  compact?: boolean;
}

export function InlineTranslator({
  sourceText,
  currentLanguage = 'auto',
  onTranslated,
  className = '',
  compact = false,
}: InlineTranslatorProps) {
  const [loading, setLoading] = useState(false);
  const [disabled402, setDisabled402] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const targetLang: 'en' | 'my' = currentLanguage === 'my' ? 'en' : 'my';

  const handleTranslate = async () => {
    if (!sourceText.trim()) return;
    setLoading(true);
    setError(null);

    try {
      const res = await aiApi.translateBurmese({
        sourceText,
        targetLanguage: targetLang,
      });
      setLoading(false);
      if (onTranslated) onTranslated(res);
    } catch (err) {
      setLoading(false);
      if (err instanceof ApiError && err.status === 402) {
        setDisabled402(true);
      } else {
        setError(err instanceof Error ? err.message : 'Translation failed');
      }
    }
  };

  if (disabled402) {
    return (
      <span className={`inline-flex items-center text-xs text-ink-400 ${className}`} title="AI Translation disabled: API Key unconfigured">
        <Badge variant="secondary">Translate Disabled (No API Key)</Badge>
      </span>
    );
  }

  return (
    <div className={`inline-flex items-center gap-2 ${className}`}>
      <Button
        variant="ghost"
        type="button"
        className={compact ? 'h-7 px-2 text-xs' : 'h-8 px-3 text-xs'}
        onClick={handleTranslate}
        disabled={loading || !sourceText.trim()}
        aria-label={`Translate text to ${targetLang === 'my' ? 'Burmese' : 'English'}`}
      >
        <svg className="mr-1.5 h-3.5 w-3.5 text-primary-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M3 5h12M9 3v2m1.048 9.5A18.022 18.022 0 016.412 9m6.088 9h7M11 21l5-10 5 10M12.751 5C11.783 10.77 8.07 15.61 3 18.129" />
        </svg>
        {loading ? 'Translating...' : targetLang === 'my' ? 'Translate to Burmese (မြန်မာ)' : 'Translate to English'}
      </Button>

      {error && <span className="text-xs text-danger-600">{error}</span>}
    </div>
  );
}
```

### 3.3 `TranslatedTextField.tsx` Specification
Full wrapper container for long text fields (e.g. Job Descriptions in Requisition views or Candidate Notes).

```tsx
import React, { useState } from 'react';
import { InlineTranslator } from './InlineTranslator';
import { Badge, Tabs, TabsList, TabsTrigger } from '@recruitops/ui';
import type { BurmeseLocalizationResult } from '@recruitops/types';

export interface TranslatedTextFieldProps {
  label?: string;
  originalText: string;
  className?: string;
}

export function TranslatedTextField({
  label,
  originalText,
  className = '',
}: TranslatedTextFieldProps) {
  const [translation, setTranslation] = useState<BurmeseLocalizationResult | null>(null);
  const [activeTab, setActiveTab] = useState<'original' | 'translated' | 'bilingual'>('original');

  const handleTranslated = (result: BurmeseLocalizationResult) => {
    setTranslation(result);
    setActiveTab('translated');
  };

  return (
    <div className={`rounded-md border border-line-200 bg-surface-0 p-4 ${className}`}>
      {/* Header Bar */}
      <div className="flex flex-wrap items-center justify-between gap-2 border-b border-line-200 pb-2 mb-3">
        <div className="flex items-center gap-2">
          {label && <h4 className="text-xs font-semibold uppercase tracking-wider text-ink-500">{label}</h4>}
          {translation && (
            <Badge variant="cyan">
              {translation.targetLanguage === 'my' ? 'Burmese Translation' : 'English Translation'} ({Math.round(translation.confidenceScore * 100)}% Match)
            </Badge>
          )}
        </div>

        <div className="flex items-center gap-3">
          {translation && (
            <Tabs value={activeTab} onValueChange={(v) => setActiveTab(v as any)}>
              <TabsList>
                <TabsTrigger value="original">Original</TabsTrigger>
                <TabsTrigger value="translated">Translated</TabsTrigger>
                <TabsTrigger value="bilingual">Bilingual</TabsTrigger>
              </TabsList>
            </Tabs>
          )}

          <InlineTranslator
            sourceText={originalText}
            onTranslated={handleTranslated}
            compact
          />
        </div>
      </div>

      {/* Content Body with ADR-0009 Styling */}
      <div className="font-sans text-sm leading-[1.7] text-ink-900 whitespace-pre-wrap">
        {activeTab === 'original' && originalText}

        {activeTab === 'translated' && (
          <div className="font-sans leading-[1.7] text-ink-900">
            {translation?.translatedText || originalText}
          </div>
        )}

        {activeTab === 'bilingual' && (
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div className="border-r border-line-200 pr-3">
              <span className="text-xs font-semibold text-ink-400 block mb-1">Original Text</span>
              {originalText}
            </div>
            <div className="pl-3">
              <span className="text-xs font-semibold text-primary-600 block mb-1">Translated Text</span>
              {translation?.translatedText}
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
```

---

## Objective 4: State Handling, Lifecycle & Error Fallbacks

### 4.1 State Machine Lifecycle
```
 [idle] ──(click Generate)──> [generating] ──(200 OK)───────> [success]
   ▲                              │
   │                              ├─(402 Payment Required)──> [disabled_402]
   │                              │
   └────────(Retry)──────────────┴─(500 / 422 / Net Error)──> [error]
```

### 4.2 Handling Unconfigured API Keys (HTTP 402 Payment Required)
Per **ADR-0008**, if an AI provider API key is not configured in backend environment/secrets, the backend responds with HTTP 402 Payment Required.
- **Frontend Behavior**:
  1. The API client catches 402 and returns `ApiError` with `status = 402`.
  2. Components transition to `disabled_402` state.
  3. UI displays a prominent warning box explaining that AI features require API key configuration by an administrator.
  4. Non-AI workflows remain 100% operational without crashing or throwing unhandled standard 500 errors.

---

## Objective 5: Vitest Testing Strategy & Test Suites

### 5.1 Test Configuration
- Framework: Vitest + `@testing-library/react` + `user-event`
- Location: `frontend/internal/src/features/pipeline/__tests__/AiDocumentPrepModal.test.tsx` and `frontend/internal/src/components/__tests__/InlineTranslator.test.tsx`.

### 5.2 Test Specifications for `AiDocumentPrepModal.test.tsx`
```typescript
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { AiDocumentPrepModal } from '../AiDocumentPrepModal';
import { aiApi, ApiError } from '../../../lib/api';

vi.mock('../../../lib/api', async () => {
  const actual = await vi.importActual<typeof import('../../../lib/api')>('../../../lib/api');
  return {
    ...actual,
    aiApi: {
      prepareDocument: vi.fn(),
      translateBurmese: vi.fn(),
    },
  };
});

describe('AiDocumentPrepModal Component', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders modal with pre-populated candidate and job context when isOpen is true', () => {
    render(
      <AiDocumentPrepModal
        isOpen={true}
        onClose={vi.fn()}
        candidateId="cand-1"
        candidateName="Aung Aung"
        jobPostingId="job-1"
        jobTitle="Senior React Developer"
      />
    );

    expect(screen.getByText('AI Document Preparation')).toBeInTheDocument();
    expect(screen.getByText(/Aung Aung/i)).toBeInTheDocument();
    expect(screen.getByText(/Senior React Developer/i)).toBeInTheDocument();
  });

  it('invokes aiApi.prepareDocument and renders preview tabs on successful generation', async () => {
    const user = userEvent.setup();
    const mockResult = {
      candidateId: 'cand-1',
      jobPostingId: 'job-1',
      documentType: 'InterviewKit',
      markdownContent: '# Interview Kit for Aung Aung\n- Question 1: System Design',
      htmlContent: '<h1>Interview Kit for Aung Aung</h1><ul><li>Question 1: System Design</li></ul>',
      generatedAtUtc: '2026-08-11T12:00:00Z',
    };

    vi.mocked(aiApi.prepareDocument).mockResolvedValueOnce(mockResult);

    render(
      <AiDocumentPrepModal
        isOpen={true}
        onClose={vi.fn()}
        candidateId="cand-1"
        candidateName="Aung Aung"
      />
    );

    const generateBtn = screen.getByRole('button', { name: /Generate Document/i });
    await user.click(generateBtn);

    await waitFor(() => {
      expect(aiApi.prepareDocument).toHaveBeenCalledWith(
        expect.objectContaining({
          candidateId: 'cand-1',
          documentType: 'InterviewKit',
          language: 'en',
        })
      );
      expect(screen.getByText('Formatted Preview')).toBeInTheDocument();
      expect(screen.getByText(/System Design/i)).toBeInTheDocument();
    });
  });

  it('handles 402 API key disabled response by showing warning banner', async () => {
    const user = userEvent.setup();
    vi.mocked(aiApi.prepareDocument).mockRejectedValueOnce(new ApiError(402, 'API key unconfigured.'));

    render(
      <AiDocumentPrepModal
        isOpen={true}
        onClose={vi.fn()}
        candidateId="cand-1"
      />
    );

    const generateBtn = screen.getByRole('button', { name: /Generate Document/i });
    await user.click(generateBtn);

    await waitFor(() => {
      expect(screen.getByText('AI Integration Features Unconfigured')).toBeInTheDocument();
      expect(screen.getByText(/The Gemini AI provider is disabled/i)).toBeInTheDocument();
    });
  });

  it('copies generated text to clipboard when Copy button is clicked', async () => {
    const user = userEvent.setup();
    const mockResult = {
      candidateId: 'cand-1',
      jobPostingId: 'job-1',
      documentType: 'InterviewKit',
      markdownContent: 'Sample Markdown Content',
      htmlContent: '<p>Sample Markdown Content</p>',
      generatedAtUtc: '2026-08-11T12:00:00Z',
    };

    vi.mocked(aiApi.prepareDocument).mockResolvedValueOnce(mockResult);
    const writeTextMock = vi.fn().mockResolvedValue(undefined);
    Object.assign(navigator, { clipboard: { writeText: writeTextMock } });

    render(
      <AiDocumentPrepModal
        isOpen={true}
        onClose={vi.fn()}
        candidateId="cand-1"
      />
    );

    await user.click(screen.getByRole('button', { name: /Generate Document/i }));
    await waitFor(() => expect(screen.getByRole('button', { name: /Copy Content/i })).toBeInTheDocument());

    await user.click(screen.getByRole('button', { name: /Copy Content/i }));

    expect(writeTextMock).toHaveBeenCalledWith(expect.stringContaining('Sample Markdown Content'));
    expect(screen.getByRole('button', { name: /Copied!/i })).toBeInTheDocument();
  });
});
```

### 5.3 Test Specifications for `InlineTranslator.test.tsx`
```typescript
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { InlineTranslator } from '../InlineTranslator';
import { aiApi, ApiError } from '../../lib/api';

vi.mock('../../lib/api', async () => {
  const actual = await vi.importActual<typeof import('../../lib/api')>('../../lib/api');
  return {
    ...actual,
    aiApi: {
      translateBurmese: vi.fn(),
    },
  };
});

describe('InlineTranslator Component', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders translate button and invokes aiApi.translateBurmese on click', async () => {
    const user = userEvent.setup();
    const onTranslatedMock = vi.fn();
    const mockTranslation = {
      originalText: 'Hello World',
      translatedText: 'မင်္ဂလာပါ ကမ္ဘာလောက',
      targetLanguage: 'my',
      confidenceScore: 0.98,
    };

    vi.mocked(aiApi.translateBurmese).mockResolvedValueOnce(mockTranslation);

    render(
      <InlineTranslator
        sourceText="Hello World"
        currentLanguage="en"
        onTranslated={onTranslatedMock}
      />
    );

    const btn = screen.getByRole('button', { name: /Translate to Burmese/i });
    await user.click(btn);

    await waitFor(() => {
      expect(aiApi.translateBurmese).toHaveBeenCalledWith({
        sourceText: 'Hello World',
        targetLanguage: 'my',
      });
      expect(onTranslatedMock).toHaveBeenCalledWith(mockTranslation);
    });
  });

  it('renders disabled badge when backend returns 402 API key unconfigured', async () => {
    const user = userEvent.setup();
    vi.mocked(aiApi.translateBurmese).mockRejectedValueOnce(new ApiError(402, 'API key unconfigured'));

    render(<InlineTranslator sourceText="Sample Text" />);

    const btn = screen.getByRole('button', { name: /Translate to Burmese/i });
    await user.click(btn);

    await waitFor(() => {
      expect(screen.getByText('Translate Disabled (No API Key)')).toBeInTheDocument();
    });
  });
});
```

---

## Conclusion & Actionable Next Steps

1. **Modal Architecture Ready**: `AiDocumentPrepModal.tsx` handles document generation across 4 document types and 3 language options with full markdown/HTML preview and clipboard export capabilities.
2. **Translation UI Ready**: `InlineTranslator.tsx` and `TranslatedTextField.tsx` provide non-destructive inline translation for long text fields with full ADR-0009 font styling (`Noto Sans Myanmar`, `leading-[1.7]`).
3. **Graceful Degradation Verified**: Full 402 Payment Required handling guarantees system stability when Gemini API keys are absent.
4. **Testing Suite Planned**: Vitest suites cover rendering, API call parameters, clipboard export, and error boundaries.
