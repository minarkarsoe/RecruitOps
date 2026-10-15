import { beforeEach, describe, expect, it } from 'vitest';
import type { LoginResponse } from '@recruitops/types';
import { auth, hasPermission, type Session } from './auth';

const login = (overrides: Partial<LoginResponse> = {}): LoginResponse => ({
  accessToken: 'token',
  expiresAtUtc: '2099-01-01T00:00:00Z',
  role: 'HiringManager',
  displayName: 'Someone',
  userId: 'usr-1',
  isSuperAdmin: false,
  permissions: ['permission:requisitions:requisitions:read'],
  ...overrides,
});

/**
 * `hasPermission` decides what to render. Until 2026-10-15 it returned true with no session, for
 * every Admin, and whenever the permission list was missing — which, since the API never sent
 * one, was always. These pin the fail-closed behaviour that replaced it.
 */
describe('hasPermission', () => {
  it('grants what the session lists, and nothing else', () => {
    const session = auth.set(login());
    expect(hasPermission(session, 'permission:requisitions:requisitions:read')).toBe(true);
    expect(hasPermission(session, 'permission:users:users:read')).toBe(false);
  });

  it('grants nothing without a session', () => {
    expect(hasPermission(null, 'permission:requisitions:requisitions:read')).toBe(false);
  });

  it('grants nothing when the list is missing', () => {
    const legacy = { ...auth.set(login()), permissions: undefined } as unknown as Session;
    expect(hasPermission(legacy, 'permission:requisitions:requisitions:read')).toBe(false);
  });

  it('gives an Admin only what their list says — the API has no Admin bypass', () => {
    const session = auth.set(login({ role: 'Admin', permissions: ['permission:users:users:read'] }));
    expect(hasPermission(session, 'permission:users:users:read')).toBe(true);
    expect(hasPermission(session, 'permission:system:system:manage')).toBe(false);
  });

  it('lets a SuperAdmin through, mirroring the API bypass', () => {
    const session = auth.set(login({ role: 'SuperAdmin', isSuperAdmin: true, permissions: [] }));
    expect(hasPermission(session, 'permission:system:system:manage')).toBe(true);
  });
});

describe('auth session storage', () => {
  beforeEach(() => sessionStorage.clear());

  it('stores an empty list when the API sends none, rather than nothing', () => {
    const session = auth.set({ ...login(), permissions: undefined } as unknown as LoginResponse);
    expect(session.permissions).toEqual([]);
  });

  it('drops a stored session that predates the permission list, forcing a fresh sign-in', () => {
    const { permissions: _dropped, ...legacy } = auth.set(login());
    sessionStorage.setItem('recruitops.session', JSON.stringify(legacy));
    expect(auth.get()).toBeNull();
    expect(sessionStorage.getItem('recruitops.session')).toBeNull();
  });
});
