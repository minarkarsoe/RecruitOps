import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { api } from './api';

/**
 * `api()` picks a different base URL depending on whether it is running in Node (a Server
 * Component) or in the browser, and **that choice is the one thing here that has actually
 * broken in production**.
 *
 * On 2026-08-27 the app shipped with `next.config.mjs` rewriting `/api/*` to
 * `http://localhost:5080/api/*`. Inside a container `localhost` is the container itself, where
 * nothing listens on 5080, so every browser-side call 500'd with `ECONNREFUSED 127.0.0.1:5080`
 * — including submitting an application, which is this app's entire purpose. It went unnoticed
 * because SSR calls `API_INTERNAL_URL` directly and never touches the rewrite, so the job page
 * rendered perfectly while the form behind it could not reach anything.
 *
 * These tests pin the branch, not the rewrite (which lives in `next.config.mjs` and is frozen
 * into `.next/routes-manifest.json` at build time). What they guarantee is that the two paths
 * stay genuinely different, so a change that collapses them fails here rather than in Docker.
 */

const realFetch = globalThis.fetch;

// The mock is given fetch's real parameter list so `mock.calls[0][1]` is typed as
// `RequestInit` — `tsc --noEmit` runs over these tests with the app's own config, and an
// untyped mock would make the assertions below unverifiable at the type level.
function mockFetch(body: unknown, init?: { ok?: boolean; status?: number; statusText?: string }) {
  const fn = vi.fn(async (_input: RequestInfo | URL, _init?: RequestInit) => ({
    ok: init?.ok ?? true,
    status: init?.status ?? 200,
    statusText: init?.statusText ?? 'OK',
    json: async () => body,
  }));
  globalThis.fetch = fn as unknown as typeof fetch;
  return fn;
}

/** The url `fetch` was actually called with. */
function calledUrl(fn: ReturnType<typeof mockFetch>): string {
  return String(fn.mock.calls[0][0]);
}

/** The `RequestInit` `fetch` was actually called with. */
function calledInit(fn: ReturnType<typeof mockFetch>): RequestInit {
  return fn.mock.calls[0][1] ?? {};
}

beforeEach(() => {
  delete process.env.API_INTERNAL_URL;
  delete process.env.NEXT_PUBLIC_API_BASE_URL;
});

afterEach(() => {
  globalThis.fetch = realFetch;
  vi.unstubAllGlobals();
});

describe('api() base URL — the browser half', () => {
  it('uses a RELATIVE path so the call is same-origin and hits the rewrite', async () => {
    const fn = mockFetch({});
    await api('/public/jobs/abc');

    // Relative, so the browser resolves it against the page origin. An absolute URL here is
    // the bug: it would point at whatever host the build happened to know about.
    expect(calledUrl(fn)).toBe('/api/public/jobs/abc');
    expect(calledUrl(fn).startsWith('http')).toBe(false);
  });

  it('honours NEXT_PUBLIC_API_BASE_URL when the deployment sets one', async () => {
    process.env.NEXT_PUBLIC_API_BASE_URL = '/gateway';
    const fn = mockFetch({});
    await api('/public/jobs/abc');

    expect(calledUrl(fn)).toBe('/gateway/public/jobs/abc');
  });
});

describe('api() base URL — the server half', () => {
  it('uses an ABSOLUTE url, because a Server Component has no page origin to resolve against', async () => {
    // `typeof window === 'undefined'` is the branch under test. Node really has no `window`;
    // jsdom gives us one, so it is stubbed away for the duration of this test.
    vi.stubGlobal('window', undefined);
    process.env.API_INTERNAL_URL = 'http://backend:8080/api';
    const fn = mockFetch({});

    await api('/public/jobs/abc');

    expect(calledUrl(fn)).toBe('http://backend:8080/api/public/jobs/abc');
  });

  it('falls back to localhost:5080 — correct for `npm run dev:public`, WRONG inside a container', async () => {
    vi.stubGlobal('window', undefined);
    const fn = mockFetch({});

    await api('/public/jobs/abc');

    // This fallback is deliberate and is fine on a developer's machine. In Docker,
    // `API_INTERNAL_URL` MUST be set — as a build arg as well as a runtime variable, because
    // Next freezes `rewrites()` into the routes manifest during `next build`.
    expect(calledUrl(fn)).toBe('http://localhost:5080/api/public/jobs/abc');
  });

  it('server and browser resolve the SAME path to different URLs — they must not collapse', async () => {
    process.env.API_INTERNAL_URL = 'http://backend:8080/api';

    vi.stubGlobal('window', undefined);
    const server = mockFetch({});
    await api('/public/jobs/abc');
    const serverUrl = calledUrl(server);

    vi.unstubAllGlobals();
    const browser = mockFetch({});
    await api('/public/jobs/abc');
    const browserUrl = calledUrl(browser);

    expect(serverUrl).not.toBe(browserUrl);
    expect(serverUrl).toBe('http://backend:8080/api/public/jobs/abc');
    expect(browserUrl).toBe('/api/public/jobs/abc');
  });
});

describe('api() request shape', () => {
  it('sends JSON and never serves a cached response', async () => {
    const fn = mockFetch({});
    await api('/public/jobs/abc');

    const init = calledInit(fn);
    expect((init.headers as Record<string, string>)['Content-Type']).toBe('application/json');
    // A job page cached by the fetch layer would show a closed vacancy as open.
    expect(init.cache).toBe('no-store');
  });

  it('lets the caller add headers without dropping Content-Type', async () => {
    const fn = mockFetch({});
    await api('/x', { headers: { 'X-Trace': '1' } });

    const headers = calledInit(fn).headers as Record<string, string>;
    expect(headers['Content-Type']).toBe('application/json');
    expect(headers['X-Trace']).toBe('1');
  });

  it('throws on a non-OK response, carrying the status', async () => {
    mockFetch(null, { ok: false, status: 404, statusText: 'Not Found' });

    await expect(api('/public/jobs/gone')).rejects.toThrow('API 404: Not Found');
  });

  it('resolves the parsed body on success', async () => {
    mockFetch({ title: 'Sales Executive' });

    await expect(api<{ title: string }>('/public/jobs/abc')).resolves.toEqual({
      title: 'Sales Executive',
    });
  });
});
