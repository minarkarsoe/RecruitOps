import { describe, expect, it } from 'vitest';
import {
  hasCompanyWideReach, hasPermission, homePath, isDepartmentScoped, isExcludedFromCandidateData, isInternalUser,
  type Session,
} from './auth';

/**
 * Regression cover for the fail-open `hasPermission()`.
 *
 * The original bug was not that some edge case returned the wrong answer — it was that the
 * *only* branch non-admin users ever reached returned `true`. The API's `LoginResponse` had
 * no `permissions` field at all, so `session.permissions` was always `undefined`, so the
 * "legacy fallback" granted every permission to everyone.
 *
 * The existing component tests all passed an explicit `permissions` array, so they exercised
 * a session shape the API never produced and the hole stayed invisible. These tests cover the
 * shapes that actually occur: absent, empty, and populated.
 */

const session = (over: Partial<Session> = {}): Session => ({
  accessToken: 'token',
  expiresAtUtc: '2099-01-01T00:00:00Z',
  role: 'Recruiter',
  displayName: 'Test User',
  userId: 'usr-1',
  permissions: [],
  ...over,
});

const READ = 'permission:users:users:read';

describe('hasPermission — fails closed', () => {
  it('denies when there is no session at all', () => {
    expect(hasPermission(null, READ)).toBe(false);
  });

  it('denies when permissions is absent (pre-RBAC session in sessionStorage)', () => {
    // The exact production shape before this fix: a valid session, no permissions field.
    const stale = session();
    delete (stale as { permissions?: string[] }).permissions;
    expect(stale.permissions).toBeUndefined();
    expect(hasPermission(stale, READ)).toBe(false);
  });

  it('denies when permissions is empty — "nothing granted" is a real answer, not "unknown"', () => {
    expect(hasPermission(session({ permissions: [] }), READ)).toBe(false);
  });

  it('denies a permission the user was not granted', () => {
    expect(
      hasPermission(session({ permissions: ['permission:requisitions:requisitions:read'] }), READ)
    ).toBe(false);
  });

  it('grants a permission the user was granted', () => {
    expect(hasPermission(session({ permissions: [READ] }), READ)).toBe(true);
  });
});

describe('hasPermission — admin bypass mirrors PermissionAuthorizationHandler', () => {
  it('grants Admin regardless of the permissions array', () => {
    expect(hasPermission(session({ role: 'Admin', permissions: [] }), READ)).toBe(true);
  });

  it('grants SuperAdmin by role', () => {
    expect(hasPermission(session({ role: 'SuperAdmin', permissions: [] }), READ)).toBe(true);
  });

  it('grants SuperAdmin by the isSuperAdmin flag on a non-SuperAdmin role', () => {
    expect(
      hasPermission(session({ role: 'Recruiter', isSuperAdmin: true, permissions: [] }), READ)
    ).toBe(true);
  });

  it('does not grant a non-admin role with the flag unset', () => {
    expect(
      hasPermission(session({ role: 'HrDirector', isSuperAdmin: false, permissions: [] }), READ)
    ).toBe(false);
  });
});

/**
 * Mirrors of `RoleScope` on the server. The server added Interviewer to both on 2026-10-15; these
 * copies did not follow until 2026-10-17, which is exactly the two-copies bug `RoleScope` exists to
 * prevent on the server side. Pinned role by role so the next divergence is a failing test.
 */
describe('role scope mirrors RoleScope', () => {
  it.each([
    // role, department-scoped, excluded from candidates, CompanyWide, InternalUser
    ['SuperAdmin', false, false, true, true],
    ['Admin', false, false, true, true],
    ['HrDirector', false, false, true, true],
    ['Recruiter', false, false, true, true],
    ['HiringManager', true, false, false, true],
    ['Approver', false, true, false, true],
    ['Interviewer', true, true, false, false],
  ] as const)('%s: scoped %s, excluded %s, company-wide %s, internal %s', (role, scoped, excluded, wide, internal) => {
    expect(isDepartmentScoped(role)).toBe(scoped);
    expect(isExcludedFromCandidateData(role)).toBe(excluded);
    expect(hasCompanyWideReach(role)).toBe(wide);
    expect(isInternalUser(role)).toBe(internal);
  });

  it('an unknown role is neither company-wide nor internal', () => {
    expect(isInternalUser('Contractor')).toBe(false);
  });
});

describe('homePath — where a signed-in user lands', () => {
  it('is Requisitions for anyone who can read them', () => {
    expect(homePath(session({ permissions: ['permission:requisitions:requisitions:read'] }))).toBe('/requisitions');
    expect(homePath(session({ role: 'Admin', permissions: [] }))).toBe('/requisitions');
  });

  it('is Interviews for the Interviewer role, whose only screen it is', () => {
    expect(homePath(session({ role: 'Interviewer', permissions: ['permission:interviews:interviews:read'] })))
      .toBe('/interviews');
  });
});
