import { NavLink, useNavigate } from 'react-router-dom';
import { auth, hasPermission, isSuperAdmin, Session } from '../lib/auth';

interface SidebarProps {
  session?: Session | null;
  onSignOut?: () => void;
}

interface NavItem {
  to: string;
  label: string;
  permission: string;
}

interface NavGroup {
  title: string;
  items: NavItem[];
}

export function Sidebar({ session: propSession, onSignOut }: SidebarProps) {
  const navigate = useNavigate();
  const session = propSession !== undefined ? propSession : auth.get();

  function handleSignOut() {
    if (onSignOut) {
      onSignOut();
    } else {
      auth.clear();
      navigate('/login', { replace: true });
    }
  }

  const navGroups: NavGroup[] = [
    {
      title: 'Recruitment',
      items: [
        {
          to: '/requisitions',
          label: 'Requisitions',
          permission: 'permission:requisitions:requisitions:read',
        },
        {
          to: '/jobpostings',
          label: 'Job postings',
          permission: 'permission:postings:postings:read',
        },
        {
          to: '/inbox',
          label: 'Inbox',
          permission: 'permission:requisitions:requisitions:approve',
        },
        {
          to: '/jdtemplates',
          label: 'JD templates',
          permission: 'permission:requisitions:requisitions:read',
        },
        {
          to: '/scorecardtemplates',
          label: 'Scorecard templates',
          permission: 'permission:scorecards:scorecards:manage_templates',
        },
      ],
    },
    {
      title: 'Team',
      items: [
        {
          to: '/users',
          label: 'Users',
          permission: 'permission:users:users:read',
        },
        {
          to: '/departments',
          label: 'Departments',
          permission: 'permission:settings:settings:read',
        },
      ],
    },
    {
      title: 'Governance',
      items: [
        {
          to: '/approvalchains',
          label: 'Approval chains',
          permission: 'permission:settings:settings:read',
        },
        {
          to: '/roles',
          label: 'Role Builder',
          permission: 'permission:roles:roles:read',
        },
      ],
    },
  ];

  const linkClass = ({ isActive }: { isActive: boolean }) =>
    `flex items-center gap-2 rounded-md px-3 py-1.5 text-sm font-medium transition-colors ${
      isActive
        ? 'bg-primary-100 font-semibold text-primary-700 border-l-2 border-primary-600'
        : 'text-ink-600 hover:bg-surface-50 hover:text-ink-900'
    }`;

  return (
    <aside className="w-60 shrink-0 border-r border-line-200 bg-surface-0 flex flex-col justify-between p-4 h-full min-h-screen">
      <div>
        {/* Brand & Workspace Header */}
        <div className="mb-6 px-3 flex items-center justify-between">
          <div className="flex items-center gap-2">
            <div className="h-7 w-7 rounded-lg bg-primary-600 flex items-center justify-center text-white font-bold text-sm shadow-xs">
              R
            </div>
            <span className="font-display text-lg font-bold tracking-tight text-ink-900">
              RecruitOps
            </span>
          </div>
          <span className="text-[10px] font-semibold bg-surface-100 text-ink-500 px-1.5 py-0.5 rounded uppercase tracking-wider border border-line-200">
            CRM
          </span>
        </div>

        {/* Grouped Navigation Links */}
        <div className="space-y-5">
          {navGroups.map((group) => {
            const visibleItems = group.items.filter((item) =>
              hasPermission(session, item.permission)
            );

            if (visibleItems.length === 0) return null;

            return (
              <div key={group.title}>
                <div className="px-3 mb-1 text-[11px] font-bold text-ink-400 uppercase tracking-wider">
                  {group.title}
                </div>
                <nav className="space-y-0.5">
                  {visibleItems.map((item) => (
                    <NavLink key={item.to} to={item.to} className={linkClass}>
                      {item.label}
                    </NavLink>
                  ))}
                </nav>
              </div>
            );
          })}
        </div>
      </div>

      {/* User Profile Card */}
      {session && (
        <div className="mt-8 border-t border-line-200 px-3 pt-4">
          <div className="flex items-center justify-between">
            <div className="min-w-0 flex-1">
              <div className="flex items-center gap-1.5">
                <p className="text-sm font-semibold text-ink-900 truncate">
                  {session.displayName}
                </p>
                {isSuperAdmin(session) && (
                  <span className="text-[10px] font-bold px-1.5 py-0.2 rounded bg-amber-100 text-amber-900 border border-amber-300 shrink-0">
                    👑 Super
                  </span>
                )}
              </div>
              <p className="text-xs text-ink-400 truncate">{session.role}</p>
            </div>
          </div>
          <button
            onClick={handleSignOut}
            className="mt-3 text-xs font-semibold text-primary-600 hover:text-primary-700 transition-colors"
          >
            Sign out
          </button>
        </div>
      )}
    </aside>
  );
}
