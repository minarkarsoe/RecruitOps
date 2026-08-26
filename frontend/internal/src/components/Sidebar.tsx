import { NavLink, useNavigate } from 'react-router-dom';
import { auth, hasPermission, isSuperAdmin, Session } from '../lib/auth';
import { useFeatureFlags } from '../lib/useFeatureFlags';

// Built against the nav rail in `design/internal/board.html` (ADR-0025), which every internal
// screen in the kit shares.
//
// It is a DARK rail on purpose, and that is the one decision here worth defending: it is the
// second neutral layer the design calls for, so the content surface reads as the workspace and
// navigation recedes behind it. A white sidebar next to a white content pane makes the two
// compete, and on a screen that is mostly table it is the table that should win.

interface SidebarProps {
  session?: Session | null;
  onSignOut?: () => void;
}

interface NavItem {
  to: string;
  label: string;
  permission: string;
  featureFlag?: string;
  icon: JSX.Element;
}

interface NavGroup {
  title: string;
  items: NavItem[];
}

/** 16px stroke icons, matching the kit. Inline rather than a library: eleven paths is less
 *  weight than an icon package, and it keeps the rail's markup readable next to the design. */
const icons = {
  requisitions: (
    <path d="M4 2.5h8a1 1 0 011 1v9a1 1 0 01-1 1H4a1 1 0 01-1-1v-9a1 1 0 011-1zM5.5 6h5M5.5 9h3"
      stroke="currentColor" strokeWidth="1.4" strokeLinecap="round" />
  ),
  postings: (
    <path d="M8 2.5l5 2.5v6l-5 2.5-5-2.5v-6l5-2.5z"
      stroke="currentColor" strokeWidth="1.4" strokeLinejoin="round" />
  ),
  inbox: (
    <path d="M2 8h3l1 2h4l1-2h3M2.5 4.5h11a1 1 0 011 1v6a1 1 0 01-1 1h-11a1 1 0 01-1-1v-6a1 1 0 011-1z"
      stroke="currentColor" strokeWidth="1.4" strokeLinecap="round" strokeLinejoin="round" />
  ),
  templates: (
    <>
      <rect x="2.5" y="3" width="11" height="10" rx="1.5" stroke="currentColor" strokeWidth="1.4" />
      <path d="M5.5 6.5h5M5.5 9.5h3" stroke="currentColor" strokeWidth="1.4" strokeLinecap="round" />
    </>
  ),
  analytics: (
    <>
      <rect x="2" y="2.5" width="3.5" height="11" rx="1" stroke="currentColor" strokeWidth="1.4" />
      <rect x="6.5" y="2.5" width="3.5" height="7.5" rx="1" stroke="currentColor" strokeWidth="1.4" />
      <rect x="11" y="2.5" width="3" height="9.5" rx="1" stroke="currentColor" strokeWidth="1.4" />
    </>
  ),
  users: (
    <>
      <circle cx="6" cy="6" r="2.2" stroke="currentColor" strokeWidth="1.4" />
      <path d="M2.5 13c0-1.9 1.6-3 3.5-3s3.5 1.1 3.5 3M11 5.5h3M11 8h3"
        stroke="currentColor" strokeWidth="1.4" strokeLinecap="round" />
    </>
  ),
  departments: (
    <>
      <rect x="2.5" y="6" width="4" height="7.5" rx="1" stroke="currentColor" strokeWidth="1.4" />
      <rect x="9.5" y="2.5" width="4" height="11" rx="1" stroke="currentColor" strokeWidth="1.4" />
    </>
  ),
  chains: (
    <path d="M2.5 4h11M2.5 8h11M2.5 12h7" stroke="currentColor" strokeWidth="1.4" strokeLinecap="round" />
  ),
  roles: (
    <>
      <circle cx="8" cy="5.5" r="2.4" stroke="currentColor" strokeWidth="1.4" />
      <path d="M3.5 13.5c0-2.3 2-3.8 4.5-3.8s4.5 1.5 4.5 3.8"
        stroke="currentColor" strokeWidth="1.4" strokeLinecap="round" />
    </>
  ),
  delivery: (
    <path d="M2.5 4.5h11a1 1 0 011 1v5a1 1 0 01-1 1h-11a1 1 0 01-1-1v-5a1 1 0 011-1zM2.8 5l5.2 3.6L13.2 5"
      stroke="currentColor" strokeWidth="1.4" strokeLinecap="round" strokeLinejoin="round" />
  ),
};

function Icon({ children, active }: { children: JSX.Element; active: boolean }) {
  return (
    <svg
      className={`h-4 w-4 shrink-0 ${active ? 'text-brand-200' : ''}`}
      viewBox="0 0 16 16"
      fill="none"
      aria-hidden="true"
    >
      {children}
    </svg>
  );
}

export function Sidebar({ session: propSession, onSignOut }: SidebarProps) {
  const navigate = useNavigate();
  const session = propSession !== undefined ? propSession : auth.get();
  const { isFeatureEnabled } = useFeatureFlags();

  function handleSignOut() {
    if (onSignOut) {
      onSignOut();
    } else {
      auth.clear();
      navigate('/login', { replace: true });
    }
  }

  // Group names and membership are UNCHANGED. The kit's rail shows "Work" and "Configure",
  // but which items live under which heading is product information architecture, not a
  // design-system decision — renaming the whole nav is a separate call for the product owner,
  // and it would break tests for a reason that has nothing to do with tokens. What is adopted
  // from the kit is how the rail LOOKS, and the icons.
  const navGroups: NavGroup[] = [
    {
      title: 'Recruitment',
      items: [
        { to: '/requisitions', label: 'Requisitions', permission: 'permission:requisitions:requisitions:read', icon: icons.requisitions },
        { to: '/jobpostings', label: 'Job postings', permission: 'permission:postings:postings:read', icon: icons.postings },
        { to: '/inbox', label: 'Inbox', permission: 'permission:requisitions:requisitions:approve', icon: icons.inbox },
        { to: '/jdtemplates', label: 'JD templates', permission: 'permission:requisitions:requisitions:read', icon: icons.templates },
        { to: '/scorecardtemplates', label: 'Scorecard templates', permission: 'permission:scorecards:scorecards:manage_templates', icon: icons.templates },
        // ADR-0026's delivery log. Gated on `applications:read` rather than on a permission of
        // its own, and the mapping is the point: the log is a list of what we told applicants
        // about their applications, and the set of people holding that permission — Admin,
        // HR Director, Recruiter, Hiring Manager — is exactly the set the endpoint serves.
        // Notably it excludes Interviewer and Approver, both of whom the API also refuses, so
        // the rail never shows a link that 403s.
        { to: '/delivery', label: 'Delivery log', permission: 'permission:applications:applications:read', icon: icons.delivery },
      ],
    },
    {
      title: 'Insights',
      items: [
        { to: '/analytics', label: 'Analytics', permission: 'permission:requisitions:requisitions:read', featureFlag: 'EnableAnalytics', icon: icons.analytics },
      ],
    },
    {
      title: 'Team',
      items: [
        { to: '/users', label: 'Users', permission: 'permission:users:users:read', icon: icons.users },
        { to: '/departments', label: 'Departments', permission: 'permission:settings:settings:read', icon: icons.departments },
      ],
    },
    {
      title: 'Governance',
      items: [
        { to: '/approvalchains', label: 'Approval chains', permission: 'permission:settings:settings:read', icon: icons.chains },
        { to: '/roles', label: 'Role Builder', permission: 'permission:roles:roles:read', icon: icons.roles },
      ],
    },
  ];

  const linkClass = ({ isActive }: { isActive: boolean }) =>
    `mx-2 flex h-9 items-center gap-2.5 rounded-md px-2.5 text-base transition-colors ${
      isActive
        ? 'bg-white/10 font-medium text-white'
        : 'text-white/70 hover:bg-white/5 hover:text-white'
    }`;

  const initials = (session?.displayName ?? '')
    .split(/\s+/)
    .filter(Boolean)
    .slice(-2)
    .map((part) => part[0]?.toUpperCase() ?? '')
    .join('') || 'U';

  return (
    <aside className="flex w-[224px] shrink-0 flex-col bg-ink-900 text-white">
      <div className="flex h-14 items-center gap-2.5 border-b border-white/10 px-4">
        <svg width="22" height="22" viewBox="0 0 26 26" fill="none" aria-hidden="true">
          <rect x="1" y="1" width="24" height="24" rx="7" fill="#0F766E" />
          <circle cx="9" cy="7.5" r="2.1" fill="#fff" />
          <circle cx="9" cy="13" r="2.1" fill="#fff" />
          <circle cx="9" cy="18.5" r="2.1" fill="#F59E0B" />
          <path d="M13.4 7.5h4.2M13.4 13h4.2M13.4 18.5h2.6" stroke="#99F6E4" strokeWidth="1.4" strokeLinecap="round" />
        </svg>
        <span className="text-base font-semibold tracking-tight">RecruitOps</span>
      </div>

      <nav className="flex-1 overflow-y-auto py-3">
        {navGroups.map((group) => {
          const visibleItems = group.items.filter(
            (item) =>
              hasPermission(session, item.permission) &&
              (!item.featureFlag || isFeatureEnabled(item.featureFlag))
          );

          // A group whose every item is hidden must not leave its heading behind — an empty
          // "Team" label tells the user something exists that they cannot see.
          if (visibleItems.length === 0) return null;

          return (
            <div key={group.title} className="pb-1">
              {/* white/50, not the kit's white/40. Measured 2026-08-21 on ink-900: /40 is
                  3.81:1 and fails AA for text this size; /50 is 5.23:1. The kit was corrected
                  to match rather than the other way round. */}
              <p className="px-4 pb-1.5 pt-3 text-2xs font-medium uppercase tracking-wider text-white/50">
                {group.title}
              </p>
              {visibleItems.map((item) => (
                <NavLink key={item.to} to={item.to} className={linkClass}>
                  {({ isActive }) => (
                    <>
                      <Icon active={isActive}>{item.icon}</Icon>
                      {item.label}
                    </>
                  )}
                </NavLink>
              ))}
            </div>
          );
        })}
      </nav>

      {session && (
        <div className="border-t border-white/10 p-3">
          <div className="flex items-center gap-2.5 px-2 py-1">
            <span className="grid h-7 w-7 shrink-0 place-items-center rounded-full bg-brand-700 text-2xs font-semibold">
              {initials}
            </span>
            <span className="min-w-0 flex-1">
              <span className="block truncate text-sm font-medium">{session.displayName}</span>
              {/* Super admin is a role line, not a badge with an emoji. An emoji in an
                  enterprise nav is decoration where a fact belongs. */}
              <span className="block truncate text-2xs text-white/50">
                {isSuperAdmin(session) ? 'Super admin' : session.role}
              </span>
            </span>
          </div>
          <button
            onClick={handleSignOut}
            className="mt-1 w-full rounded-md px-2 py-1.5 text-left text-sm text-white/70 transition-colors hover:bg-white/5 hover:text-white"
          >
            Sign out
          </button>
        </div>
      )}
    </aside>
  );
}
