import { NavLink, Outlet, useNavigate } from 'react-router-dom';
import {
  auth, hasPermission, isSuperAdmin,
} from '../lib/auth';
import { TenantSwitcherBar } from './TenantSwitcherBar';

// Internal app shell: fixed 240px sidebar + fluid content, max-width 1280 (design system §4).

export function AppLayout() {
  const navigate = useNavigate();
  const session = auth.get();

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
    <div className="min-h-screen flex flex-col">
      <TenantSwitcherBar />
      <div className="flex flex-1">
        <aside className="w-60 shrink-0 border-r border-line-200 bg-surface-0 p-4">
          <div className="mb-6 px-3">
            <span className="font-display text-lg font-bold">RecruitOps</span>
          </div>

          <nav className="space-y-1">
            {hasPermission(session, 'permission:requisitions:requisitions:read') && (
              <NavLink to="/requisitions" className={link}>Requisitions</NavLink>
            )}

            {hasPermission(session, 'permission:postings:postings:read') && (
              <NavLink to="/jobpostings" className={link}>Job postings</NavLink>
            )}

            {hasPermission(session, 'permission:requisitions:requisitions:approve') && (
              <NavLink to="/inbox" className={link}>
                Inbox
              </NavLink>
            )}

            {hasPermission(session, 'permission:requisitions:requisitions:read') && (
              <NavLink to="/jdtemplates" className={link}>JD templates</NavLink>
            )}

            {hasPermission(session, 'permission:scorecards:scorecards:manage_templates') && (
              <NavLink to="/scorecardtemplates" className={link}>Scorecard templates</NavLink>
            )}

            {hasPermission(session, 'permission:settings:settings:read') && (
              <>
                <NavLink to="/approvalchains" className={link}>Approval chains</NavLink>
                <NavLink to="/departments" className={link}>Departments</NavLink>
              </>
            )}

            {hasPermission(session, 'permission:users:users:read') && (
              <NavLink to="/users" className={link}>Users</NavLink>
            )}

            {hasPermission(session, 'permission:roles:roles:read') && (
              <NavLink to="/roles" className={link}>Role Builder</NavLink>
            )}
          </nav>

          {session && (
            <div className="mt-8 border-t border-line-200 px-3 pt-4">
              <div className="flex items-center gap-1.5">
                <p className="text-[15px] font-semibold">{session.displayName}</p>
                {isSuperAdmin(session) && (
                  <span className="text-[10px] font-bold px-1.5 py-0.2 rounded bg-amber-100 text-amber-900 border border-amber-300">
                    👑 Super
                  </span>
                )}
              </div>
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
    </div>
  );
}

