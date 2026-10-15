import { describe, expect, it, beforeEach } from 'vitest';
import { render, screen, fireEvent, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { AppLayout } from './AppLayout';
import { auth } from '../lib/auth';

describe('AppLayout Permission-Aware Navigation', () => {
  beforeEach(() => {
    sessionStorage.clear();
  });

  it('renders all menu items for SuperAdmin', () => {
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
      <MemoryRouter>
        <AppLayout />
      </MemoryRouter>
    );

    expect(screen.getByText('Requisitions')).toBeInTheDocument();
    expect(screen.getByText('Job postings')).toBeInTheDocument();
    expect(screen.getByText('Inbox')).toBeInTheDocument();
    expect(screen.getByText('Users')).toBeInTheDocument();
    expect(screen.getByText('Role Builder')).toBeInTheDocument();
  });

  it('filters menu items based on custom role granular permissions', () => {
    auth.set({
      accessToken: 'token-custom',
      expiresAtUtc: '2099-01-01T00:00:00Z',
      role: 'Recruiter',
      displayName: 'Custom Recruiter',
      userId: 'usr-custom',
      isSuperAdmin: false,
      permissions: ['permission:requisitions:requisitions:read'],
    });

    render(
      <MemoryRouter>
        <AppLayout />
      </MemoryRouter>
    );

    // Should render Requisitions and JD templates (both check permission:requisitions:requisitions:read)
    expect(screen.getByText('Requisitions')).toBeInTheDocument();
    expect(screen.getByText('JD templates')).toBeInTheDocument();

    // Should hide links for ungranted permissions
    expect(screen.queryByText('Job postings')).not.toBeInTheDocument();
    expect(screen.queryByText('Inbox')).not.toBeInTheDocument();
    expect(screen.queryByText('Users')).not.toBeInTheDocument();
    expect(screen.queryByText('Role Builder')).not.toBeInTheDocument();
  });

  it('hides every gated link when the session carries no permissions array', () => {
    // Regression: this is the shape the API actually returned before `permissions` was added
    // to LoginResponse. `hasPermission()` read the missing array as "unknown, allow", so a
    // plain Recruiter was served the full admin sidebar — Users and Role Builder included.
    sessionStorage.setItem(
      'recruitops.session',
      JSON.stringify({
        accessToken: 'token-pre-rbac',
        expiresAtUtc: '2099-01-01T00:00:00Z',
        role: 'Recruiter',
        displayName: 'Pre-RBAC Recruiter',
        userId: 'usr-pre-rbac',
      })
    );

    render(
      <MemoryRouter>
        <AppLayout />
      </MemoryRouter>
    );

    expect(screen.queryByText('Users')).not.toBeInTheDocument();
    expect(screen.queryByText('Role Builder')).not.toBeInTheDocument();
    expect(screen.queryByText('Requisitions')).not.toBeInTheDocument();
    expect(screen.queryByText('Job postings')).not.toBeInTheDocument();
  });

  it('renders Users and Role Builder when appropriate permissions are assigned', () => {
    auth.set({
      accessToken: 'token-custom-admin',
      expiresAtUtc: '2099-01-01T00:00:00Z',
      role: 'CustomRole',
      displayName: 'User Admin Custom',
      userId: 'usr-user-admin',
      isSuperAdmin: false,
      permissions: ['permission:users:users:read', 'permission:roles:roles:read'],
    });

    render(
      <MemoryRouter>
        <AppLayout />
      </MemoryRouter>
    );

    expect(screen.getByText('Users')).toBeInTheDocument();
    expect(screen.getByText('Role Builder')).toBeInTheDocument();
    expect(screen.queryByText('Requisitions')).not.toBeInTheDocument();
    expect(screen.queryByText('Job postings')).not.toBeInTheDocument();
  });

  it('opens Command Palette when Ctrl+K keyboard shortcut is pressed', () => {
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
      <MemoryRouter>
        <AppLayout />
      </MemoryRouter>
    );

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();

    fireEvent.keyDown(window, { key: 'k', ctrlKey: true });

    expect(screen.getByRole('dialog', { name: /command palette/i })).toBeInTheDocument();
  });

  it('updates Breadcrumbs dynamically based on the current location route', () => {
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
      <MemoryRouter initialEntries={['/requisitions/req-123/edit']}>
        <AppLayout />
      </MemoryRouter>
    );

    const breadcrumbNav = screen.getByRole('navigation', { name: /breadcrumb/i });
    expect(breadcrumbNav).toBeInTheDocument();
    expect(screen.getAllByText('Requisitions').length).toBeGreaterThan(0);
    expect(screen.getByText('Requisition Details')).toBeInTheDocument();
    expect(screen.getByText('Edit')).toBeInTheDocument();
  });

  it('allows searching and route navigation via Command Palette', () => {
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
      <MemoryRouter initialEntries={['/']}>
        <AppLayout />
      </MemoryRouter>
    );

    const searchBtn = screen.getByRole('button', { name: /search commands/i });
    fireEvent.click(searchBtn);

    const dialog = screen.getByRole('dialog', { name: /command palette/i });
    expect(dialog).toBeInTheDocument();

    const input = screen.getByPlaceholderText(/type a command or search/i);
    fireEvent.change(input, { target: { value: 'Role Builder' } });

    const roleOption = within(dialog).getByText('Role Builder');
    expect(roleOption).toBeInTheDocument();

    fireEvent.click(roleOption);

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });
});

/**
 * The app shell must be exactly one viewport tall, with the CONTENT pane scrolling inside it —
 * the pattern every screen in `design/internal/` uses (`body.overflow-hidden` around
 * `div.flex.h-screen`).
 *
 * ⚠️ What these tests can and cannot prove. jsdom does no layout: every `getBoundingClientRect`
 * is 0×0, so a test here CANNOT measure that "Sign out" is on screen. These pin the class
 * contract that produces the behaviour, and nothing more. The behaviour itself was measured in a
 * real browser on 2026-08-28 against the production build, with 3000px of filler in `<main>`:
 *
 *   fixed     → aside 720px (= viewport), Sign out at y=676 and visible, document not scrollable
 *   reverted  → aside 3124px, Sign out at y=3080, 2392px of scrolling needed to reach it
 *
 * That second row is the bug as reported: the rail grew to the height of the tallest page and
 * took its own footer off screen with it. Treat a failure here as "the shell stopped being a
 * shell", and re-measure in a browser rather than adjusting the expectation.
 */
describe('AppLayout shell geometry (ADR-0025 app-shell pattern)', () => {
  beforeEach(() => {
    sessionStorage.clear();
    auth.set({
      accessToken: 'token-shell',
      expiresAtUtc: '2099-01-01T00:00:00Z',
      role: 'Admin',
      displayName: 'Shell Tester',
      userId: 'usr-shell',
      isSuperAdmin: false,
      permissions: ['permission:users:users:read'],
    });
  });

  function renderShell() {
    const { container } = render(
      <MemoryRouter>
        <AppLayout />
      </MemoryRouter>
    );
    const aside = container.querySelector('aside')!;
    return { container, aside, contentPane: aside.nextElementSibling as HTMLElement };
  }

  it('caps the shell at one viewport instead of letting it grow with the page', () => {
    const { container } = renderShell();
    const shell = container.firstElementChild as HTMLElement;

    expect(shell.className).toContain('h-screen');
    expect(shell.className).toContain('overflow-hidden');
    // `min-h-screen` is the regression: it lets the shell — and with it the rail — grow to the
    // height of the tallest page on the screen.
    expect(shell.className).not.toContain('min-h-screen');
  });

  it('scrolls the content pane, not the document', () => {
    const { contentPane } = renderShell();

    expect(contentPane.className).toContain('overflow-y-auto');
  });

  it('lets the shell row shrink so the inner scroll container can engage', () => {
    const { aside } = renderShell();
    const row = aside.parentElement!;

    // Without `min-h-0` a flex item's default `min-height:auto` floors it at content height, so
    // the pane's `overflow-y-auto` never activates and the document scrolls after all.
    expect(row.className).toContain('min-h-0');
    expect(row.className).not.toContain('min-h-screen');
  });

  it('keeps the sign-out control inside the rail, below the scrolling nav', () => {
    const { aside } = renderShell();
    const nav = aside.querySelector('nav')!;
    const signOut = within(aside).getByRole('button', { name: /sign out/i });

    // The nav is the part that scrolls when there are more links than fit; the footer holding
    // the user block and Sign out is a sibling of it, so it stays pinned to the bottom.
    expect(nav.className).toContain('overflow-y-auto');
    expect(nav.contains(signOut)).toBe(false);
    expect(aside.contains(signOut)).toBe(true);
  });
});
