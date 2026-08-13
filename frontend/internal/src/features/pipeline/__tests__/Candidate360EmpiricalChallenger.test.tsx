import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { CandidateSlideOver } from '../CandidateSlideOver';
import { SmartMatchBreakdown, getMatchBadgeConfig } from '../SmartMatchBreakdown';
import { ExecutiveSummaryPanel } from '../ExecutiveSummaryPanel';
import { aiApi, ApiError } from '../../../lib/api';
import type { CandidateMatchAnalysis, ExecutiveSummaryResult } from '@recruitops/types';

export const mockCandidate = {
  id: 'app-100',
  candidateId: 'cand-100',
  candidateName: 'Aung Kyaw',
  email: 'aung.kyaw@example.com',
  phone: '+95 912345678',
  status: 'New' as const,
  source: 'Referral' as const,
  appliedAt: '2026-08-10T12:00:00Z',
  coverNote: 'Interested in senior role.',
  customFieldsJson: null,
};

vi.mock('../../../lib/api', async () => {
  const actual = await vi.importActual<typeof import('../../../lib/api')>('../../../lib/api');
  return {
    ...actual,
    aiApi: {
      parseResume: vi.fn(),
      matchCandidate: vi.fn(),
      generateExecutiveSummary: vi.fn(),
      prepareDocument: vi.fn(),
      translateBurmese: vi.fn(),
    },
  };
});



describe('Candidate 360 Challenger Stress Test Suite', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('1. getMatchBadgeConfig Boundary Scoring Unit Tests', () => {
    it('evaluates recommendation priorities and exact boundary scores (80, 60, 40)', () => {
      expect(getMatchBadgeConfig('StrongMatch', 100)).toEqual({ variant: 'success', label: 'Strong Match' });
      expect(getMatchBadgeConfig('StrongMatch', 80)).toEqual({ variant: 'success', label: 'Strong Match' });
      expect(getMatchBadgeConfig(undefined, 80)).toEqual({ variant: 'success', label: '80% Match' });
      expect(getMatchBadgeConfig(undefined, 79)).toEqual({ variant: 'primary', label: '79% Match' });
      expect(getMatchBadgeConfig(undefined, 60)).toEqual({ variant: 'primary', label: '60% Match' });
      expect(getMatchBadgeConfig(undefined, 59)).toEqual({ variant: 'warning', label: '59% Match' });
      expect(getMatchBadgeConfig(undefined, 40)).toEqual({ variant: 'warning', label: '40% Match' });
      expect(getMatchBadgeConfig(undefined, 39)).toEqual({ variant: 'danger', label: '39% Match' });
      expect(getMatchBadgeConfig(undefined, 0)).toEqual({ variant: 'danger', label: '0% Match' });
      expect(getMatchBadgeConfig('LowMatch', 35)).toEqual({ variant: 'danger', label: 'Low Match' });
    });
  });

  describe('2. Empty Contexts & Null / Undefined Properties Stress Tests', () => {
    it('renders CandidateSlideOver when candidate prop is null without throwing errors', () => {
      render(<CandidateSlideOver candidate={null} isOpen={true} onClose={vi.fn()} />);
      expect(screen.getByText('No candidate profile selected.')).toBeInTheDocument();
    });

    it('renders SmartMatchBreakdown when jobPostingId is undefined', async () => {
      render(<SmartMatchBreakdown candidateId="cand-100" jobPostingId={undefined} />);
      expect(screen.getByText('No job posting selected for match analysis.')).toBeInTheDocument();
      const button = screen.getByRole('button', { name: /Analyze Fit/i });
      expect(button).toBeDisabled();
      expect(aiApi.matchCandidate).not.toHaveBeenCalled();
    });

    it('handles empty strengths, gaps, criteria, and interview questions in SmartMatchBreakdown', async () => {
      const emptyAnalysis: CandidateMatchAnalysis = {
        candidateId: 'cand-100',
        jobPostingId: 'job-500',
        overallScore: 50,
        recommendation: 'PossibleMatch',
        strengths: [],
        gaps: [],
        criteria: [],
        suggestedInterviewQuestions: [],
        summary: 'Moderate fit candidate.',
      };

      vi.mocked(aiApi.matchCandidate).mockResolvedValueOnce(emptyAnalysis);

      render(<SmartMatchBreakdown candidateId="cand-100" jobPostingId="job-500" />);

      await waitFor(() => {
        expect(screen.getByText('50% Match (Possible Match)')).toBeInTheDocument();
        expect(screen.getByText('No specific strengths identified.')).toBeInTheDocument();
        expect(screen.getByText('No critical gaps identified.')).toBeInTheDocument();
        expect(screen.getByText('Moderate fit candidate.')).toBeInTheDocument();
      });
    });

    it('handles undefined optional arrays in ExecutiveSummaryPanel without crashing', async () => {
      const minimalSummary: ExecutiveSummaryResult = {
        candidateId: 'cand-100',
        headline: 'Software Developer Profile',
        summary: 'Solid background in application development.',
        keyStrengths: undefined as any,
        suggestedInterviewQuestions: undefined as any,
        isBilingual: false,
      };

      const user = userEvent.setup();
      vi.mocked(aiApi.generateExecutiveSummary).mockResolvedValueOnce(minimalSummary);

      render(<ExecutiveSummaryPanel candidateId="cand-100" jobPostingId="job-500" candidateName="Aung Kyaw" />);

      const generateBtn = screen.getByRole('button', { name: /Generate AI Summary/i });
      await user.click(generateBtn);

      await waitFor(() => {
        expect(screen.getByText('Software Developer Profile')).toBeInTheDocument();
        expect(screen.getByText('Solid background in application development.')).toBeInTheDocument();
      });

      // Verify Copy Summary does not crash when strengths/questions are undefined
      const writeTextMock = vi.fn().mockResolvedValue(undefined);
      Object.defineProperty(navigator, 'clipboard', {
        value: { writeText: writeTextMock },
        writable: true,
        configurable: true,
      });

      const copyBtn = screen.getByRole('button', { name: /Copy Summary/i });
      await user.click(copyBtn);
      expect(writeTextMock).toHaveBeenCalledWith(
        expect.stringContaining('Headline: Software Developer Profile')
      );
    });
  });

  describe('3. Skeleton Loaders & Error Fallback Stress Tests', () => {
    it('shows general non-402 error box and supports retry on 500 error', async () => {
      const user = userEvent.setup();
      vi.mocked(aiApi.matchCandidate)
        .mockRejectedValueOnce(new ApiError(500, 'Server overloaded'))
        .mockResolvedValueOnce({
          candidateId: 'cand-100',
          jobPostingId: 'job-500',
          overallScore: 90,
          recommendation: 'StrongMatch',
          strengths: ['Great experience'],
          gaps: [],
          criteria: [],
          suggestedInterviewQuestions: [],
          summary: 'High fit.',
        });

      render(<SmartMatchBreakdown candidateId="cand-100" jobPostingId="job-500" />);

      await waitFor(() => {
        expect(screen.getByText('Server overloaded')).toBeInTheDocument();
      });

      const retryBtn = screen.getByRole('button', { name: /Retry/i });
      await user.click(retryBtn);

      await waitFor(() => {
        expect(screen.getByText('90% Match (Strong Match)')).toBeInTheDocument();
      });
    });
  });
});
