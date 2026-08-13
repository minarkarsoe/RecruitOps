import { useState, useEffect } from 'react';
import { Outlet, useNavigate } from 'react-router-dom';
import { CommandPalette, type CommandItem } from '@recruitops/ui';
import { auth, hasPermission } from '../lib/auth';
import { TenantSwitcherBar } from './TenantSwitcherBar';
import { Sidebar } from './Sidebar';
import { Header } from './Header';
import { useSearch } from '../features/search/useSearch';

export function AppLayout() {
  const navigate = useNavigate();
  const session = auth.get();
  const [isCommandPaletteOpen, setIsCommandPaletteOpen] = useState(false);

  const { query, setQuery, results, isLoading, error, clear } = useSearch({
    enabled: isCommandPaletteOpen,
  });

  const searchResults: CommandItem[] = (results?.items ?? []).map((item) => ({
    id: item.id,
    title: item.title,
    description:
      item.subtitle ||
      (item.descriptionSnippet ? item.descriptionSnippet.replace(/<[^>]+>/g, '') : undefined),
    category: item.category === 'Postings' ? 'Job Postings' : item.category,
    path: item.targetUrl,
  }));

  function signOut() {
    auth.clear();
    navigate('/login', { replace: true });
  }

  const handleClosePalette = () => {
    setIsCommandPaletteOpen(false);
    clear();
  };

  // Global Ctrl+K / Cmd+K keyboard shortcut listener
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === 'k') {
        e.preventDefault();
        setIsCommandPaletteOpen((prev) => {
          if (prev) {
            clear();
          }
          return !prev;
        });
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [clear]);

  const rawCommandItems = [
    {
      id: 'nav-requisitions',
      title: 'Requisitions',
      description: 'View and manage active job requisitions',
      category: 'Navigation',
      path: '/requisitions',
      shortcut: 'G R',
      permission: 'permission:requisitions:requisitions:read',
    },
    {
      id: 'action-new-requisition',
      title: 'Create New Requisition',
      description: 'Submit a new hiring request',
      category: 'Quick Actions',
      path: '/requisitions/new',
      shortcut: 'N R',
      permission: 'permission:requisitions:requisitions:create',
    },
    {
      id: 'nav-jobpostings',
      title: 'Job Postings',
      description: 'Manage external and internal job postings',
      category: 'Navigation',
      path: '/jobpostings',
      shortcut: 'G J',
      permission: 'permission:postings:postings:read',
    },
    {
      id: 'nav-inbox',
      title: 'Inbox',
      description: 'Review pending requisition approvals',
      category: 'Navigation',
      path: '/inbox',
      shortcut: 'G I',
      permission: 'permission:requisitions:requisitions:approve',
    },
    {
      id: 'nav-jdtemplates',
      title: 'JD Templates',
      description: 'Job description templates library',
      category: 'Navigation',
      path: '/jdtemplates',
      permission: 'permission:requisitions:requisitions:read',
    },
    {
      id: 'nav-scorecardtemplates',
      title: 'Scorecard Templates',
      description: 'Evaluation criteria & interview rubrics',
      category: 'Navigation',
      path: '/scorecardtemplates',
      permission: 'permission:scorecards:scorecards:manage_templates',
    },
    {
      id: 'nav-analytics',
      title: 'Reporting & Analytics',
      description: 'Recruitment KPIs, time-to-hire, funnel conversion & custom reports',
      category: 'Insights',
      path: '/analytics',
      shortcut: 'G A',
      permission: 'permission:requisitions:requisitions:read',
    },
    {
      id: 'nav-approvalchains',
      title: 'Approval Chains',
      description: 'Configure approval workflows',
      category: 'Governance',
      path: '/approvalchains',
      permission: 'permission:settings:settings:read',
    },
    {
      id: 'nav-departments',
      title: 'Departments',
      description: 'Manage organization departments',
      category: 'Governance',
      path: '/departments',
      shortcut: 'G D',
      permission: 'permission:settings:settings:read',
    },
    {
      id: 'nav-users',
      title: 'Users',
      description: 'Manage team members and user directory',
      category: 'Governance',
      path: '/users',
      permission: 'permission:users:users:read',
    },
    {
      id: 'nav-roles',
      title: 'Role Builder',
      description: 'Configure RBAC roles and permissions',
      category: 'Governance',
      path: '/roles',
      permission: 'permission:roles:roles:read',
    },
  ];

  const commandItems = rawCommandItems
    .filter((item) => hasPermission(session, item.permission))
    .map(({ permission: _, ...item }) => item);

  return (
    <div className="min-h-screen flex flex-col bg-surface-50">
      {/* SuperAdmin tenant switcher banner */}
      <TenantSwitcherBar />

      {/* Main shell container */}
      <div className="flex flex-1 min-h-screen">
        {/* Collateral Grouped Sidebar */}
        <Sidebar session={session} onSignOut={signOut} />

        {/* Content area with sticky Header */}
        <div className="flex-1 flex flex-col min-w-0">
          <Header onOpenCommandPalette={() => setIsCommandPaletteOpen(true)} />
          <main className="mx-auto w-full max-w-[1280px] p-6 flex-1">
            <Outlet />
          </main>
        </div>
      </div>

      {/* Global Command Palette modal primitive */}
      <CommandPalette
        isOpen={isCommandPaletteOpen}
        onClose={handleClosePalette}
        onSelectRoute={(path) => {
          navigate(path);
          handleClosePalette();
        }}
        items={commandItems}
        searchResults={searchResults}
        query={query}
        onQueryChange={setQuery}
        isLoading={isLoading}
        error={error}
      />
    </div>
  );
}

