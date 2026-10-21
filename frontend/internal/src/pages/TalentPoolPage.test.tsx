import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { DuplicateCandidate, DuplicatePair } from '@recruitops/types';
import { ApiError } from '../lib/api';
import { auth } from '../lib/auth';
import { afterMerging, TalentPoolPage } from './TalentPoolPage';

const apiMock = vi.hoisted(() => vi.fn());
vi.mock('../lib/api', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../lib/api')>()),
  api: apiMock,
}));

// Module 2.7, drawn in design/internal/talent-pool.html: the "Possible duplicate" row and
// "Review" → "Two records, one person?".

const older: DuplicateCandidate = {
  id: 'cand-older', fullName: 'U Kyaw Swar', email: 'kyawswar90@example.test', phone: '09250000001',
  source: 'Direct', createdAt: '2026-03-04T00:00:00Z',
  applicationCount: 1, interviewCount: 1, hasSubmittedScorecard: true,
};
const newer: DuplicateCandidate = {
  id: 'cand-newer', fullName: 'Kyaw Swar (U)', email: null, phone: '09250000001',
  source: 'ExcelImport', createdAt: '2026-08-17T00:00:00Z',
  applicationCount: 1, interviewCount: 0, hasSubmittedScorecard: false,
};
const pair: DuplicatePair = { first: older, second: newer, matchedOn: 'Phone', matchedValue: '09250000001' };

function signIn(role: string) {
  sessionStorage.clear();
  auth.set({
    accessToken: 'token', expiresAtUtc: '2099-01-01T00:00:00Z', role,
    displayName: 'Viewer', userId: 'usr-1', isSuperAdmin: false, permissions: [],
  });
}

/** Serves the list until a decision is posted, then an empty list — as the server would. */
function serve(decide: (path: string, init?: RequestInit) => unknown = () => ({ keptCandidateId: older.id, applicationsMoved: 1 })) {
  let decided = false;
  apiMock.mockImplementation(async (path: string, init?: RequestInit) => {
    if (path === '/candidates/duplicates') return decided ? [] : [pair];
    const result = decide(path, init);
    decided = true;
    return result;
  });
}

function posted(path: string): unknown {
  const call = apiMock.mock.calls.find(([p]) => p === path);
  expect(call, `expected a POST to ${path}`).toBeDefined();
  const init = call![1] as RequestInit;
  expect(init.method).toBe('POST');
  return JSON.parse(init.body as string);
}

beforeEach(() => {
  apiMock.mockReset();
});

describe('TalentPoolPage', () => {
  it.each(['HiringManager', 'Interviewer', 'Approver'])('answers a %s without asking the API', async (role) => {
    signIn(role);
    render(<TalentPoolPage />);

    expect(screen.getByText(/open to recruitment staff/i)).toBeInTheDocument();
    expect(apiMock).not.toHaveBeenCalled();
  });

  it('lists a flagged pair with the newer record leading, as drawn', async () => {
    signIn('Recruiter');
    serve();
    render(<TalentPoolPage />);

    const row = (await screen.findByText('Kyaw Swar (U)')).closest('tr')!;
    expect(within(row).getByText('Possible duplicate')).toBeInTheDocument();
    expect(within(row).getByText('U Kyaw Swar')).toBeInTheDocument();
    expect(within(row).getByText('Excel import')).toBeInTheDocument();
    expect(within(row).getByText('2 applications between them')).toBeInTheDocument();
  });

  it('says so when nothing is flagged', async () => {
    signIn('Admin');
    apiMock.mockResolvedValue([]);
    render(<TalentPoolPage />);

    expect(await screen.findByText('No possible duplicates')).toBeInTheDocument();
  });

  it('keeps the older record by default and merges the newer into it', async () => {
    signIn('Recruiter');
    serve();
    const user = userEvent.setup();
    render(<TalentPoolPage />);

    await user.click(await screen.findByRole('button', { name: /^Review/ }));
    const dialog = screen.getByRole('dialog');
    expect(within(dialog).getByText(/Matched on phone/)).toBeInTheDocument();
    const keptColumn = within(dialog).getByText('Keep this one').closest('div.px-5')!;
    expect(within(keptColumn as HTMLElement).getByText('U Kyaw Swar')).toBeInTheDocument();
    expect(within(dialog).getByText(/This cannot be undone/)).toBeInTheDocument();

    await user.click(within(dialog).getByRole('button', { name: 'Merge' }));

    expect(posted('/candidates/merge')).toEqual({ keepCandidateId: older.id, mergeCandidateId: newer.id });
    expect(await screen.findByRole('status')).toHaveTextContent('Merged into U Kyaw Swar: 1 application moved.');
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    // Reloaded: the decided pair is gone.
    expect(await screen.findByText('No possible duplicates')).toBeInTheDocument();
  });

  it('lets the reviewer keep the other record instead', async () => {
    signIn('HrDirector');
    serve(() => ({ keptCandidateId: newer.id, applicationsMoved: 1 }));
    const user = userEvent.setup();
    render(<TalentPoolPage />);

    await user.click(await screen.findByRole('button', { name: /^Review/ }));
    await user.click(screen.getByRole('button', { name: 'Keep Kyaw Swar (U) instead' }));
    // The choice is reflected before anything is sent.
    expect(screen.getByRole('button', { name: 'Keep U Kyaw Swar instead' })).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Merge' }));

    expect(posted('/candidates/merge')).toEqual({ keepCandidateId: newer.id, mergeCandidateId: older.id });
  });

  it('remembers "Not the same person" for the pair', async () => {
    signIn('Recruiter');
    serve(() => undefined);
    const user = userEvent.setup();
    render(<TalentPoolPage />);

    await user.click(await screen.findByRole('button', { name: /^Review/ }));
    await user.click(screen.getByRole('button', { name: 'Not the same person' }));

    expect(posted('/candidates/keep-separate')).toEqual({ firstCandidateId: older.id, secondCandidateId: newer.id });
    expect(await screen.findByRole('status')).toHaveTextContent('marked as different people');
    expect(apiMock.mock.calls.some(([p]) => p === '/candidates/merge')).toBe(false);
  });

  it('"Decide later" changes nothing', async () => {
    signIn('Recruiter');
    serve();
    const user = userEvent.setup();
    render(<TalentPoolPage />);

    await user.click(await screen.findByRole('button', { name: /^Review/ }));
    await user.click(screen.getByRole('button', { name: 'Decide later' }));

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(apiMock.mock.calls.every(([p]) => p === '/candidates/duplicates')).toBe(true);
    expect(await screen.findByText('Kyaw Swar (U)')).toBeInTheDocument();
  });

  it('shows a refusal in the dialog and keeps it open', async () => {
    signIn('Recruiter');
    serve(() => {
      throw new ApiError(409, 'One of these records was merged by someone else a moment ago. Reload and review again.');
    });
    const user = userEvent.setup();
    render(<TalentPoolPage />);

    await user.click(await screen.findByRole('button', { name: /^Review/ }));
    await user.click(screen.getByRole('button', { name: 'Merge' }));

    const dialog = screen.getByRole('dialog');
    expect(await within(dialog).findByRole('alert')).toHaveTextContent('merged by someone else');
    expect(within(dialog).getByRole('button', { name: 'Merge' })).toBeEnabled();
  });
});

describe('afterMerging', () => {
  it('fills a blank on the kept record and never overwrites a value', () => {
    const lines = afterMerging(
      { ...newer, email: null, phone: '0911' },
      { ...older, email: 'a@example.test', phone: '0922' },
    );
    expect(lines).toContain('The kept record has no email, so it takes a@example.test');
    expect(lines).toContain("The kept record's phone stays; 0922 is not copied");
    expect(lines[0]).toBe('2 applications on one candidate, each keeping its own stage history, interviews and scorecards');
  });

  it('does not count applications that are not there', () => {
    const none = { applicationCount: 0 };
    expect(afterMerging({ ...older, ...none }, { ...newer, ...none })[0]).toBe('Neither record has an application yet');
    expect(afterMerging({ ...older, ...none }, newer)[0]).toBe(
      '1 application on one candidate, keeping its own stage history, interviews and scorecards');
  });

  it('says nothing about a field both records share', () => {
    const lines = afterMerging(older, { ...newer, email: older.email });
    expect(lines.some((l) => l.includes('email') || l.includes('phone'))).toBe(false);
  });
});
