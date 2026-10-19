import type { LoginResponse, UserRole } from '@recruitops/types';

const STORAGE_KEY = 'recruitops.session';

export interface Session {
  accessToken: string;
  expiresAtUtc: string;
  refreshToken?: string;
  refreshTokenExpiresAtUtc?: string;
  role: UserRole;
  displayName: string;
  userId: string;
  isSuperAdmin?: boolean;
  activeTenantId?: string;
  activeTenantName?: string;
  /**
   * Optional here — unlike on the API's `LoginResponse`, where it is required — because a
   * session persisted in `sessionStorage` before the API shipped the field will not have it.
   * `hasPermission()` treats absent as "no permissions", never as "allow".
   */
  permissions?: string[];
}

/**
 * Session storage for the internal SPA.
 *
 * ⚠️ SECURITY TRADE-OFF — revisit before an enterprise/bank deployment.
 * The token is held in `sessionStorage`, which means any XSS on this origin can read
 * it. The stronger option is an httpOnly, SameSite cookie issued by the API, which
 * JavaScript cannot read at all — but that requires backend changes (cookie issuance
 * + CSRF protection) that ADR-0002 has not made yet.
 *
 * `sessionStorage` (not `localStorage`) is a deliberate middle ground: the session
 * dies with the tab instead of persisting indefinitely on a shared machine — which
 * matters for recruiters using hot-desked or shared workstations.
 */
export const auth = {
  get(): Session | null {
    const raw = sessionStorage.getItem(STORAGE_KEY);
    if (!raw) return null;
    try {
      const session = JSON.parse(raw) as Session;
      // If access token is expired AND there is no refresh token, drop session.
      // Silent refresh in apiFetch will handle renewing the access token if refresh token is present.
      if (!session.refreshToken && new Date(session.expiresAtUtc).getTime() <= Date.now()) {
        sessionStorage.removeItem(STORAGE_KEY);
        return null;
      }
      return session;
    } catch {
      sessionStorage.removeItem(STORAGE_KEY);
      return null;
    }
  },

  set(response: LoginResponse): Session {
    const current = auth.get();
    const isSuper = response.isSuperAdmin || response.role === 'SuperAdmin';

    // ⚠️ Only a super-admin carries a tenant override, and this is why.
    //
    // `activeTenantId` becomes the `X-Tenant-Id` header, which the API honours **only** for
    // super-admins (see CurrentTenant). Setting it for everyone — which is what this did until
    // 2026-08-26 — meant every signed-in user's every request carried a header naming a tenant.
    // It was harmless while nothing read the header and stayed harmless once something did,
    // because the server gates on the token rather than on the header. But "every user sends a
    // tenant id the server is trusted to ignore" is one careless commit away from a breach, and
    // there is no reason for an ordinary user to send it at all: their tenant is in their token.
    //
    // The override survives a token refresh (same user only), so a super-admin looking at another
    // company does not get silently bounced home when their access token rotates.
    const keptOverride =
      isSuper && current?.userId === response.userId ? current.activeTenantId : undefined;

    const session: Session = {
      accessToken: response.accessToken,
      expiresAtUtc: response.expiresAtUtc,
      refreshToken: response.refreshToken,
      refreshTokenExpiresAtUtc: response.refreshTokenExpiresAtUtc,
      role: response.role,
      displayName: response.displayName,
      userId: response.userId,
      isSuperAdmin: isSuper,
      activeTenantId: isSuper
        ? keptOverride ?? response.activeTenantId ?? response.tenantId
        : undefined,
      activeTenantName: isSuper
        ? (keptOverride ? current?.activeTenantName : response.activeTenantName)
        : undefined,
      permissions: response.permissions,
    };
    sessionStorage.setItem(STORAGE_KEY, JSON.stringify(session));
    return session;
  },

  setActiveTenant(tenantId: string, tenantName: string): void {
    const current = auth.get();
    if (!current) return;
    const updated: Session = {
      ...current,
      activeTenantId: tenantId,
      activeTenantName: tenantName,
    };
    sessionStorage.setItem(STORAGE_KEY, JSON.stringify(updated));
  },

  clear(): void {
    sessionStorage.removeItem(STORAGE_KEY);
  },
};

/*
 * ---------------------------------------------------------------------------
 * Role predicates — the client-side mirror of `Domain/RoleScope.cs`.
 *
 * These decide what to *render*, never what is *allowed*: the API re-answers every one of
 * them, and a hidden button is not access control. They live together, and are the only
 * place in this app a role name is written, for the reason ADR-0018 exists — the one method
 * that spelled a role out by hand was the one that shipped a hole, and it did so while
 * carrying a comment describing the case it let through. If you find yourself typing a role
 * literal in a component, add a predicate here instead and grep for its siblings.
 * ---------------------------------------------------------------------------
 */

/**
 * Roles that see only their own departments (ADR-0003). Mirrors `RoleScope.IsDepartmentScoped`,
 * which added Interviewer on 2026-10-15 — this copy did not follow until 2026-10-17.
 */
export function isDepartmentScoped(role: UserRole): boolean {
  return role === 'HiringManager' || role === 'Interviewer';
}

/**
 * Roles with no standing reach into candidate data — applications, pipeline, interviews,
 * scorecards, notes (ADR-0018). Mirrors `RoleScope.IsExcludedFromCandidateData`.
 *
 * A separate axis from `isDepartmentScoped` on purpose: an Approver is company-wide for
 * requisitions and reaches no candidate at all, so neither predicate alone describes them.
 * They still reach a single application by sitting on its interview panel (ADR-0017 §4) —
 * so this must not be used to hide interview screens outright, only the pipeline entry
 * points that would otherwise 404 for them.
 */
export function isExcludedFromCandidateData(role: UserRole): boolean {
  return role === 'Approver' || role === 'Interviewer';
}

/**
 * The `CompanyWide` policy: reach over every department on both axes. Department and
 * approval-chain administration need it on top of their permission, because both move a scoped
 * role's own reach — so Hiring Manager, Interviewer and Approver bases never get those screens,
 * whatever they hold. Built from the two predicates above, as the server's is from `RoleScope`.
 */
export function hasCompanyWideReach(role: UserRole): boolean {
  return !isDepartmentScoped(role) && !isExcludedFromCandidateData(role);
}

/**
 * The `InternalUser` policy — everyone but Interviewer. Most of the API (search, pipeline,
 * postings, analytics) sits behind it; an Interviewer reaches only its panels (`PanelAccess`).
 * Listed rather than negated so an unknown role fails closed.
 */
export function isInternalUser(role: UserRole): boolean {
  return ['SuperAdmin', 'Admin', 'HrDirector', 'Recruiter', 'HiringManager', 'Approver'].includes(role);
}

/**
 * The `RecruitmentStaff` policy: who owns the hiring process. Scheduling, rescheduling,
 * changing a panel, cancelling, completing, moving a stage and editing scorecard templates
 * are all gated on this server-side.
 *
 * Excludes HiringManager (department-scoped) and Approver (no candidate reach) — two
 * different reasons for the same exclusion, which is why this is not `!isDepartmentScoped`.
 */
export function isRecruitmentStaff(role: UserRole): boolean {
  return role === 'Admin' || role === 'HrDirector' || role === 'Recruiter' || role === 'SuperAdmin';
}

/** Roles that decide on requisitions, and therefore need the approver inbox. */
export function canApprove(role: UserRole): boolean {
  return role === 'Admin' || role === 'HrDirector' || role === 'Approver' || role === 'SuperAdmin';
}

/** Administrators — approval chains, departments, the user directory. */
export function isAdmin(role: UserRole): boolean {
  return role === 'Admin' || role === 'SuperAdmin';
}

export function isSuperAdmin(session: Session | null): boolean {
  if (!session) return false;
  return !!session.isSuperAdmin || session.role === 'SuperAdmin';
}

/**
 * Whether to *render* a permission-gated control. Fails closed on every uncertain input.
 *
 * ⚠️ This function used to return `true` for a null session and for a session whose
 * `permissions` array was absent — the latter commented as a "legacy" fallback. It was not
 * legacy: the login endpoint never sent `permissions` at all, so that branch was the only
 * one non-admin users ever reached, and every gated button, sidebar link and route guard in
 * the app silently rendered for everyone. The tests missed it because they all constructed
 * sessions with an explicit `permissions` array, a shape the API never actually returned.
 *
 * The rule now: unknown means no. An empty array is a real answer ("this user has nothing
 * granted"), and absence — an old session in `sessionStorage` from before the API shipped
 * the field — is treated the same way rather than being read as permission to proceed.
 *
 * Admin and SuperAdmin still bypass, mirroring `PermissionAuthorizationHandler` server-side.
 *
 * As with the role predicates above: this decides what to render, never what is allowed.
 * The API re-answers every check.
 */
export function hasPermission(session: Session | null, permissionCode: string): boolean {
  if (!session) return false;
  if (isSuperAdmin(session)) return true;
  if (session.role === 'Admin') return true;
  return session.permissions?.includes(permissionCode) ?? false;
}

/**
 * Where a signed-in user lands: Requisitions for everyone who can read them, Interviews
 * otherwise — the Interviewer role's one screen, and the only one a deactivated custom role's
 * holders reach. Until 2026-10-19 every sign-in landed on Requisitions, so an Interviewer's first
 * sight of the product was an error.
 */
export function homePath(session: Session | null): string {
  return hasPermission(session, 'permission:requisitions:requisitions:read') ? '/requisitions' : '/interviews';
}


