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

export async function api<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`${baseUrl()}${path}`, {
    headers: { 'Content-Type': 'application/json', ...(init?.headers ?? {}) },
    cache: 'no-store',
    ...init,
  });
  if (!res.ok) throw new Error(`API ${res.status}: ${res.statusText}`);
  return res.json() as Promise<T>;
}
