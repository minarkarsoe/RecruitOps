import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ExecutiveDashboardDto } from '@recruitops/types';
import { ApiError } from '../lib/api';
import { analyticsApi } from '../features/analytics/analyticsApi';
import { ExecutiveDashboardPage, acceptanceDelta, durationDelta, periodName } from './ExecutiveDashboardPage';

vi.mock('../features/analytics/analyticsApi', () => ({
  analyticsApi: {
    getExecutive: vi.fn(),
    getTimeToHire: vi.fn(),
    getConversionFunnel: vi.fn(),
    getSourceOfHire: vi.fn(),
    queryReport: vi.fn(),
    exportReportCsv: vi.fn(),
    downloadReportCsv: vi.fn(),
  },
}));

const SALES = 'd-sales';
const FINANCE = 'd-finance';

/** The API's own fixture numbers (`ExecutiveDashboardTests`), as the SPA receives them. */
const company: ExecutiveDashboardDto = {
  scope: 'Company',
  period: { kind: 'Quarter', from: '2026-07-01', to: '2026-08-18' },
  previous: { kind: 'Quarter', from: '2026-04-01', to: '2026-06-30' },
  timeToHire: { days: 17.5, count: 4, previousDays: 30, previousCount: 1 },
  timeToFill: { days: 40.5, count: 4, previousDays: 14, previousCount: 1 },
  acceptance: { accepted: 4, decided: 6, rate: 66.7, previousAccepted: 1, previousDecided: 2, previousRate: 50 },
  vacancies: [
    { departmentId: FINANCE, departmentName: 'Finance', open: 3 },
    { departmentId: SALES, departmentName: 'Sales', open: 1 },
  ],
  openVacancies: 4,
  departments: [{ id: FINANCE, name: 'Finance' }, { id: SALES, name: 'Sales' }],
  recruiters: [{ id: 'u-admin', name: 'Admin' }, { id: 'u-rec', name: 'Rita Recruiter' }],
};

const tile = (label: string) => screen.getByText(label, { selector: 'p' }).parentElement as HTMLElement;

describe('ExecutiveDashboardPage', () => {
  beforeEach(() => {
    vi.mocked(analyticsApi.getExecutive).mockReset();
    vi.mocked(analyticsApi.getExecutive).mockResolvedValue(company);
  });

  it('draws the three tiles with their definitions and how they moved since the previous quarter', async () => {
    render(<ExecutiveDashboardPage />);

    expect(await screen.findByText('Company-wide')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Executive dashboard' })).toBeInTheDocument();
    expect(screen.getByText(/^Q3 2026 · /)).toBeInTheDocument();

    const hire = tile('Time-to-Hire');
    expect(hire).toHaveTextContent('17.5 days');
    expect(hire).toHaveTextContent('Application received → offer accepted');
    // Fewer days is good news, so it is positive however the number moved.
    expect(within(hire).getByText('12.5 days faster than Q2')).toHaveClass('text-positive-700');

    const fill = tile('Time-to-Fill');
    expect(fill).toHaveTextContent('40.5 days');
    expect(fill).toHaveTextContent('Requisition approved → offer accepted');
    expect(within(fill).getByText('26.5 days slower than Q2')).toHaveClass('text-critical-700');

    const rate = tile('Offer acceptance rate');
    expect(rate).toHaveTextContent('67%');
    expect(rate).toHaveTextContent('4 of 6 offers accepted');
    // 67 − 50, from the rates as shown, not 16.7.
    expect(within(rate).getByText('17 points above Q2')).toHaveClass('text-positive-700');

    expect(analyticsApi.getExecutive).toHaveBeenCalledWith({ period: 'Quarter', departmentId: undefined, recruiterId: undefined });
  });

  it('shows vacancies as bars, and as a table with each department\'s share', async () => {
    const user = userEvent.setup();
    render(<ExecutiveDashboardPage />);

    const section = (await screen.findByRole('heading', { name: 'Active vacancies by department' })).closest('section') as HTMLElement;
    expect(within(section).getByText('4 open')).toBeInTheDocument();
    expect(within(section).getByRole('img', { name: /horizontal bar chart/ })).toBeInTheDocument();
    expect(within(section).getByTitle('Sales — 1 open vacancy')).toBeInTheDocument();

    await user.click(within(section).getByRole('button', { name: 'View as table' }));
    const rows = within(section).getAllByRole('row');
    expect(rows[1]).toHaveTextContent('Finance375%');
    expect(rows[2]).toHaveTextContent('Sales125%');
    expect(within(section).queryByRole('img')).not.toBeInTheDocument();

    await user.click(within(section).getByRole('button', { name: 'View as chart' }));
    expect(within(section).getByRole('img')).toBeInTheDocument();
  });

  it('asks again for each period, department and recruiter chosen', async () => {
    const user = userEvent.setup();
    render(<ExecutiveDashboardPage />);
    await screen.findByText('Company-wide');

    expect(screen.getByRole('button', { name: 'Quarter' })).toHaveAttribute('aria-pressed', 'true');
    await user.click(screen.getByRole('button', { name: 'Month' }));
    await waitFor(() => expect(analyticsApi.getExecutive).toHaveBeenLastCalledWith(
      { period: 'Month', departmentId: undefined, recruiterId: undefined }));

    await user.selectOptions(screen.getByRole('combobox', { name: 'Department' }), FINANCE);
    await waitFor(() => expect(analyticsApi.getExecutive).toHaveBeenLastCalledWith(
      { period: 'Month', departmentId: FINANCE, recruiterId: undefined }));

    await user.selectOptions(screen.getByRole('combobox', { name: 'Recruiter' }), 'u-rec');
    await waitFor(() => expect(analyticsApi.getExecutive).toHaveBeenLastCalledWith(
      { period: 'Month', departmentId: FINANCE, recruiterId: 'u-rec' }));
  });

  it('gives a recruiter no recruiter filter — their rows are their own', async () => {
    vi.mocked(analyticsApi.getExecutive).mockResolvedValue({ ...company, scope: 'Recruiter', recruiters: [] });
    render(<ExecutiveDashboardPage />);

    expect(await screen.findByText('Offers you made')).toBeInTheDocument();
    expect(screen.getByRole('combobox', { name: 'Department' })).toBeInTheDocument();
    expect(screen.queryByRole('combobox', { name: 'Recruiter' })).not.toBeInTheDocument();
  });

  it('draws a Hiring Manager only the tiles the API sent: Time-to-Fill, and vacancies', async () => {
    vi.mocked(analyticsApi.getExecutive).mockResolvedValue({
      ...company, scope: 'Department', timeToHire: null, acceptance: null, recruiters: [],
      departments: [{ id: SALES, name: 'Sales' }],
      vacancies: [{ departmentId: SALES, departmentName: 'Sales', open: 1 }], openVacancies: 1,
    });
    render(<ExecutiveDashboardPage />);

    expect(await screen.findByText('Your departments')).toBeInTheDocument();
    expect(screen.getByText('Time-to-Fill')).toBeInTheDocument();
    expect(screen.queryByText('Time-to-Hire')).not.toBeInTheDocument();
    expect(screen.queryByText('Offer acceptance rate')).not.toBeInTheDocument();
    expect(screen.getByText('1 open')).toBeInTheDocument();
  });

  it('never shows a 0 for a period with nothing in it', async () => {
    vi.mocked(analyticsApi.getExecutive).mockResolvedValue({
      ...company,
      timeToHire: { days: null, count: 0, previousDays: 30, previousCount: 1 },
      timeToFill: { days: null, count: 0, previousDays: null, previousCount: 0 },
      acceptance: { accepted: 0, decided: 0, rate: null, previousAccepted: 0, previousDecided: 0, previousRate: null },
      vacancies: [], openVacancies: 0,
    });
    render(<ExecutiveDashboardPage />);
    await screen.findByText('Company-wide');

    const hire = tile('Time-to-Hire');
    expect(hire).toHaveTextContent('No offers accepted');
    expect(hire).toHaveTextContent('30 days in Q2');
    expect(hire).not.toHaveTextContent(/(^|[^\d.])0 days/);
    expect(tile('Time-to-Fill')).toHaveTextContent('No offers accepted');
    const rate = tile('Offer acceptance rate');
    expect(rate).toHaveTextContent('No offers decided');
    expect(rate).not.toHaveTextContent('%');
    expect(screen.getByText(/^No open vacancies/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'View as table' })).not.toBeInTheDocument();
  });

  it('says a 404 is out of reach, and keeps the filters so the choice can be undone', async () => {
    const user = userEvent.setup();
    render(<ExecutiveDashboardPage />);
    await screen.findByText('Company-wide');

    vi.mocked(analyticsApi.getExecutive).mockRejectedValue(new ApiError(404, 'Not Found'));
    await user.selectOptions(screen.getByRole('combobox', { name: 'Department' }), SALES);

    expect(await screen.findByRole('alert')).toHaveTextContent('Nothing on this dashboard is within your reach.');
    expect(screen.queryByText('Time-to-Fill')).not.toBeInTheDocument();
    expect(screen.getByRole('combobox', { name: 'Department' })).toHaveValue(SALES);
  });

  it('ignores a slower answer to an earlier filter', async () => {
    const user = userEvent.setup();
    let answerQuarter: (dto: ExecutiveDashboardDto) => void = () => {};
    vi.mocked(analyticsApi.getExecutive)
      .mockImplementationOnce(() => new Promise((resolve) => { answerQuarter = resolve; }))
      .mockResolvedValueOnce({ ...company, period: { kind: 'Year', from: '2026-01-01', to: '2026-08-18' } });
    render(<ExecutiveDashboardPage />);

    await user.click(screen.getByRole('button', { name: 'Year' }));
    expect(await screen.findByText(/^2026 · /)).toBeInTheDocument();

    answerQuarter(company);
    await new Promise((r) => setTimeout(r, 0));
    expect(screen.queryByText(/^Q3 2026 · /)).not.toBeInTheDocument();
    expect(screen.getByText(/^2026 · /)).toBeInTheDocument();
  });
});

describe('the tiles\' wording', () => {
  it('names the previous period, with its year only when it differs', () => {
    expect(periodName({ kind: 'Quarter', from: '2026-04-01', to: '2026-06-30' }, 2026)).toBe('Q2');
    expect(periodName({ kind: 'Quarter', from: '2025-10-01', to: '2025-12-31' }, 2026)).toBe('Q4 2025');
    expect(periodName({ kind: 'Year', from: '2025-01-01', to: '2025-12-31' }, 2026)).toBe('2025');
    expect(periodName({ kind: 'Month', from: '2026-07-01', to: '2026-07-31' }, 2026)).toMatch(/^July$/);
  });

  it('counts days and points in the singular when there is one', () => {
    expect(durationDelta({ days: 10, count: 1, previousDays: 11, previousCount: 1 }, 'Q2')?.text).toBe('1 day faster than Q2');
    expect(durationDelta({ days: 10, count: 1, previousDays: 10, previousCount: 1 }, 'Q2'))
      .toEqual({ tone: 'same', text: 'Unchanged from Q2' });
    expect(acceptanceDelta({ accepted: 1, decided: 2, rate: 51, previousAccepted: 1, previousDecided: 2, previousRate: 50 }, 'Q2')?.text)
      .toBe('1 point above Q2');
  });

  it('has nothing to compare when neither period has a number', () => {
    expect(durationDelta({ days: null, count: 0, previousDays: null, previousCount: 0 }, 'Q2')).toBeNull();
    expect(durationDelta({ days: 5, count: 1, previousDays: null, previousCount: 0 }, 'Q2')?.text).toBe('No offers accepted in Q2');
    expect(acceptanceDelta({ accepted: 0, decided: 0, rate: null, previousAccepted: 0, previousDecided: 0, previousRate: null }, 'Q2')).toBeNull();
  });
});
