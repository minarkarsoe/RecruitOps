import type { LoginResponse, UserRole } from '@recruitops/types';

const STORAGE_KEY = 'recruitops.session';

export interface Session {
  accessToken: string;
  expiresAtUtc: string;
  role: UserRole;
  displayName: string;
  userId: string;
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
      // Expired tokens are useless; drop them rather than letting the API 401 later.
      if (new Date(session.expiresAtUtc).getTime() <= Date.now()) {
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
    const session: Session = {
      accessToken: response.accessToken,
      expiresAtUtc: response.expiresAtUtc,
      role: response.role,
      displayName: response.displayName,
      userId: response.userId,
    };
    sessionStorage.setItem(STORAGE_KEY, JSON.stringify(session));
    return session;
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

/** Roles that see only their own departments (ADR-0003). Mirrors `RoleScope.IsDepartmentScoped`. */
export function isDepartmentScoped(role: UserRole): boolean {
  return role === 'HiringManager';
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
  return role === 'Approver';
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
  return role === 'Admin' || role === 'HrDirector' || role === 'Recruiter';
}

/** Roles that decide on requisitions, and therefore need the approver inbox. */
export function canApprove(role: UserRole): boolean {
  return role === 'Admin' || role === 'HrDirector' || role === 'Approver';
}

/** Administrators — approval chains, departments, the user directory. */
export function isAdmin(role: UserRole): boolean {
  return role === 'Admin';
}
