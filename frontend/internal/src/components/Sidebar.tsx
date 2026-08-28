import { useCallback, useState } from 'react';
import { NavLink, useLocation, useNavigate } from 'react-router-dom';
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
  /**
   * Omit for a destination every signed-in user may open.
   *
   * Only correct when the server decides reach per row rather than per role — Interviews is the
   * case it was added for: the API's read endpoints are `InternalUser`, not `RecruitmentStaff`,
   * because a panel member is very often a Hiring Manager from another department, and an
   * Interviewer holds no `applications:*` permission at all. Gating the link on one would hide
   * it from exactly the people whose job it is. Someone with no rounds gets an empty list, which
   * is the honest answer rather than a hidden link.
   */
  permission?: string;
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
  interviews: (
    <>
      <circle cx="8" cy="6" r="2.5" stroke="currentColor" strokeWidth="1.4" />
      <path d="M3.5 13c0-2.2 2-3.5 4.5-3.5s4.5 1.3 4.5 3.5"
        stroke="currentColor" strokeWidth="1.4" strokeLinecap="round" />
    </>
  ),
  delivery: (
    <path d="M2.5 4.5h11a1 1 0 011 1v5a1 1 0 01-1 1h-11a1 1 0 01-1-1v-5a1 1 0 011-1zM2.8 5l5.2 3.6L13.2 5"
      stroke="currentColor" strokeWidth="1.4" strokeLinecap="round" strokeLinejoin="round" />
  ),
  menu: (
    <path d="M2.5 4h11M2.5 8h11M2.5 12h11" stroke="currentColor" strokeWidth="1.4" strokeLinecap="round" />
  ),
  chevronDown: (
    <path d="M4 6l4 4 4-4" stroke="currentColor" strokeWidth="1.4" strokeLinecap="round" strokeLinejoin="round" />
  ),
  signOut: (
    <path d="M6 2.5H3.5a1 1 0 00-1 1v9a1 1 0 001 1H6M10 11l3-3-3-3M13 8H6"
      stroke="currentColor" strokeWidth="1.4" strokeLinecap="round" strokeLinejoin="round" />
  ),
};

/**
 * Persisted collapse preference.
 *
 * `localStorage`, deliberately — NOT the `sessionStorage` that `auth` uses. That choice is a
 * security trade-off about a bearer token dying with the tab; this is a width preference. A
 * preference that resets every time you open a tab is not a preference, and there is nothing
 * here worth protecting from an attacker who can already read the page.
 *
 * Every access is guarded: Safari private mode throws on `localStorage` access outright, and a
 * nav rail that cannot render because a preference could not be read would be a poor trade.
 */
const COLLAPSE_KEY = 'recruitops.sidebar.collapsed';

function readCollapsed(): boolean {
  try {
    return localStorage.getItem(COLLAPSE_KEY) === 'true';
  } catch {
    return false;
  }
}

function writeCollapsed(value: boolean): void {
  try {
    localStorage.setItem(COLLAPSE_KEY, String(value));
  } catch {
    /* preference is a convenience; losing it must never break navigation */
  }
}

/**
 * Which groups the user has folded shut.
 *
 * Stored as the SHUT set, not the open set, so that a group added to the rail in a later release
 * is open by default for someone who already has a preference saved. Storing the open set would
 * hide new navigation from exactly the existing users least likely to go looking for it.
 */
const SHUT_GROUPS_KEY = 'recruitops.sidebar.shutGroups';

function readShutGroups(): string[] {
  try {
    const raw = localStorage.getItem(SHUT_GROUPS_KEY);
    if (!raw) return [];
    const parsed: unknown = JSON.parse(raw);
    return Array.isArray(parsed) ? parsed.filter((g): g is string => typeof g === 'string') : [];
  } catch {
    return [];
  }
}

function writeShutGroups(groups: string[]): void {
  try {
    localStorage.setItem(SHUT_GROUPS_KEY, JSON.stringify(groups));
  } catch {
    /* as above */
  }
}

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

  const { pathname } = useLocation();

  // Lazy initialisers: read the stored preferences once, on mount, not on every render.
  const [collapsed, setCollapsed] = useState<boolean>(readCollapsed);
  const [shutGroups, setShutGroups] = useState<string[]>(readShutGroups);

  const toggleCollapsed = useCallback(() => {
    setCollapsed((prev) => {
      const next = !prev;
      writeCollapsed(next);
      return next;
    });
  }, []);

  const toggleGroup = useCallback((title: string) => {
    setShutGroups((prev) => {
      const next = prev.includes(title) ? prev.filter((t) => t !== title) : [...prev, title];
      writeShutGroups(next);
      return next;
    });
  }, []);

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
        // No permission: see the note on NavItem.permission. The API is InternalUser here and
        // the service decides reach per row, so an Interviewer — who holds no `applications:*`
        // permission — still reaches the panels they sit on (ADR-0017 §4).
        { to: '/interviews', label: 'Interviews', icon: icons.interviews },
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

  // Only groups with at least one permitted item survive; computed up front because the
  // collapsed rail draws a hairline BETWEEN groups, and "between" needs the final list. Deriving
  // it inside the map would put a rule above a group that turns out to be empty.
  const visibleGroups = navGroups
    .map((group) => ({
      ...group,
      items: group.items.filter(
        (item) =>
          (item.permission === undefined || hasPermission(session, item.permission)) &&
          (!item.featureFlag || isFeatureEnabled(item.featureFlag))
      ),
    }))
    .filter((group) => group.items.length > 0);

  // A group is shut only if the user shut it AND the page you are on is not inside it. Folding a
  // group closed over the active route would hide where you are — the rail would stop answering
  // "where am I", which is half of what it is for.
  const isGroupOpen = (group: NavGroup) =>
    !shutGroups.includes(group.title) ||
    group.items.some((item) => pathname === item.to || pathname.startsWith(`${item.to}/`));

  const linkClass = ({ isActive }: { isActive: boolean }) =>
    collapsed
      ? `mx-2 grid h-9 place-items-center rounded-md transition-colors ${
          isActive ? 'bg-white/10 text-white' : 'text-white/70 hover:bg-white/5 hover:text-white'
        }`
      : `mx-2 flex h-9 items-center gap-2.5 rounded-md px-2.5 text-base transition-colors ${
          isActive
            ? 'bg-white/10 font-medium text-white'
            : 'text-white/70 hover:bg-white/5 hover:text-white'
        }`;

  const footerBtnClass = collapsed
    ? 'grid h-9 w-full place-items-center rounded-md text-white/70 transition-colors hover:bg-white/5 hover:text-white'
    : 'flex h-9 w-full items-center gap-2.5 rounded-md px-2 text-left text-sm text-white/70 transition-colors hover:bg-white/5 hover:text-white';

  const menuButton = (
    // The hamburger is the toggle, in the header beside the wordmark. It was a "Collapse" row in
    // the footer for about an hour on 2026-08-28; that spent a whole 36px row on a control the
    // header already had room for, and pushed the nav into overflow on a laptop.
    <button
      type="button"
      onClick={toggleCollapsed}
      className="grid h-8 w-8 shrink-0 place-items-center rounded-md text-white/70 transition-colors hover:bg-white/5 hover:text-white"
      aria-expanded={!collapsed}
      aria-controls="app-sidebar"
      title={collapsed ? 'Expand sidebar' : 'Collapse sidebar'}
      aria-label={collapsed ? 'Expand sidebar' : 'Collapse sidebar'}
    >
      <svg className="h-4 w-4" viewBox="0 0 16 16" fill="none" aria-hidden="true">
        {icons.menu}
      </svg>
    </button>
  );

  const initials = (session?.displayName ?? '')
    .split(/\s+/)
    .filter(Boolean)
    .slice(-2)
    .map((part) => part[0]?.toUpperCase() ?? '')
    .join('') || 'U';

  return (
    // Width is NOT transitioned, on purpose. Animating it would relayout the content pane every
    // frame, and the screens this feature exists for — Pipeline, Analytics — are exactly the
    // wide tables where that costs the most. An instant toggle is also simply faster to use.
    <aside
      id="app-sidebar"
      className={`flex shrink-0 flex-col bg-ink-900 text-white ${collapsed ? 'w-16' : 'w-[224px]'}`}
    >
      <div
        className={`flex h-14 shrink-0 items-center border-b border-white/10 ${
          collapsed ? 'justify-center' : 'gap-2.5 px-3'
        }`}
      >
        {collapsed ? (
          // At 64px the hamburger is the header. The mark would have to displace it, and a
          // logo you cannot click is worth less here than the control you need.
          menuButton
        ) : (
          <>
            <svg width="22" height="22" viewBox="0 0 26 26" fill="none" aria-hidden="true" className="ml-1 shrink-0">
              <rect x="1" y="1" width="24" height="24" rx="7" fill="#0F766E" />
              <circle cx="9" cy="7.5" r="2.1" fill="#fff" />
              <circle cx="9" cy="13" r="2.1" fill="#fff" />
              <circle cx="9" cy="18.5" r="2.1" fill="#F59E0B" />
              <path d="M13.4 7.5h4.2M13.4 13h4.2M13.4 18.5h2.6" stroke="#99F6E4" strokeWidth="1.4" strokeLinecap="round" />
            </svg>
            <span className="text-base font-semibold tracking-tight">RecruitOps</span>
            <span className="ml-auto">{menuButton}</span>
          </>
        )}
      </div>

      <nav className="rail-scroll flex-1 overflow-y-auto py-3" aria-label="Main">
        {visibleGroups.map((group, index) => {
          const open = isGroupOpen(group);
          const panelId = `nav-group-${group.title.toLowerCase().replace(/\s+/g, '-')}`;

          return (
            <div key={group.title} className="pb-1">
              {collapsed ? (
                // At 64px a heading cannot be read, so grouping is carried by a rule instead of a
                // word — and with no heading there is nothing to fold, so every item shows. The
                // accordion is a wide-rail affordance; narrowing the rail is already the
                // compaction, and hiding items behind an invisible parent would strand them.
                index > 0 && <div className="mx-3 my-2 border-t border-white/10" aria-hidden="true" />
              ) : (
                /* white/50, not the kit's white/40. Measured 2026-08-21 on ink-900: /40 is
                   3.81:1 and fails AA for text this size; /50 is 5.23:1. The kit was corrected
                   to match rather than the other way round.
                   A <button>, not a <p> with a click handler: it is operable by keyboard and
                   announced as expandable for free. */
                <button
                  type="button"
                  onClick={() => toggleGroup(group.title)}
                  aria-expanded={open}
                  aria-controls={panelId}
                  className="flex w-full items-center gap-1.5 px-4 pb-1.5 pt-3 text-2xs font-medium uppercase tracking-wider text-white/50 transition-colors hover:text-white/80"
                >
                  {group.title}
                  <svg
                    className={`ml-auto h-3 w-3 shrink-0 transition-transform ${open ? '' : '-rotate-90'}`}
                    viewBox="0 0 16 16"
                    fill="none"
                    aria-hidden="true"
                  >
                    {icons.chevronDown}
                  </svg>
                </button>
              )}
              <div id={collapsed ? undefined : panelId} hidden={!collapsed && !open}>
                {group.items.map((item) => (
                  <NavLink
                    key={item.to}
                    to={item.to}
                    className={linkClass}
                    // Both, and they do different jobs: `title` is the mouse's tooltip,
                    // `aria-label` is what a screen reader announces once the visible text is
                    // gone. An icon-only rail carrying neither is a memory test.
                    title={collapsed ? item.label : undefined}
                    aria-label={collapsed ? item.label : undefined}
                  >
                    {({ isActive }) => (
                      <>
                        <Icon active={isActive}>{item.icon}</Icon>
                        {!collapsed && item.label}
                      </>
                    )}
                  </NavLink>
                ))}
              </div>
            </div>
          );
        })}
      </nav>

      {/* The footer holds only the identity block now. The collapse control moved up to the
          header hamburger: it was costing a full 36px row here for something the header already
          had room for, and that row was enough to push the nav into overflow on a laptop. */}
      <div className="shrink-0 border-t border-white/10 p-3">
        {session && (
          <>
            <div
              className={
                collapsed
                  ? 'grid h-9 place-items-center'
                  : 'flex items-center gap-2.5 px-2 py-1'
              }
              // Collapsed, the avatar is the only thing left of the user block, so it has to
              // say who it belongs to on its own.
              title={collapsed ? session.displayName : undefined}
            >
              <span className="grid h-7 w-7 shrink-0 place-items-center rounded-full bg-brand-700 text-2xs font-semibold">
                {initials}
              </span>
              {!collapsed && (
                <span className="min-w-0 flex-1">
                  <span className="block truncate text-sm font-medium">{session.displayName}</span>
                  {/* Super admin is a role line, not a badge with an emoji. An emoji in an
                      enterprise nav is decoration where a fact belongs. */}
                  <span className="block truncate text-2xs text-white/50">
                    {isSuperAdmin(session) ? 'Super admin' : session.role}
                  </span>
                </span>
              )}
            </div>
            <button
              type="button"
              onClick={handleSignOut}
              className={`mt-1 ${footerBtnClass}`}
              title={collapsed ? 'Sign out' : undefined}
              aria-label={collapsed ? 'Sign out' : undefined}
            >
              {collapsed ? (
                <svg className="h-4 w-4" viewBox="0 0 16 16" fill="none" aria-hidden="true">
                  {icons.signOut}
                </svg>
              ) : (
                'Sign out'
              )}
            </button>
          </>
        )}
      </div>
    </aside>
  );
}
