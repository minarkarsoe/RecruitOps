import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { DepartmentDetail, DepartmentMember } from '@recruitops/types';
import { auth } from '../lib/auth';
import { DepartmentsPage } from './DepartmentsPage';

const apiMock = vi.hoisted(() => vi.fn());
vi.mock('../lib/api', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../lib/api')>()),
  api: apiMock,
}));

const sales: DepartmentDetail = {
  id: 'dep-sales', name: 'Sales', code: 'SAL', isActive: true, memberCount: 1, openRequisitionCount: 0,
};
const members: DepartmentMember[] = [
  { userId: 'u-1', displayName: 'Mya Hiring Manager', email: 'mya@alpha.test', role: 'HiringManager', isMember: true },
];

function signIn(permissions: string[]) {
  sessionStorage.clear();
  auth.set({
    accessToken: 'token', expiresAtUtc: '2099-01-01T00:00:00Z', role: 'HrDirector',
    displayName: 'Viewer', userId: 'usr-1', isSuperAdmin: false, permissions,
  });
}

beforeEach(() => {
  apiMock.mockReset();
  apiMock.mockImplementation(async (path: string) =>
    path.endsWith('/members') ? members : [sales]);
});

// ADR-0022 amendment (2026-10-17): reading departments needs settings:read — the HR Director holds
// it — and every change needs settings:update, which only Admin holds by default. Before, the HR
// Director's menu led here and the page answered 403; a read-only screen must not offer controls
// that can only answer 403 either.
describe('DepartmentsPage', () => {
  it('is read-only without settings:update: the list and the members, no editing controls', async () => {
    signIn(['permission:settings:settings:read']);
    const user = userEvent.setup();
    render(<DepartmentsPage />);

    expect(await screen.findByText('Sales')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'New department' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Rename' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Deactivate' })).not.toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Members' }));
    const box = await screen.findByRole('checkbox', { name: /Mya Hiring Manager/ });
    // Who belongs is visible; changing it is not on offer.
    expect(box).toBeChecked();
    expect(box).toBeDisabled();
    expect(screen.queryByRole('button', { name: 'Save members' })).not.toBeInTheDocument();
  });

  it('offers every control with settings:update', async () => {
    signIn(['permission:settings:settings:read', 'permission:settings:settings:update']);
    const user = userEvent.setup();
    render(<DepartmentsPage />);

    expect(await screen.findByText('Sales')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'New department' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Rename' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Deactivate' })).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Members' }));
    expect(await screen.findByRole('checkbox', { name: /Mya Hiring Manager/ })).toBeEnabled();
    expect(screen.getByRole('button', { name: 'Save members' })).toBeInTheDocument();
  });

  it('shows the role alone when the server withholds the address', async () => {
    // The roster carries emails only for a users:read holder (security review, 2026-10-17, L1).
    apiMock.mockImplementation(async (path: string) =>
      path.endsWith('/members') ? [{ ...members[0], email: null }] : [sales]);
    signIn(['permission:settings:settings:read']);
    const user = userEvent.setup();
    render(<DepartmentsPage />);

    await user.click(await screen.findByRole('button', { name: 'Members' }));
    const box = await screen.findByRole('checkbox', { name: /Mya Hiring Manager/ });
    const label = box.closest('label')!;
    expect(label).toHaveTextContent(/HiringManager$/);
    expect(label).not.toHaveTextContent(/null|·/);
  });

  it('keeps the address beside the role for a directory reader', async () => {
    signIn(['permission:settings:settings:read', 'permission:users:users:read']);
    const user = userEvent.setup();
    render(<DepartmentsPage />);

    await user.click(await screen.findByRole('button', { name: 'Members' }));
    const box = await screen.findByRole('checkbox', { name: /Mya Hiring Manager/ });
    expect(box.closest('label')!).toHaveTextContent('HiringManager · mya@alpha.test');
  });
});
