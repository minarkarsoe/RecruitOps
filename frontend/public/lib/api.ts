// Resolves the API base URL for the PUBLIC app.
//
// Next.js Server Components run in Node and have no page origin, so a relative
// path like "/api" cannot be fetched there — it throws
// "TypeError: Failed to parse URL". Server-side calls must use an absolute URL;
// browser calls use the rewrite in next.config.mjs.
function baseUrl(): string {
  if (typeof window === 'undefined') {
    return process.env.API_INTERNAL_URL ?? 'http://localhost:5080/api';
  }
  return process.env.NEXT_PUBLIC_API_BASE_URL ?? '/api';
}

/**
 * A non-2xx answer. `message` never carries server text — `app/error.tsx` renders it to strangers
 * — so the ProblemDetails `detail` travels separately, and each caller decides whether that
 * status's wording is fit to show. Only two endpoints write their 409s for the public: the apply
 * endpoint (`PublicJobsController`) and the offer link (`PublicOffersController`); everything else
 * stays behind a message the caller writes itself.
 */
export class ApiError extends Error {
  constructor(
    readonly status: number,
    statusText: string,
    readonly detail: string | null,
  ) {
    super(`API ${status}: ${statusText}`);
    this.name = 'ApiError';
  }
}

async function problemDetail(res: Response): Promise<string | null> {
  try {
    const body: unknown = await res.json();
    if (typeof body === 'object' && body !== null && 'detail' in body) {
      const { detail } = body as { detail: unknown };
      return typeof detail === 'string' && detail.trim() ? detail : null;
    }
  } catch {
    // Not JSON (a proxy's HTML error page, an empty body): there is no detail to carry.
  }
  return null;
}

export async function api<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`${baseUrl()}${path}`, {
    cache: 'no-store',
    ...init,
    // ⚠️ `headers` MUST come after `...init`. It used to come before, which meant the merge
    // below was silently discarded: spreading `init` last replaced the whole `headers` key with
    // the caller's raw object, dropping `Content-Type: application/json`. Latent until
    // 2026-08-27 only because no call site passed a header — the next one would have sent a
    // JSON body with no content type and had it rejected or misparsed. Found by the first test
    // ever written for this app.
    headers: { 'Content-Type': 'application/json', ...(init?.headers ?? {}) },
  });
  if (!res.ok) throw new ApiError(res.status, res.statusText, await problemDetail(res));
  // Accepted / No Content carry no body (the offer link's "send a code" and "verify"), and
  // parsing one would throw on a call that succeeded.
  if (res.status === 202 || res.status === 204) return undefined as T;
  return res.json() as Promise<T>;
}

/**
 * A multipart POST (a file from the offer link's documents step). No `Content-Type` of ours: the
 * browser writes `multipart/form-data` with its boundary, and a JSON header would break the body.
 */
export async function upload<T>(path: string, form: FormData): Promise<T> {
  const res = await fetch(`${baseUrl()}${path}`, { method: 'POST', body: form, cache: 'no-store' });
  if (!res.ok) throw new ApiError(res.status, res.statusText, await problemDetail(res));
  return res.json() as Promise<T>;
}
