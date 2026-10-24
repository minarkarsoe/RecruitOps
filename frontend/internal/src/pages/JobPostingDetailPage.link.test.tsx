import { describe, expect, it, vi, afterEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import type { JobPostingDetail } from '@recruitops/types';
import * as apiModule from '../lib/api';
import { auth } from '../lib/auth';
import { JobPostingDetailPage, advertUrl } from './JobPostingDetailPage';

/**
 * The link a recruiter shares. It was built here as `{host}:3000/jobs/{token}` — right only in the dev
 * compose stack. Production serves `/jobs/` on the SPA's own origin and publishes no port 3000, so every
 * shared link was dead there. The API now sends the address candidates are given.
 */

const live: JobPostingDetail = {
  id: 'post-1',
  requisitionId: 'req-1',
  departmentId: 'dep-1',
  departmentName: 'Credit Risk',
  title: 'Senior Credit Analyst',
  description: 'Assess and monitor credit exposure.',
  location: 'Yangon',
  employmentType: 'FullTime',
  headcount: 2,
  salaryMin: null,
  salaryMax: null,
  showSalary: false,
  status: 'Live',
  publicToken: 'tok-1',
  publicUrl: 'https://careers.example.com/jobs/tok-1',
  postedAt: '2026-08-01T00:00:00Z',
  closedAt: null,
  applicationCount: 0,
  applicationFormFieldsJson: null,
};

describe('advertUrl', () => {
  it('uses the address the API built from the configured public site', () => {
    expect(advertUrl(live, 'https://hr.example.com')).toBe('https://careers.example.com/jobs/tok-1');
  });

  it('falls back to this origin — where production serves /jobs/ — never to a guessed port', () => {
    expect(advertUrl({ publicToken: 'tok-1', publicUrl: null }, 'https://hr.example.com'))
      .toBe('https://hr.example.com/jobs/tok-1');
  });

  it('has nothing to share before publishing', () => {
    expect(advertUrl({ publicToken: null, publicUrl: null }, 'https://hr.example.com')).toBeNull();
  });
});

describe('JobPostingDetailPage — the share link', () => {
  afterEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
  });

  it('shows the API\'s address for a live posting', async () => {
    auth.set({
      accessToken: 't', refreshToken: 'r', expiresAtUtc: '2099-01-01T00:00:00Z',
      refreshTokenExpiresAtUtc: '2099-01-01T00:00:00Z', userId: 'u-1', displayName: 'Ma Su Su Hlaing',
      role: 'Recruiter', permissions: ['permission:postings:postings:read'],
    } as never);
    vi.spyOn(apiModule, 'api').mockImplementation((async (path: string) =>
      path === '/jobpostings/post-1' ? live : []) as unknown as typeof apiModule.api);

    render(
      <MemoryRouter initialEntries={['/jobpostings/post-1']}>
        <Routes>
          <Route path="/jobpostings/:id" element={<JobPostingDetailPage />} />
        </Routes>
      </MemoryRouter>
    );

    expect(await screen.findByText('https://careers.example.com/jobs/tok-1')).toBeInTheDocument();
    expect(screen.queryByText(/:3000/)).not.toBeInTheDocument();
  });
});
