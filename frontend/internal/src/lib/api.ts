import { auth } from './auth';
import type {
  LoginResponse,
  ParseResumeRequest,
  ParsedResumeResult,
  MatchCandidateRequest,
  CandidateMatchAnalysis,
  GenerateExecutiveSummaryRequest,
  ExecutiveSummaryResult,
  PrepareDocumentRequest,
  DocumentPrepResult,
  BurmeseLocalizationRequest,
  BurmeseLocalizationResult,
  ResumeExtractionResult,
  ConfirmParsedProfileRequest,
  BulkResumeUploadResponse,
  BulkResumeBatchStatus,
  VersionInfo,
} from '@recruitops/types';

const BASE = import.meta.env.VITE_API_BASE_URL ?? '/api';

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    message: string,
    /**
     * Seconds from the `Retry-After` header, when the server sent one. Only a 429 does.
     *
     * Carried on the error because the header is the ONLY place the number exists — the
     * login lockout's countdown (ADR-0016) is otherwise unrenderable, and a screen that
     * invents "try again in a few minutes" is guessing at a value the server already knows.
     */
    public readonly retryAfterSeconds?: number
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

/** `Retry-After` in seconds, or undefined. Only the delta-seconds form is used by this API. */
function retryAfterSeconds(res: Response): number | undefined {
  const raw = res.headers.get('Retry-After');
  if (!raw) return undefined;
  const seconds = Number.parseInt(raw, 10);
  return Number.isFinite(seconds) && seconds >= 0 ? seconds : undefined;
}

let refreshPromise: Promise<LoginResponse | null> | null = null;

async function performSilentRefresh(refreshToken: string): Promise<LoginResponse | null> {
  if (!refreshPromise) {
    refreshPromise = fetch(`${BASE}/auth/refresh`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ refreshToken }),
    })
      .then(async (res) => {
        if (!res.ok) return null;
        const data = (await res.json()) as LoginResponse;
        auth.set(data);
        return data;
      })
      .catch(() => null)
      .finally(() => {
        refreshPromise = null;
      });
  }
  return refreshPromise;
}

/**
 * Fetch wrapper for the internal SPA. Always runs in the browser, so a relative
 * base URL is fine — unlike the public Next.js app, whose Server Components have
 * no origin and need an absolute URL.
 */
export async function apiFetch<T>(path: string, init?: RequestInit): Promise<T> {
  let session = auth.get();

  let res = await fetch(`${BASE}${path}`, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      ...(session ? { Authorization: `Bearer ${session.accessToken}` } : {}),
      ...(session?.activeTenantId ? { 'X-Tenant-Id': session.activeTenantId } : {}),
      ...(init?.headers ?? {}),
    },
  });

  if (res.status === 401 && session?.refreshToken && path !== '/auth/refresh' && path !== '/auth/login') {
    const refreshed = await performSilentRefresh(session.refreshToken);
    if (refreshed) {
      // Retry original request with new access token
      res = await fetch(`${BASE}${path}`, {
        ...init,
        headers: {
          'Content-Type': 'application/json',
          Authorization: `Bearer ${refreshed.accessToken}`,
          ...(refreshed.activeTenantId ?? session.activeTenantId
            ? { 'X-Tenant-Id': refreshed.activeTenantId ?? session.activeTenantId }
            : {}),
          ...(init?.headers ?? {}),
        },
      });
    } else {
      auth.clear();
      throw new ApiError(401, 'Your session has expired. Please sign in again.');
    }
  }

  if (res.status === 401) {
    // ⚠️ Not on the login endpoint. A 401 there means the credentials were wrong — the caller
    // never had a session to expire — and telling someone who just mistyped their password
    // that their session expired sends them looking for a problem that does not exist.
    // ADR-0016: that 401 carries no body, so the copy is the page's to choose.
    if (path === '/auth/login') {
      throw new ApiError(401, 'Email or password is incorrect.');
    }
    auth.clear();
    throw new ApiError(401, 'Your session has expired. Please sign in again.');
  }

  if (!res.ok) {
    throw new ApiError(res.status, await readError(res), retryAfterSeconds(res));
  }

  // 204 and empty bodies are valid successes.
  const text = await res.text();
  return (text ? JSON.parse(text) : undefined) as T;
}

/**
 * Helper for FormData multipart requests (e.g. CV file uploads).
 * Intentionally omits 'Content-Type' header so fetch automatically inserts boundary.
 */
export async function apiUpload<T>(path: string, formData: FormData): Promise<T> {
  const session = auth.get();

  let res = await fetch(`${BASE}${path}`, {
    method: 'POST',
    headers: {
      ...(session ? { Authorization: `Bearer ${session.accessToken}` } : {}),
      ...(session?.activeTenantId ? { 'X-Tenant-Id': session.activeTenantId } : {}),
    },
    body: formData,
  });

  if (res.status === 401 && session?.refreshToken && path !== '/auth/refresh' && path !== '/auth/login') {
    const refreshed = await performSilentRefresh(session.refreshToken);
    if (refreshed) {
      res = await fetch(`${BASE}${path}`, {
        method: 'POST',
        headers: {
          Authorization: `Bearer ${refreshed.accessToken}`,
          ...(refreshed.activeTenantId ?? session.activeTenantId
            ? { 'X-Tenant-Id': refreshed.activeTenantId ?? session.activeTenantId }
            : {}),
        },
        body: formData,
      });
      if (!res.ok) throw new ApiError(res.status, await readError(res));
      const retryText = await res.text();
      return (retryText ? JSON.parse(retryText) : undefined) as T;
    }
    auth.clear();
    throw new ApiError(401, 'Your session has expired. Please sign in again.');
  }

  if (res.status === 401) {
    // No login special-case here: uploads are multipart and sign-in never is.
    auth.clear();
    throw new ApiError(401, 'Your session has expired. Please sign in again.');
  }

  if (!res.ok) {
    throw new ApiError(res.status, await readError(res), retryAfterSeconds(res));
  }

  const text = await res.text();
  return (text ? JSON.parse(text) : undefined) as T;
}

async function readError(res: Response): Promise<string> {
  try {
    const problem = await res.json();
    // `??` alone only falls through on null/undefined, so `{"detail":""}` would win over the
    // `title` fallback and leave the caller with an empty (falsy) message — which every page
    // that renders `{error && <p role="alert">}` then silently drops. Treat blank/whitespace
    // the same as absent.
    const detail = typeof problem?.detail === 'string' ? problem.detail.trim() : '';
    if (detail) return detail;
    const title = typeof problem?.title === 'string' ? problem.title.trim() : '';
    if (title) return title;
    return `Request failed (${res.status})`;
  } catch {
    return `Request failed (${res.status})`;
  }
}

/** Alias kept for backwards compatibility — prefer `apiFetch` for new code. */
export { apiFetch as api };

// ── Single & Bulk Resume API client namespace ──────────────────────────────
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
    const res = await fetch(`${BASE}/applications/${applicationId}/resume`, {
      headers: session ? { Authorization: `Bearer ${session.accessToken}` } : {},
    });
    if (!res.ok) throw new ApiError(res.status, 'Failed to download resume document.');
    return await res.blob();
  },

  /** Recruiter explicit confirmation of parsed profile fields */
  confirmParsedProfile: (applicationId: string, profileData: ConfirmParsedProfileRequest): Promise<void> =>
    apiFetch<void>(`/applications/${applicationId}/profile`, {
      method: 'PUT',
      body: JSON.stringify(profileData),
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

// ── Hybrid AI API client namespace ─────────────────────────────────────────
// Claude handles data analytics (resume parsing, candidate matching).
// Gemini handles document generation & Burmese localization (ADR-0021).
export const aiApi = {
  /** Claude: Parse & structure resume text into candidate profile fields. */
  parseResume: (req: ParseResumeRequest): Promise<ParsedResumeResult> =>
    apiFetch<ParsedResumeResult>('/ai/claude/parse-resume', {
      method: 'POST',
      body: JSON.stringify(req),
    }),

  /** Claude: Score candidate fit against a job posting. */
  matchCandidate: (req: MatchCandidateRequest): Promise<CandidateMatchAnalysis> =>
    apiFetch<CandidateMatchAnalysis>('/ai/claude/match-candidate', {
      method: 'POST',
      body: JSON.stringify(req),
    }),

  /** Gemini: Generate a client-safe or internal executive summary. */
  generateExecutiveSummary: (req: GenerateExecutiveSummaryRequest): Promise<ExecutiveSummaryResult> =>
    apiFetch<ExecutiveSummaryResult>('/ai/gemini/executive-summary', {
      method: 'POST',
      body: JSON.stringify(req),
    }),

  /** Gemini: Prepare a formatted interview kit, client dossier, or JD document. */
  prepareDocument: (req: PrepareDocumentRequest): Promise<DocumentPrepResult> =>
    apiFetch<DocumentPrepResult>('/ai/gemini/document-prep', {
      method: 'POST',
      body: JSON.stringify(req),
    }),

  /** Gemini: Translate between English and Burmese. */
  translateBurmese: (req: BurmeseLocalizationRequest): Promise<BurmeseLocalizationResult> =>
    apiFetch<BurmeseLocalizationResult>('/ai/gemini/burmese-localization', {
      method: 'POST',
      body: JSON.stringify(req),
    }),

  /** Get system version metadata and active feature flags. */
  version: (): Promise<VersionInfo> => apiFetch<VersionInfo>('/version'),
};


