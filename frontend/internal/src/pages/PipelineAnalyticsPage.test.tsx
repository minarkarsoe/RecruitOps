import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { PipelineAnalyticsDto } from '@recruitops/types';
import { ApiError } from '../lib/api';
import { analyticsApi } from '../features/analytics/analyticsApi';
import { PipelineAnalyticsPage } from './PipelineAnalyticsPage';

vi.mock('../features/analytics/analyticsApi', () => ({
  analyticsApi: {
    getExecutive: vi.fn(),
    getPipeline: vi.fn(),
    queryReport: vi.fn(),
    exportReportCsv: vi.fn(),
    downloadReportCsv: vi.fn(),
  },
}));

const TELLER = 'p-teller';
const CREDIT = 'p-credit';

/** The API's own fixture numbers (`PipelineAnalyticsTests`), as the SPA receives them. */
const company: PipelineAnalyticsDto = {
  scope: 'Company',
  window: '90d',
  from: '2026-07-27',
  to: '2026-10-24',
  candidates: 10,
  sources: [
    { channel: 'Facebook', candidates: 3, share: 30, hired: 1, hireRate: 33.3 },
    { channel: 'LinkedIn', candidates: 1, share: 10, hired: 0, hireRate: 0 },
    { channel: 'CareerPage', candidates: 2, share: 20, hired: 0, hireRate: 0 },
    { channel: 'ManualUpload', candidates: 2, share: 20, hired: 1, hireRate: 50 },
    { channel: 'Other', candidates: 2, share: 20, hired: 0, hireRate: 0 },
  ],
  funnel: [
    { band: 'Sourcing', count: 10, dropOff: null },
    { band: 'Screened', count: 5, dropOff: 50 },
    { band: 'Interview', count: 3, dropOff: 40 },
    { band: 'Offered', count: 2, dropOff: 33.3 },
  ],
  leaderboard: [
    { userId: 'u-a', name: 'Alpha Admin', cvsIn: 0, interviews: 1, hires: 2 },
    { userId: 'u-r', name: 'Alpha Recruiter', cvsIn: 2, interviews: 2, hires: 1 },
  ],
  positions: [{ id: CREDIT, name: 'Pipeline Credit' }, { id: TELLER, name: 'Pipeline Teller' }],
};

const section = (name: string) => screen.getByRole('region', { name });

describe('PipelineAnalyticsPage', () => {
  beforeEach(() => {
    vi.mocked(analyticsApi.getPipeline).mockReset();
    vi.mocked(analyticsApi.getPipeline).mockResolvedValue(company);
  });

  it('draws where candidates come from, and which of them get hired, on separate scales', async () => {
    render(<PipelineAnalyticsPage />);

    expect(await screen.findByRole('heading', { name: 'Pipeline & source analytics' })).toBeInTheDocument();
    expect(screen.getByText('Not visible to Hiring Managers')).toBeInTheDocument();
    expect(analyticsApi.getPipeline).toHaveBeenCalledWith({ window: '90d', postingId: undefined, source: undefined });

    const sources = await screen.findByRole('region', { name: 'Where candidates come from' });
    expect(within(sources).getByText('10 candidates')).toBeInTheDocument();
    expect(within(sources).getByRole('img')).toHaveAccessibleName(
      'Share of candidates by source: Facebook 30%, LinkedIn 10%, Career page 20%, Manual upload 20%, Other 20%');
    expect(within(sources).getAllByRole('listitem').map((li) => li.textContent))
      .toEqual(['Facebook30%', 'LinkedIn10%', 'Career page20%', 'Manual upload20%', 'Other20%']);

    // Sorted by how well each channel converts — a different order from the share above.
    const hired = section('Which of them get hired');
    const rows = within(hired).getAllByTitle(/hired$/);
    expect(rows.map((r) => r.getAttribute('title'))).toEqual([
      'Manual upload — 1 of 2 hired', 'Facebook — 1 of 3 hired', 'LinkedIn — 0 of 1 hired',
      'Career page — 0 of 2 hired', 'Other — 0 of 2 hired',
    ]);
    expect(rows[1]).toHaveTextContent('33%');
  });

  it('shows the funnel in four bands with their drop-off, and the mapping beneath it', async () => {
    render(<PipelineAnalyticsPage />);
    const funnel = await screen.findByRole('region', { name: 'Where candidates drop off' });

    expect(within(funnel).getByTitle('Sourcing — 10 candidates')).toHaveTextContent(/^Sourcing10$/);
    // Half or more is red, as the design marks it; less is ink.
    expect(within(funnel).getByText('−50%')).toHaveClass('text-critical-700');
    expect(within(funnel).getByText('−40%')).toHaveClass('text-ink-600');
    expect(within(funnel).getByText('−33%')).toHaveClass('text-ink-600');
    expect(within(funnel).getByText('Last 90 days')).toBeInTheDocument();
    expect(funnel).toHaveTextContent('Screened = Screening + Shortlisted');
    expect(funnel).toHaveTextContent('Rejected has no band');
  });

  it('lists the leaderboard by name, as staff data', async () => {
    render(<PipelineAnalyticsPage />);
    const board = await screen.findByRole('region', { name: 'Recruiter leaderboard' });

    expect(within(board).getByText('Staff performance data')).toBeInTheDocument();
    const rows = within(board).getAllByRole('row').slice(1);
    expect(rows.map((r) => r.textContent)).toEqual(['Alpha Admin012', 'Alpha Recruiter221']);
    expect(board).toHaveTextContent('Listed by name, not ranked.');
  });

  it('draws a Recruiter no leaderboard — the API sends none', async () => {
    vi.mocked(analyticsApi.getPipeline).mockResolvedValue({ ...company, scope: 'Recruiter', leaderboard: null });
    render(<PipelineAnalyticsPage />);

    await screen.findByRole('region', { name: 'Where candidates drop off' });
    expect(screen.queryByRole('region', { name: 'Recruiter leaderboard' })).not.toBeInTheDocument();
  });

  it('asks again for each period, position and source', async () => {
    const user = userEvent.setup();
    render(<PipelineAnalyticsPage />);
    await screen.findByRole('region', { name: 'Where candidates come from' });

    await user.selectOptions(screen.getByRole('combobox', { name: 'Period' }), 'year');
    await waitFor(() => expect(analyticsApi.getPipeline).toHaveBeenLastCalledWith(
      { window: 'year', postingId: undefined, source: undefined }));
    await user.selectOptions(screen.getByRole('combobox', { name: 'Position' }), CREDIT);
    await waitFor(() => expect(analyticsApi.getPipeline).toHaveBeenLastCalledWith(
      { window: 'year', postingId: CREDIT, source: undefined }));
    await user.selectOptions(screen.getByRole('combobox', { name: 'Source' }), 'ManualUpload');
    await waitFor(() => expect(analyticsApi.getPipeline).toHaveBeenLastCalledWith(
      { window: 'year', postingId: CREDIT, source: 'ManualUpload' }));
  });

  // Found on PostgreSQL 2026-10-24: 5 of 11 is 45.45%, the API sends 45.5, and rounding it again showed 46%.
  it('rounds each percentage once, from the counts', async () => {
    vi.mocked(analyticsApi.getPipeline).mockResolvedValue({
      ...company,
      candidates: 11,
      sources: [{ channel: 'CareerPage', candidates: 11, share: 100, hired: 5, hireRate: 45.5 }],
      funnel: [
        { band: 'Sourcing', count: 11, dropOff: null },
        { band: 'Screened', count: 6, dropOff: 45.5 },
        { band: 'Interview', count: 6, dropOff: 0 },
        { band: 'Offered', count: 6, dropOff: 0 },
      ],
    });
    render(<PipelineAnalyticsPage />);

    const hired = await screen.findByRole('region', { name: 'Which of them get hired' });
    expect(within(hired).getByTitle('Career page — 5 of 11 hired')).toHaveTextContent('45%');
    expect(within(section('Where candidates drop off')).getByText('−45%')).toBeInTheDocument();
  });

  it('says so when nothing arrived, rather than drawing empty charts', async () => {
    vi.mocked(analyticsApi.getPipeline).mockResolvedValue({
      ...company,
      candidates: 0,
      sources: company.sources.filter((s) => s.channel !== 'Other')
        .map((s) => ({ ...s, candidates: 0, share: null, hired: 0, hireRate: null })),
      funnel: company.funnel.map((b) => ({ ...b, count: 0, dropOff: null })),
      leaderboard: [],
    });
    render(<PipelineAnalyticsPage />);

    await screen.findByRole('region', { name: 'Where candidates come from' });
    expect(screen.getAllByText('No applications arrived in this period.')).toHaveLength(3);
    expect(screen.queryByRole('img')).not.toBeInTheDocument();
    expect(screen.getByText(/Nobody uploaded a CV/)).toBeInTheDocument();
  });

  it('says a 404 is out of reach, and keeps the filters so the choice can be undone', async () => {
    const user = userEvent.setup();
    render(<PipelineAnalyticsPage />);
    await screen.findByRole('region', { name: 'Where candidates come from' });

    vi.mocked(analyticsApi.getPipeline).mockRejectedValue(new ApiError(404, 'Not Found'));
    await user.selectOptions(screen.getByRole('combobox', { name: 'Position' }), TELLER);

    expect(await screen.findByRole('alert')).toHaveTextContent('Nothing on this page is within your reach.');
    expect(screen.queryByRole('region', { name: 'Where candidates come from' })).not.toBeInTheDocument();
    expect(screen.getByRole('combobox', { name: 'Position' })).toHaveValue(TELLER);
  });

  it('ignores a slower answer to an earlier filter', async () => {
    const user = userEvent.setup();
    let answerFirst: (dto: PipelineAnalyticsDto) => void = () => {};
    vi.mocked(analyticsApi.getPipeline)
      .mockImplementationOnce(() => new Promise((resolve) => { answerFirst = resolve; }))
      .mockResolvedValueOnce({ ...company, window: 'year', candidates: 11 });
    render(<PipelineAnalyticsPage />);

    await user.selectOptions(screen.getByRole('combobox', { name: 'Period' }), 'year');
    expect(await screen.findByText('11 candidates')).toBeInTheDocument();

    answerFirst(company);
    await new Promise((r) => setTimeout(r, 0));
    expect(screen.queryByText('10 candidates')).not.toBeInTheDocument();
  });
});
