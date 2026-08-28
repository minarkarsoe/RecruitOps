import { describe, expect, it, beforeEach, vi, afterEach } from 'vitest';
import { render, screen, fireEvent, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { Sidebar } from './Sidebar';
import type { Session } from '../lib/auth';

/**
 * The rail's collapsed state (added 2026-08-28, drawn in `design/internal/components.html`
 * under "Nav rail — collapsed state").
 *
 * The risk this file covers is not that collapsing fails to narrow the rail — that is one class
 * name and it is obvious when wrong. It is that collapsing makes the rail **unusable to someone
 * who cannot see it**: strip the labels and an icon-only nav is a memory test for a sighted user
 * and silence for a screen reader. So most of what is asserted here is accessible names, not
 * widths.
 */

const session: Session = {
  accessToken: 'token-sidebar',
  expiresAtUtc: '2099-01-01T00:00:00Z',
  role: 'Admin',
  displayName: 'Ma Su Su Hlaing',
  userId: 'usr-sidebar',
  isSuperAdmin: false,
  permissions: [
    'permission:requisitions:requisitions:read',
    'permission:postings:postings:read',
    'permission:requisitions:requisitions:approve',
    'permission:scorecards:scorecards:manage_templates',
    'permission:applications:applications:read',
    'permission:users:users:read',
    'permission:settings:settings:read',
    'permission:roles:roles:read',
  ],
};

const COLLAPSE_KEY = 'recruitops.sidebar.collapsed';

function renderSidebar(onSignOut = vi.fn()) {
  const { container } = render(
    <MemoryRouter>
      <Sidebar session={session} onSignOut={onSignOut} />
    </MemoryRouter>
  );
  return { container, aside: container.querySelector('aside')!, onSignOut };
}

describe('Sidebar collapse', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('starts expanded when no preference has been stored', () => {
    const { aside } = renderSidebar();

    expect(aside.className).toContain('w-[224px]');
    expect(screen.getByText('RecruitOps')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /collapse sidebar/i })).toHaveAttribute(
      'aria-expanded',
      'true'
    );
  });

  it('restores the collapsed preference from a previous visit', () => {
    localStorage.setItem(COLLAPSE_KEY, 'true');

    const { aside } = renderSidebar();

    expect(aside.className).toContain('w-16');
    expect(screen.getByRole('button', { name: /expand sidebar/i })).toHaveAttribute(
      'aria-expanded',
      'false'
    );
  });

  it('persists the choice, because a preference that resets is not a preference', () => {
    const { aside } = renderSidebar();

    fireEvent.click(screen.getByRole('button', { name: /collapse sidebar/i }));

    expect(localStorage.getItem(COLLAPSE_KEY)).toBe('true');
    expect(aside.className).toContain('w-16');

    fireEvent.click(screen.getByRole('button', { name: /expand sidebar/i }));

    expect(localStorage.getItem(COLLAPSE_KEY)).toBe('false');
    expect(aside.className).toContain('w-[224px]');
  });

  it('keeps an accessible name on every link once the visible labels are gone', () => {
    localStorage.setItem(COLLAPSE_KEY, 'true');
    renderSidebar();

    // The visible text is gone…
    expect(screen.queryByText('Requisitions')).not.toBeInTheDocument();
    expect(screen.queryByText('Delivery log')).not.toBeInTheDocument();

    // …but every destination is still addressable by name.
    //
    // ⚠️ This loop is NOT sufficient on its own, and the next test exists because of it. The
    // accessible-name algorithm falls back to `title`, so `getByRole({ name })` still resolves
    // when `aria-label` is dropped entirely — verified by mutation on 2026-08-28, where removing
    // the label left all ten of these tests green. `title` as the *sole* accessible name is the
    // spec's last resort and is unevenly supported; asserting the attribute is what actually
    // pins the intent.
    for (const label of [
      'Requisitions',
      'Job postings',
      'Inbox',
      'JD templates',
      'Scorecard templates',
      'Delivery log',
      'Analytics',
      'Users',
      'Departments',
      'Approval chains',
      'Role Builder',
    ]) {
      expect(screen.getByRole('link', { name: label })).toBeInTheDocument();
    }
  });

  it('labels collapsed links with BOTH aria-label and title', () => {
    localStorage.setItem(COLLAPSE_KEY, 'true');
    renderSidebar();

    // They do different jobs: `title` is the mouse's tooltip, `aria-label` is the name assistive
    // tech announces. Dropping either degrades one audience silently.
    for (const label of ['Analytics', 'Delivery log', 'Role Builder']) {
      const link = screen.getByRole('link', { name: label });
      expect(link).toHaveAttribute('aria-label', label);
      expect(link).toHaveAttribute('title', label);
    }
  });

  it('does not put a title on expanded links, where the label is already visible', () => {
    renderSidebar();

    expect(screen.getByRole('link', { name: 'Analytics' })).not.toHaveAttribute('title');
  });

  it('replaces group headings with rules, and never rules above the first group', () => {
    const { aside } = renderSidebar();
    const nav = within(aside).getByRole('navigation');

    // Expanded: headings present, no separators.
    expect(within(nav).getByText('Recruitment')).toBeInTheDocument();
    expect(within(nav).getByText('Governance')).toBeInTheDocument();
    expect(nav.querySelectorAll('div[aria-hidden="true"]')).toHaveLength(0);

    fireEvent.click(screen.getByRole('button', { name: /collapse sidebar/i }));

    // Collapsed: headings gone, and one rule BETWEEN each pair of groups — four groups means
    // three rules, not four. A leading rule would separate the first group from nothing.
    expect(within(nav).queryByText('Recruitment')).not.toBeInTheDocument();
    expect(within(nav).queryByText('Governance')).not.toBeInTheDocument();
    expect(nav.querySelectorAll('div[aria-hidden="true"]')).toHaveLength(3);
  });

  it('keeps sign-out reachable and named when collapsed', () => {
    localStorage.setItem(COLLAPSE_KEY, 'true');
    const { onSignOut } = renderSidebar();

    const signOut = screen.getByRole('button', { name: /^sign out$/i });
    fireEvent.click(signOut);

    expect(onSignOut).toHaveBeenCalledTimes(1);
  });

  it('names the avatar when it is all that is left of the user block', () => {
    localStorage.setItem(COLLAPSE_KEY, 'true');
    const { aside } = renderSidebar();

    expect(screen.queryByText('Ma Su Su Hlaing')).not.toBeInTheDocument();
    expect(aside.querySelector('[title="Ma Su Su Hlaing"]')).not.toBeNull();
  });

  it('still renders when localStorage is unavailable', () => {
    // Safari private mode throws on access outright. A nav rail that cannot render because a
    // width preference could not be read would be a very poor trade.
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('SecurityError');
    });
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('SecurityError');
    });

    const { aside } = renderSidebar();

    expect(aside.className).toContain('w-[224px]');

    // And toggling still works in-session, even though it cannot be saved.
    fireEvent.click(screen.getByRole('button', { name: /collapse sidebar/i }));
    expect(aside.className).toContain('w-16');
  });
});
