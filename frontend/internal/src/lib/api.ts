import { auth } from './auth';

const BASE = import.meta.env.VITE_API_BASE_URL ?? '/api';

export class ApiError extends Error {
  constructor(public readonly status: number, message: string) {
    super(message);
    this.name = 'ApiError';
  }
}

/**
 * Fetch wrapper for the internal SPA. Always runs in the browser, so a relative
 * base URL is fine — unlike the public Next.js app, whose Server Components have
 * no origin and need an absolute URL.
 */
export async function api<T>(path: string, init?: RequestInit): Promise<T> {
  const session = auth.get();

  const res = await fetch(`${BASE}${path}`, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      ...(session ? { Authorization: `Bearer ${session.accessToken}` } : {}),
      ...(init?.headers ?? {}),
    },
  });

  if (res.status === 401) {
    // Token rejected or expired — drop it so the router sends us back to login.
    auth.clear();
    throw new ApiError(401, 'Your session has expired. Please sign in again.');
  }

  if (!res.ok) {
    throw new ApiError(res.status, await readError(res));
  }

  // 204 and empty bodies are valid successes.
  const text = await res.text();
  return (text ? JSON.parse(text) : undefined) as T;
}

async function readError(res: Response): Promise<string> {
  try {
    const problem = await res.json();
    return problem?.detail ?? problem?.title ?? `Request failed (${res.status})`;
  } catch {
    return `Request failed (${res.status})`;
  }
}
