import { describe, expect, it, beforeEach } from 'vitest';
import { render, screen, fireEvent, within } from '@testing-library/react';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import { AppLayout } from './AppLayout';
import { getBreadcrumbsForPath } from './Breadcrumbs';
import { auth } from '../lib/auth';

describe('Milestone 2 Empirical Stress Testing & Verification', () => {
  beforeEach(() => {
    sessionStorage.clear();
  });

  describe('1. Breadcrumb Path Mapping Engine', () => {
    it('maps root path correctly', () => {
      const crumbs = getBreadcrumbsForPath('/');
      expect(crumbs).toEqual([{ label: 'Dashboard', path: '/' }]);
    });

    it('maps top-level routes correctly', () => {
      expect(getBreadcrumbsForPath('/requisitions')).toEqual([
        { label: 'Home', path: '/' },
        { label: 'Requisitions', path: undefined },
      ]);
      expect(getBreadcrumbsForPath('/jobpostings')).toEqual([
        { label: 'Home', path: '/' },
        { label: 'Job Postings', path: undefined },
      ]);
      expect(getBreadcrumbsForPath('/inbox')).toEqual([
        { label: 'Home', path: '/' },
        { label: 'Inbox', path: undefined },
      ]);
      expect(getBreadcrumbsForPath('/roles')).toEqual([
        { label: 'Home', path: '/' },
        { label: 'Role Builder', path: undefined },
      ]);
    });

    it('maps nested detail view routes and creation routes correctly', () => {
      expect(getBreadcrumbsForPath('/requisitions/req-999')).toEqual([
        { label: 'Home', path: '/' },
        { label: 'Requisitions', path: '/requisitions' },
        { label: 'Requisition Details', path: undefined },
      ]);
      expect(getBreadcrumbsForPath('/requisitions/req-999/edit')).toEqual([
        { label: 'Home', path: '/' },
        { label: 'Requisitions', path: '/requisitions' },
        { label: 'Requisition Details', path: '/requisitions/req-999' },
        { label: 'Edit', path: undefined },
      ]);
      expect(getBreadcrumbsForPath('/requisitions/new')).toEqual([
        { label: 'Home', path: '/' },
        { label: 'Requisitions', path: '/requisitions' },
        { label: 'New Requisition', path: undefined },
      ]);
      expect(getBreadcrumbsForPath('/jobpostings/jp-123')).toEqual([
        { label: 'Home', path: '/' },
        { label: 'Job Postings', path: '/jobpostings' },
        { label: 'Posting Details', path: undefined },
      ]);
      expect(getBreadcrumbsForPath('/jobpostings/new')).toEqual([
        { label: 'Home', path: '/' },
        { label: 'Job Postings', path: '/jobpostings' },
        { label: 'New Job Posting', path: undefined },
      ]);
      expect(getBreadcrumbsForPath('/interviews/int-456')).toEqual([
        { label: 'Home', path: '/' },
        { label: 'Interviews', path: '/interviews' },
        { label: 'Interview Round', path: undefined },
      ]);
    });
  });

  describe('2. Ctrl+K & Cmd+K Keyboard Shortcut & Command Palette', () => {
    it('opens and closes palette using Ctrl+K', () => {
      auth.set({
        accessToken: 'token-test',
        expiresAtUtc: '2099-01-01T00:00:00Z',
        role: 'SuperAdmin',
        displayName: 'Test Admin',
        userId: 'usr-1',
        isSuperAdmin: true,
        permissions: [],
      });

      render(
        <MemoryRouter initialEntries={['/']}>
          <AppLayout />
        </MemoryRouter>
      );

      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();

      // Open with Ctrl+k
      fireEvent.keyDown(window, { key: 'k', ctrlKey: true });
      expect(screen.getByRole('dialog', { name: /command palette/i })).toBeInTheDocument();

      // Toggle close with Ctrl+k
      fireEvent.keyDown(window, { key: 'k', ctrlKey: true });
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    });

    it('opens palette using Cmd+K on Mac', () => {
      auth.set({
        accessToken: 'token-test',
        expiresAtUtc: '2099-01-01T00:00:00Z',
        role: 'SuperAdmin',
        displayName: 'Test Admin',
        userId: 'usr-1',
        isSuperAdmin: true,
        permissions: [],
      });

      render(
        <MemoryRouter initialEntries={['/']}>
          <AppLayout />
        </MemoryRouter>
      );

      fireEvent.keyDown(window, { key: 'K', metaKey: true });
      expect(screen.getByRole('dialog', { name: /command palette/i })).toBeInTheDocument();
    });

    it('filters command palette items by permission', () => {
      auth.set({
        accessToken: 'token-recruiter',
        expiresAtUtc: '2099-01-01T00:00:00Z',
        role: 'Recruiter',
        displayName: 'Recruiter User',
        userId: 'usr-2',
        isSuperAdmin: false,
        permissions: ['permission:requisitions:requisitions:read'],
      });

      render(
        <MemoryRouter initialEntries={['/']}>
          <AppLayout />
        </MemoryRouter>
      );

      // Open palette via Header button
      fireEvent.click(screen.getByRole('button', { name: /search commands/i }));

      const dialog = screen.getByRole('dialog', { name: /command palette/i });
      expect(within(dialog).getByText('Requisitions')).toBeInTheDocument();
      expect(within(dialog).getByText('JD Templates')).toBeInTheDocument();

      // Ungranted permissions items must NOT be listed
      expect(within(dialog).queryByText('Job Postings')).not.toBeInTheDocument();
      expect(within(dialog).queryByText('Users')).not.toBeInTheDocument();
      expect(within(dialog).queryByText('Role Builder')).not.toBeInTheDocument();
    });

    it('navigates to selected route when clicking item in command palette', () => {
      auth.set({
        accessToken: 'token-admin',
        expiresAtUtc: '2099-01-01T00:00:00Z',
        role: 'SuperAdmin',
        displayName: 'Admin User',
        userId: 'usr-3',
        isSuperAdmin: true,
        permissions: [],
      });

      render(
        <MemoryRouter initialEntries={['/']}>
          <Routes>
            <Route path="/" element={<AppLayout />}>
              <Route path="users" element={<div data-testid="users-page">Users Page Target</div>} />
            </Route>
          </Routes>
        </MemoryRouter>
      );

      fireEvent.keyDown(window, { key: 'k', ctrlKey: true });
      const dialog = screen.getByRole('dialog', { name: /command palette/i });
      fireEvent.click(within(dialog).getByText('Users'));

      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
      expect(screen.getByTestId('users-page')).toBeInTheDocument();
    });
  });

  describe('3. Sidebar Grouping & Permission Filtering', () => {
    it('groups navigation into Recruitment, Team, and Governance headers', () => {
      auth.set({
        accessToken: 'token-admin',
        expiresAtUtc: '2099-01-01T00:00:00Z',
        role: 'SuperAdmin',
        displayName: 'Super Admin',
        userId: 'usr-4',
        isSuperAdmin: true,
        permissions: [],
      });

      render(
        <MemoryRouter initialEntries={['/']}>
          <AppLayout />
        </MemoryRouter>
      );

      expect(screen.getByText('Recruitment')).toBeInTheDocument();
      expect(screen.getByText('Team')).toBeInTheDocument();
      expect(screen.getByText('Governance')).toBeInTheDocument();
    });

    it('hides entire group header if user has 0 permitted items in that group', () => {
      auth.set({
        accessToken: 'token-limited',
        expiresAtUtc: '2099-01-01T00:00:00Z',
        role: 'HiringManager',
        displayName: 'Limited HM',
        userId: 'usr-5',
        isSuperAdmin: false,
        permissions: ['permission:requisitions:requisitions:read'],
      });

      render(
        <MemoryRouter initialEntries={['/']}>
          <AppLayout />
        </MemoryRouter>
      );

      // Recruitment group visible (Requisitions & JD templates)
      expect(screen.getByText('Recruitment')).toBeInTheDocument();

      // Team & Governance groups should not render
      expect(screen.queryByText('Team')).not.toBeInTheDocument();
      expect(screen.queryByText('Governance')).not.toBeInTheDocument();
    });

    // ⚠️ REWRITTEN 2026-08-28. This used to assert that a user with no permissions sees NO nav
    // groups at all. That stopped being true when Interviews was added, and the change is
    // deliberate rather than a regression: the interviews list carries no `permission`, because
    // the people who most need it — an Interviewer, an Approver sitting on one panel — hold no
    // `applications:*` permission, and gating the link on one would hide it from exactly them.
    // The API is `InternalUser` and decides reach per row (ADR-0017 §4), so someone with nothing
    // to see gets an empty list rather than a hidden link.
    //
    // What still matters, and is what this test now pins: the permission-gated groups stay
    // hidden, and the only surviving link is the unguarded one.
    it('shows a user with no permissions only the destinations that need none', () => {
      auth.set({
        accessToken: 'token-empty',
        expiresAtUtc: '2099-01-01T00:00:00Z',
        role: 'RestrictedRole',
        displayName: 'No Access User',
        userId: 'usr-6',
        isSuperAdmin: false,
        permissions: [],
      });

      render(
        <MemoryRouter initialEntries={['/']}>
          <AppLayout />
        </MemoryRouter>
      );

      // Recruitment survives, carrying exactly one link: the unguarded Interviews entry.
      expect(screen.getByText('Recruitment')).toBeInTheDocument();
      expect(screen.getByRole('link', { name: 'Interviews' })).toBeInTheDocument();
      for (const gated of ['Requisitions', 'Job postings', 'JD templates', 'Delivery log']) {
        expect(screen.queryByRole('link', { name: gated })).not.toBeInTheDocument();
      }

      // The permission-gated groups are still gone entirely — an empty heading would tell the
      // user something exists that they cannot see.
      expect(screen.queryByText('Team')).not.toBeInTheDocument();
      expect(screen.queryByText('Governance')).not.toBeInTheDocument();
      // ONE, not two. The signed-in name used to render in both the sidebar and the header,
      // and this assertion pinned the duplication. ADR-0025's kit puts identity in the nav
      // rail's footer and nowhere else: two avatars on one screen is two places to check who
      // you are signed in as, and they can disagree while a session is being replaced.
      expect(screen.getAllByText('No Access User').length).toBe(1);
    });
  });

  describe('4. Header Quick Actions & User Profile Context', () => {
    it('renders New Requisition button only when user has requisition create permission', () => {
      // HM without create permission
      auth.set({
        accessToken: 'token-hm',
        expiresAtUtc: '2099-01-01T00:00:00Z',
        role: 'HiringManager',
        displayName: 'HM User',
        userId: 'usr-7',
        isSuperAdmin: false,
        permissions: ['permission:requisitions:requisitions:read'],
      });

      const { rerender } = render(
        <MemoryRouter initialEntries={['/']}>
          <AppLayout />
        </MemoryRouter>
      );

      expect(screen.queryByRole('button', { name: /new requisition/i })).not.toBeInTheDocument();

      // Admin with create permission
      auth.set({
        accessToken: 'token-creator',
        expiresAtUtc: '2099-01-01T00:00:00Z',
        role: 'Recruiter',
        displayName: 'Recruiter Creator',
        userId: 'usr-8',
        isSuperAdmin: false,
        permissions: ['permission:requisitions:requisitions:create'],
      });

      rerender(
        <MemoryRouter initialEntries={['/']}>
          <AppLayout />
        </MemoryRouter>
      );

      expect(screen.getByRole('button', { name: /new requisition/i })).toBeInTheDocument();
    });
  });
});
