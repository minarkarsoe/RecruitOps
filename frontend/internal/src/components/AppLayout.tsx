import { NavLink, Outlet, useNavigate } from 'react-router-dom';
import {
  auth, canApprove, isAdmin, isExcludedFromCandidateData, isRecruitmentStaff,
} from '../lib/auth';

// Internal app shell: fixed 240px sidebar + fluid content, max-width 1280 (design system §4).
//
// The role predicates live in lib/auth.ts, not here. They used to be three local functions,
// which is how a role list ends up written twice and corrected once — the mistake ADR-0018
// exists because of. This file decides layout; it does not decide what a role is.

export function AppLayout() {
  const navigate = useNavigate();
  const session = auth.get();
  const role = session?.role;

  function signOut() {
    auth.clear();
    navigate('/login', { replace: true });
  }

  const link = ({ isActive }: { isActive: boolean }) =>
    `block rounded-md px-3 py-2 text-[15px] ${
      isActive
        ? 'bg-primary-100 font-semibold text-primary-700'
        : 'text-ink-600 hover:bg-surface-50'
    }`;

  return (
    <div className="flex min-h-screen">
      <aside className="w-60 shrink-0 border-r border-line-200 bg-surface-0 p-4">
        <div className="mb-6 px-3">
          <span className="font-display text-lg font-bold">RecruitOps</span>
        </div>

        <nav className="space-y-1">
          <NavLink to="/requisitions" className={link}>Requisitions</NavLink>

          {/* Hidden from roles with no reach into candidate data (ADR-0018): the pipeline on
              a posting is empty for them by design, and a link to a page that can only
              disappoint reads as a fault rather than a rule. They still reach a single
              interview by being on its panel — that route is not gated. */}
          {role && !isExcludedFromCandidateData(role) && (
            <NavLink to="/jobpostings" className={link}>Job postings</NavLink>
          )}

          {role && canApprove(role) && (
            <NavLink to="/inbox" className={link}>
              Inbox
            </NavLink>
          )}

          {role && isRecruitmentStaff(role) && (
            <NavLink to="/jdtemplates" className={link}>JD templates</NavLink>
          )}

          {/* Readable by anyone internal — an interviewer wants to know what they will be
              asked before the day, not on it. */}
          {role && !isExcludedFromCandidateData(role) && (
            <NavLink to="/scorecardtemplates" className={link}>Scorecard templates</NavLink>
          )}

          {role && isAdmin(role) && (
            <>
              <NavLink to="/approvalchains" className={link}>Approval chains</NavLink>
              <NavLink to="/departments" className={link}>Departments</NavLink>
            </>
          )}
        </nav>

        {session && (
          <div className="mt-8 border-t border-line-200 px-3 pt-4">
            <p className="text-[15px] font-semibold">{session.displayName}</p>
            <p className="text-[13px] text-ink-400">{session.role}</p>
            <button
              onClick={signOut}
              className="mt-2 text-[13px] font-semibold text-primary-600 hover:text-primary-700"
            >
              Sign out
            </button>
          </div>
        )}
      </aside>

      <main className="mx-auto w-full max-w-[1280px] p-6">
        <Outlet />
      </main>
    </div>
  );
}
