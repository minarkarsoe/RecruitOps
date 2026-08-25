import { describe, expect, it, beforeEach, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { AppLayout } from './AppLayout';
import { Sidebar } from './Sidebar';
import { Breadcrumbs, getBreadcrumbsForPath } from './Breadcrumbs';
import { Header } from './Header';
import { TenantSwitcherBar } from './TenantSwitcherBar';
import { auth } from '../lib/auth';

describe('Milestone 2 Empirical Challenger Test Suite', () => {
  beforeEach(() => {
    sessionStorage.clear();
  });

  describe('1. Ctrl+K Event Listener Cleanup on Unmount', () => {
    it('attaches keydown listener on mount and removes it on unmount', () => {
      auth.set({
        accessToken: 'token-super',
        expiresAtUtc: '2099-01-01T00:00:00Z',
        role: 'SuperAdmin',
        displayName: 'Super User',
        userId: 'usr-super',
        isSuperAdmin: true,
        permissions: [],
      });

      const addEventListenerSpy = vi.spyOn(window, 'addEventListener');
      const removeEventListenerSpy = vi.spyOn(window, 'removeEventListener');

      const { unmount } = render(
        <MemoryRouter>
          <AppLayout />
        </MemoryRouter>
      );

      // Verify addEventListener was called for keydown
      const keydownCalls = addEventListenerSpy.mock.calls.filter((call) => call[0] === 'keydown');
      expect(keydownCalls.length).toBeGreaterThan(0);
      const listenerHandler = keydownCalls[0][1];

      // Unmount component
      unmount();

      // Verify removeEventListener was called with the exact same handler
      const removeKeydownCalls = removeEventListenerSpy.mock.calls.filter((call) => call[0] === 'keydown');
      expect(removeKeydownCalls.length).toBeGreaterThan(0);
      expect(removeKeydownCalls.some((call) => call[1] === listenerHandler)).toBe(true);

      addEventListenerSpy.mockRestore();
      removeEventListenerSpy.mockRestore();
    });

    it('does not open command palette on keydown after component has unmounted', () => {
      auth.set({
        accessToken: 'token-super',
        expiresAtUtc: '2099-01-01T00:00:00Z',
        role: 'SuperAdmin',
        displayName: 'Super User',
        userId: 'usr-super',
        isSuperAdmin: true,
        permissions: [],
      });

      const { unmount } = render(
        <MemoryRouter>
          <AppLayout />
        </MemoryRouter>
      );

      unmount();

      // Fire Ctrl+K after unmount
      expect(() => {
        fireEvent.keyDown(window, { key: 'k', ctrlKey: true });
      }).not.toThrow();

      // Command palette dialog should not exist
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    });
  });

  describe('2. Active Link Styling in Sidebar', () => {
    it('applies active styling (bg-white/10 + font-medium + text-white) to the current route link', () => {
      auth.set({
        accessToken: 'token-super',
        expiresAtUtc: '2099-01-01T00:00:00Z',
        role: 'SuperAdmin',
        displayName: 'Super User',
        userId: 'usr-super',
        isSuperAdmin: true,
        permissions: [],
      });

      render(
        <MemoryRouter initialEntries={['/requisitions']}>
          <Sidebar />
        </MemoryRouter>
      );

      const reqLink = screen.getByRole('link', { name: 'Requisitions' });
      expect(reqLink).toBeInTheDocument();
      expect(reqLink.className).toContain('bg-white/10');
      expect(reqLink.className).toContain('font-medium');
      expect(reqLink.className).toContain('text-white');
      // No left border any more. The kit marks the active item with a filled `bg-white/10`
      // pill; a border AND a fill is two devices saying the same thing, and on a dark rail the
      // border reads as a seam. Asserted as absent so it does not creep back in.
      expect(reqLink.className).not.toContain('border-l-2');
      expect(reqLink.className).toContain('bg-white/10');

      const postingsLink = screen.getByRole('link', { name: 'Job postings' });
      expect(postingsLink.className).not.toContain('bg-white/10');
      // `text-white/70` on the dark rail, not `text-ink-600` — the rail is ink-900 now, so an
      // ink-600 label on it would be unreadable rather than merely quiet.
      expect(postingsLink.className).toContain('text-white/70');
    });

    it('switches active link styling when route changes', () => {
      auth.set({
        accessToken: 'token-super',
        expiresAtUtc: '2099-01-01T00:00:00Z',
        role: 'SuperAdmin',
        displayName: 'Super User',
        userId: 'usr-super',
        isSuperAdmin: true,
        permissions: [],
      });

      render(
        <MemoryRouter initialEntries={['/users']}>
          <Sidebar />
        </MemoryRouter>
      );

      const usersLink = screen.getByRole('link', { name: 'Users' });
      expect(usersLink.className).toContain('bg-white/10');

      const reqLink = screen.getByRole('link', { name: 'Requisitions' });
      expect(reqLink.className).not.toContain('bg-white/10');
    });
  });

  describe('3. Accessibility Attributes Verification', () => {
    it('verifies Breadcrumbs has nav[aria-label="Breadcrumb"] and aria-current="page" on current leaf item', () => {
      render(
        <MemoryRouter initialEntries={['/requisitions/req-101/edit']}>
          <Breadcrumbs />
        </MemoryRouter>
      );

      const nav = screen.getByRole('navigation', { name: 'Breadcrumb' });
      expect(nav).toBeInTheDocument();

      const leafItem = screen.getByText('Edit');
      expect(leafItem).toHaveAttribute('aria-current', 'page');

      const homeLink = screen.getByRole('link', { name: 'Home' });
      expect(homeLink).not.toHaveAttribute('aria-current');
    });

    it('verifies Header contains accessible search button with aria-label="Search commands"', () => {
      auth.set({
        accessToken: 'token-super',
        expiresAtUtc: '2099-01-01T00:00:00Z',
        role: 'SuperAdmin',
        displayName: 'Super User',
        userId: 'usr-super',
        isSuperAdmin: true,
        permissions: [],
      });

      const onOpen = vi.fn();
      render(
        <MemoryRouter>
          <Header onOpenCommandPalette={onOpen} />
        </MemoryRouter>
      );

      const searchBtn = screen.getByRole('button', { name: 'Search commands' });
      expect(searchBtn).toBeInTheDocument();
      fireEvent.click(searchBtn);
      expect(onOpen).toHaveBeenCalledTimes(1);
    });

    it('verifies Sidebar uses semantic aside element and nav containers for groups', () => {
      auth.set({
        accessToken: 'token-super',
        expiresAtUtc: '2099-01-01T00:00:00Z',
        role: 'SuperAdmin',
        displayName: 'Super User',
        userId: 'usr-super',
        isSuperAdmin: true,
        permissions: [],
      });

      const { container } = render(
        <MemoryRouter>
          <Sidebar />
        </MemoryRouter>
      );

      const aside = container.querySelector('aside');
      expect(aside).toBeInTheDocument();

      const navs = container.querySelectorAll('nav');
      expect(navs.length).toBeGreaterThan(0);
    });
  });

  describe('4. Dynamic Breadcrumbs Logic & Path Formatting', () => {
    it('correctly maps route segments to human-readable names and dynamic detail titles', () => {
      expect(getBreadcrumbsForPath('/')).toEqual([{ label: 'Dashboard', path: '/' }]);

      expect(getBreadcrumbsForPath('/requisitions')).toEqual([
        { label: 'Home', path: '/' },
        { label: 'Requisitions', path: undefined },
      ]);

      expect(getBreadcrumbsForPath('/requisitions/new')).toEqual([
        { label: 'Home', path: '/' },
        { label: 'Requisitions', path: '/requisitions' },
        { label: 'New Requisition', path: undefined },
      ]);

      expect(getBreadcrumbsForPath('/jobpostings/jp-99/edit')).toEqual([
        { label: 'Home', path: '/' },
        { label: 'Job Postings', path: '/jobpostings' },
        { label: 'Posting Details', path: '/jobpostings/jp-99' },
        { label: 'Edit', path: undefined },
      ]);
    });
  });

  describe('5. SuperAdmin Tenant Switcher Bar', () => {
    it('renders TenantSwitcherBar for SuperAdmin users and hides it for non-SuperAdmin users', () => {
      // SuperAdmin
      auth.set({
        accessToken: 'token-super',
        expiresAtUtc: '2099-01-01T00:00:00Z',
        role: 'SuperAdmin',
        displayName: 'Super Admin',
        userId: 'usr-super',
        isSuperAdmin: true,
        permissions: [],
      });

      const { rerender } = render(<TenantSwitcherBar />);
      expect(screen.getByText(/Super-Admin Context/i)).toBeInTheDocument();

      // Regular Recruiter
      auth.set({
        accessToken: 'token-recruiter',
        expiresAtUtc: '2099-01-01T00:00:00Z',
        role: 'Recruiter',
        displayName: 'Jane Recruiter',
        userId: 'usr-recruiter',
        isSuperAdmin: false,
        permissions: [],
      });

      rerender(<TenantSwitcherBar />);
      expect(screen.queryByText(/Super-Admin Context/i)).not.toBeInTheDocument();
    });
  });
});
