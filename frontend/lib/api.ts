const BASE = process.env.NEXT_PUBLIC_API_BASE_URL ?? '/api';

// Thin fetch wrapper. TODO: error handling, auth headers, typed responses.
export async function api<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`${BASE}${path}`, {
    headers: { 'Content-Type': 'application/json', ...(init?.headers ?? {}) },
    cache: 'no-store',
    ...init,
  });
  if (!res.ok) throw new Error(`API ${res.status}: ${res.statusText}`);
  return res.json() as Promise<T>;
}
