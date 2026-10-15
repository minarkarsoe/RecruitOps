import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { LoginResponse } from '@recruitops/types';
import { apiFetch, revokeSession, ApiError } from './api';
import { auth } from './auth';

// ADR-0028: what the SPA does after a 401. Found by the security review of the refresh-token
// hardening: requests queued behind one that already refreshed sent the *rotated* token back —
// which the server reads as a stolen copy and answers by ending every session — and any failed
// refresh, a 429 included, signed the user out.

function login(over: Partial<LoginResponse> = {}): LoginResponse {
  return {
    accessToken: 'access-1',
    expiresAtUtc: '2099-01-01T00:00:00Z',
    refreshToken: 'refresh-1',
    refreshTokenExpiresAtUtc: '2099-01-01T00:00:00Z',
    role: 'Recruiter',
    displayName: 'Tester',
    userId: 'usr-1',
    permissions: [],
    ...over,
  } as LoginResponse;
}

function json(status: number, body?: unknown, headers: Record<string, string> = {}): Response {
  return new Response(body === undefined ? null : JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json', ...headers },
  });
}

const fetchMock = vi.fn<typeof fetch>();

function calls(): { url: string; auth?: string; body?: string }[] {
  return fetchMock.mock.calls.map(([url, init]) => ({
    url: String(url),
    auth: (init?.headers as Record<string, string> | undefined)?.Authorization,
    body: init?.body as string | undefined,
  }));
}

describe('apiFetch after a 401', () => {
  beforeEach(() => {
    sessionStorage.clear();
    fetchMock.mockReset();
    vi.stubGlobal('fetch', fetchMock);
    auth.set(login());
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('refreshes once and retries with the new access token', async () => {
    fetchMock
      .mockResolvedValueOnce(json(401))
      .mockResolvedValueOnce(json(200, login({ accessToken: 'access-2', refreshToken: 'refresh-2' })))
      .mockResolvedValueOnce(json(200, { ok: true }));

    await expect(apiFetch('/requisitions')).resolves.toEqual({ ok: true });

    expect(calls()[1]).toMatchObject({ url: '/api/auth/refresh', body: JSON.stringify({ refreshToken: 'refresh-1' }) });
    expect(calls()[2]).toMatchObject({ url: '/api/requisitions', auth: 'Bearer access-2' });
    expect(auth.get()?.refreshToken).toBe('refresh-2');
  });

  it('never sends back a refresh token another request already rotated', async () => {
    // The request went out with refresh-1; while it was in flight another request refreshed.
    fetchMock
      .mockImplementationOnce(async () => {
        auth.set(login({ accessToken: 'access-2', refreshToken: 'refresh-2' }));
        return json(401);
      })
      .mockResolvedValueOnce(json(200, { ok: true }));

    await expect(apiFetch('/requisitions')).resolves.toEqual({ ok: true });

    expect(calls().some((c) => c.url === '/api/auth/refresh')).toBe(false);
    expect(calls()[1]).toMatchObject({ url: '/api/requisitions', auth: 'Bearer access-2' });
  });

  it('keeps the session when the refresh is rate-limited', async () => {
    fetchMock.mockResolvedValueOnce(json(401)).mockResolvedValueOnce(json(429, undefined, { 'Retry-After': '30' }));

    const error = await apiFetch('/requisitions').catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).status).toBe(429);
    expect(auth.get()?.refreshToken).toBe('refresh-1');
  });

  it('keeps the session when the server cannot be reached', async () => {
    fetchMock.mockResolvedValueOnce(json(401)).mockRejectedValueOnce(new TypeError('Failed to fetch'));

    await expect(apiFetch('/requisitions')).rejects.toBeInstanceOf(ApiError);
    expect(auth.get()).not.toBeNull();
  });

  it('signs out when the server refuses the refresh token', async () => {
    fetchMock.mockResolvedValueOnce(json(401)).mockResolvedValueOnce(json(401));

    const error = await apiFetch('/requisitions').catch((e: unknown) => e);

    expect((error as ApiError).status).toBe(401);
    expect(auth.get()).toBeNull();
  });
});

describe('revokeSession', () => {
  beforeEach(() => {
    sessionStorage.clear();
    fetchMock.mockReset();
    vi.stubGlobal('fetch', fetchMock);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('ends the session on the server with the stored refresh token', () => {
    auth.set(login());
    fetchMock.mockResolvedValueOnce(json(204));

    revokeSession();

    expect(calls()[0]).toMatchObject({ url: '/api/auth/revoke', body: JSON.stringify({ refreshToken: 'refresh-1' }) });
  });

  it('does nothing without a session, and never throws', () => {
    revokeSession();
    expect(fetchMock).not.toHaveBeenCalled();

    auth.set(login());
    fetchMock.mockRejectedValueOnce(new TypeError('offline'));
    expect(() => revokeSession()).not.toThrow();
  });
});
