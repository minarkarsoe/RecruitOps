import { describe, expect, it, beforeEach, vi, afterEach } from 'vitest';
import { render, screen, fireEvent, within, cleanup } from '@testing-library/react';
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

const SHUT_KEY = 'recruitops.sidebar.shutGroups';

function renderSidebar(onSignOut = vi.fn(), route = '/') {
  const { container } = render(
    <MemoryRouter initialEntries={[route]}>
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

  it('puts the toggle in the header, not in a footer row of its own', () => {
    const { aside } = renderSidebar();
    const header = aside.firstElementChild as HTMLElement;
    const toggle = screen.getByRole('button', { name: /collapse sidebar/i });

    // The footer row this replaced cost a full 36px on a control the header had room for, and
    // was enough to push the nav into overflow on a laptop.
    expect(header.contains(toggle)).toBe(true);
    expect(screen.queryByText('Collapse')).not.toBeInTheDocument();
  });
});

/**
 * Parent/child groups (requested 2026-08-28, drawn in `design/internal/components.html`).
 *
 * The headings became buttons that fold their children away. Two rules carry most of the risk:
 * a fold must never hide the page you are currently on, and a group added in a later release
 * must not arrive pre-hidden for existing users.
 */
describe('Sidebar nav groups', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it('opens every group by default', () => {
    renderSidebar();

    for (const title of ['Recruitment', 'Insights', 'Team', 'Governance']) {
      expect(screen.getByRole('button', { name: new RegExp(title, 'i') })).toHaveAttribute(
        'aria-expanded',
        'true'
      );
    }
    expect(screen.getByRole('link', { name: 'Role Builder' })).toBeVisible();
  });

  it('folds a group away when its heading is clicked, and says so', () => {
    renderSidebar();
    const governance = screen.getByRole('button', { name: /governance/i });

    fireEvent.click(governance);

    expect(governance).toHaveAttribute('aria-expanded', 'false');
    expect(screen.queryByRole('link', { name: 'Role Builder' })).not.toBeInTheDocument();
    // Sibling groups are unaffected — folding one is not a mode.
    expect(screen.getByRole('link', { name: 'Requisitions' })).toBeVisible();
  });

  it('persists the SHUT set, so a group added later is open for existing users', () => {
    renderSidebar();

    fireEvent.click(screen.getByRole('button', { name: /team/i }));
    expect(JSON.parse(localStorage.getItem(SHUT_KEY)!)).toEqual(['Team']);

    // Storing the open set instead would mean any group shipped after this preference was
    // saved arrives hidden — from exactly the users least likely to go looking for it.
    localStorage.setItem(SHUT_KEY, JSON.stringify(['Team']));
    cleanup();
    renderSidebar();

    expect(screen.getByRole('button', { name: /team/i })).toHaveAttribute('aria-expanded', 'false');
    expect(screen.getByRole('button', { name: /governance/i })).toHaveAttribute(
      'aria-expanded',
      'true'
    );
  });

  it('refuses to fold the group containing the page you are on', () => {
    // Su Su Hlaing shuts "Team", then navigates to Users, which lives in it. Honouring the fold
    // would hide where she is — the rail would stop answering "where am I".
    localStorage.setItem(SHUT_KEY, JSON.stringify(['Team']));
    renderSidebar(vi.fn(), '/users');

    expect(screen.getByRole('button', { name: /team/i })).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByRole('link', { name: 'Users' })).toBeVisible();
  });

  it('treats a nested route as inside its group', () => {
    localStorage.setItem(SHUT_KEY, JSON.stringify(['Recruitment']));
    renderSidebar(vi.fn(), '/requisitions/8f2c/edit');

    expect(screen.getByRole('button', { name: /recruitment/i })).toHaveAttribute(
      'aria-expanded',
      'true'
    );
  });

  it('ignores a stored group name that no longer exists', () => {
    localStorage.setItem(SHUT_KEY, JSON.stringify(['Clients', 'Team']));
    renderSidebar();

    // `Clients` went with ADR-0001. A stale name must not throw or fold something else.
    expect(screen.getByRole('button', { name: /team/i })).toHaveAttribute('aria-expanded', 'false');
    expect(screen.getByRole('button', { name: /recruitment/i })).toHaveAttribute(
      'aria-expanded',
      'true'
    );
  });

  it('survives a corrupt stored value', () => {
    localStorage.setItem(SHUT_KEY, '{not json');
    renderSidebar();

    expect(screen.getByRole('button', { name: /recruitment/i })).toHaveAttribute(
      'aria-expanded',
      'true'
    );
  });

  it('shows every item and no group buttons when the rail is collapsed', () => {
    // With no readable heading there is nothing to fold, and hiding items behind an invisible
    // parent would strand them. Narrowing the rail is already the compaction.
    localStorage.setItem(COLLAPSE_KEY, 'true');
    localStorage.setItem(SHUT_KEY, JSON.stringify(['Recruitment', 'Team', 'Governance']));
    renderSidebar();

    expect(screen.queryByRole('button', { name: /^recruitment$/i })).not.toBeInTheDocument();
    for (const label of ['Requisitions', 'Users', 'Role Builder', 'Analytics']) {
      expect(screen.getByRole('link', { name: label })).toBeVisible();
    }
  });

  it('wires each heading to the panel it controls', () => {
    const { aside } = renderSidebar();
    const governance = screen.getByRole('button', { name: /governance/i });
    const panelId = governance.getAttribute('aria-controls');

    expect(panelId).toBeTruthy();
    const panel = aside.querySelector(`#${panelId}`);
    expect(panel).not.toBeNull();
    expect(within(panel as HTMLElement).getByRole('link', { name: 'Role Builder' })).toBeVisible();
  });
});
