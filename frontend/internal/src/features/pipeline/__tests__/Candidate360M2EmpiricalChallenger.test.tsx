import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { CandidateSlideOver } from '../CandidateSlideOver';
import { SmartMatchBreakdown, getMatchBadgeConfig } from '../SmartMatchBreakdown';
import { ExecutiveSummaryPanel } from '../ExecutiveSummaryPanel';
import { aiApi, ApiError } from '../../../lib/api';
import type { CandidateMatchAnalysis, ExecutiveSummaryResult } from '@recruitops/types';

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

const mockCandidate = {
  id: 'app-m2-test-1',
  candidateId: 'cand-m2-test-1',
  candidateName: 'Thandar Aung',
  email: 'thandar.aung@example.com',
  phone: '+95 9777666555',
  status: 'Screening' as const,
  source: 'LinkedIn' as const,
  appliedAt: '2026-08-10T09:00:00Z',
  coverNote: 'Experienced Lead Backend Engineer',
  customFieldsJson: null,
};

const mockMatchAnalysis: CandidateMatchAnalysis = {
  candidateId: 'cand-m2-test-1',
  jobPostingId: 'job-101',
  overallScore: 88,
  recommendation: 'StrongMatch',
  strengths: ['10+ years C# / .NET architecture experience', 'System design expertise'],
  gaps: ['No direct Go experience'],
  criteria: [
    { criterion: 'Backend Engineering', score: 95, rationale: 'Senior level C# expertise.' },
    { criterion: 'Cloud Infrastructure', score: 80, rationale: 'Solid AWS experience.' },
  ],
  suggestedInterviewQuestions: [
    'How do you approach refactoring monoliths into distributed microservices?',
  ],
  summary: 'Thandar Aung is an exceptional candidate for the Lead Architect position.',
};

const mockExecSummary: ExecutiveSummaryResult = {
  candidateId: 'cand-m2-test-1',
  headline: 'Lead Software Architect with 10+ Years Experience',
  summary: 'Thandar Aung has demonstrated deep expertise in enterprise systems.',
  keyStrengths: ['Enterprise C# / .NET Architecture', 'Technical Team Leadership'],
  suggestedInterviewQuestions: ['Describe a time you solved a distributed data consistency problem.'],
  isBilingual: false,
};

describe('Candidate 360 M2 Empirical Challenge Suite', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('1. Smart Match Badge Score Calculations & Color-Coding', () => {
    it('correctly maps recommendation types to badge variants and labels', () => {
      expect(getMatchBadgeConfig('StrongMatch', 85)).toEqual({ variant: 'success', label: 'Strong Match' });
      expect(getMatchBadgeConfig('GoodMatch', 65)).toEqual({ variant: 'primary', label: 'Good Match' });
      expect(getMatchBadgeConfig('PossibleMatch', 45)).toEqual({ variant: 'warning', label: 'Possible Match' });
      expect(getMatchBadgeConfig('LowMatch', 25)).toEqual({ variant: 'danger', label: 'Low Match' });
    });

    it('fallback score thresholds when recommendation is undefined', () => {
      expect(getMatchBadgeConfig(undefined, 90)).toEqual({ variant: 'success', label: '90% Match' });
      expect(getMatchBadgeConfig(undefined, 70)).toEqual({ variant: 'primary', label: '70% Match' });
      expect(getMatchBadgeConfig(undefined, 50)).toEqual({ variant: 'warning', label: '50% Match' });
      expect(getMatchBadgeConfig(undefined, 30)).toEqual({ variant: 'danger', label: '30% Match' });
    });

    it('renders match score badge and criteria breakdown in SmartMatchBreakdown component', async () => {
      vi.mocked(aiApi.matchCandidate).mockResolvedValueOnce(mockMatchAnalysis);

      render(
        <SmartMatchBreakdown
          candidateId="cand-m2-test-1"
          jobPostingId="job-101"
        />
      );

      await waitFor(() => {
        expect(aiApi.matchCandidate).toHaveBeenCalledWith({ candidateId: 'cand-m2-test-1', jobPostingId: 'job-101' });
        expect(screen.getByText(/88% Match \(Strong Match\)/i)).toBeInTheDocument();
        expect(screen.getByText('Backend Engineering')).toBeInTheDocument();
        expect(screen.getByText('95% Match')).toBeInTheDocument();
        expect(screen.getByText('10+ years C# / .NET architecture experience')).toBeInTheDocument();
      });
    });
  });

  describe('2. Executive Summary Panel Language Toggling (en, my, bilingual)', () => {
    it('sends language parameter en when EN language button is selected', async () => {
      const user = userEvent.setup();
      vi.mocked(aiApi.generateExecutiveSummary).mockResolvedValueOnce(mockExecSummary);

      render(
        <ExecutiveSummaryPanel
          candidateId="cand-m2-test-1"
          jobPostingId="job-101"
        />
      );

      const generateBtn = screen.getByRole('button', { name: /Generate AI Summary/i });
      await user.click(generateBtn);

      await waitFor(() => {
        expect(aiApi.generateExecutiveSummary).toHaveBeenCalledWith({
          candidateId: 'cand-m2-test-1',
          jobPostingId: 'job-101',
          audience: 'internal',
          language: 'en',
        });
      });
    });

    it('sends language parameter my when MY (Burmese) language button is selected', async () => {
      const user = userEvent.setup();
      vi.mocked(aiApi.generateExecutiveSummary).mockResolvedValueOnce({
        ...mockExecSummary,
        summary: 'သန္တာအောင်သည် အတွေ့အကြုံရှိသော ဆော့ဖ်ဝဲအင်ဂျင်နီယာဖြစ်သည်။',
        isBilingual: true,
      });

      render(
        <ExecutiveSummaryPanel
          candidateId="cand-m2-test-1"
          jobPostingId="job-101"
        />
      );

      const myBtn = screen.getByRole('button', { name: /MY \(Burmese\)/i });
      await user.click(myBtn);

      const generateBtn = screen.getByRole('button', { name: /Generate AI Summary/i });
      await user.click(generateBtn);

      await waitFor(() => {
        expect(aiApi.generateExecutiveSummary).toHaveBeenCalledWith({
          candidateId: 'cand-m2-test-1',
          jobPostingId: 'job-101',
          audience: 'internal',
          language: 'my',
        });
        expect(screen.getByText('Burmese Enabled')).toBeInTheDocument();
        expect(screen.getByText('သန္တာအောင်သည် အတွေ့အကြုံရှိသော ဆော့ဖ်ဝဲအင်ဂျင်နီယာဖြစ်သည်။')).toBeInTheDocument();
      });
    });

    it('sends language parameter bilingual when Bilingual button is selected', async () => {
      const user = userEvent.setup();
      vi.mocked(aiApi.generateExecutiveSummary).mockResolvedValueOnce({
        ...mockExecSummary,
        isBilingual: true,
      });

      render(
        <ExecutiveSummaryPanel
          candidateId="cand-m2-test-1"
          jobPostingId="job-101"
        />
      );

      const bilingualBtn = screen.getByRole('button', { name: /Bilingual/i });
      await user.click(bilingualBtn);

      const generateBtn = screen.getByRole('button', { name: /Generate AI Summary/i });
      await user.click(generateBtn);

      await waitFor(() => {
        expect(aiApi.generateExecutiveSummary).toHaveBeenCalledWith({
          candidateId: 'cand-m2-test-1',
          jobPostingId: 'job-101',
          audience: 'internal',
          language: 'bilingual',
        });
        expect(screen.getByText('Burmese Enabled')).toBeInTheDocument();
      });
    });
  });

  describe('3. Copy to Clipboard and Export Markdown Actions', () => {
    it('copies formatted summary to clipboard and shows temporary feedback', async () => {
      const user = userEvent.setup();
      vi.mocked(aiApi.generateExecutiveSummary).mockResolvedValueOnce(mockExecSummary);

      const writeTextMock = vi.fn().mockResolvedValue(undefined);
      Object.defineProperty(navigator, 'clipboard', {
        value: { writeText: writeTextMock },
        writable: true,
        configurable: true,
      });

      render(
        <ExecutiveSummaryPanel
          candidateId="cand-m2-test-1"
          candidateName="Thandar Aung"
        />
      );

      await user.click(screen.getByRole('button', { name: /Generate AI Summary/i }));

      await waitFor(() => {
        expect(screen.getByText('Lead Software Architect with 10+ Years Experience')).toBeInTheDocument();
      });

      const copyBtn = screen.getByRole('button', { name: /Copy Summary/i });
      await user.click(copyBtn);

      expect(writeTextMock).toHaveBeenCalledTimes(1);
      const copiedString = writeTextMock.mock.calls[0][0];
      expect(copiedString).toContain('# Executive Summary: Thandar Aung');
      expect(copiedString).toContain('Headline: Lead Software Architect with 10+ Years Experience');

      expect(screen.getByText('Copied!')).toBeInTheDocument();
    });

    it('exports summary as a downloadable markdown (.md) file', async () => {
      const user = userEvent.setup();
      vi.mocked(aiApi.generateExecutiveSummary).mockResolvedValueOnce(mockExecSummary);

      const createObjectURLMock = vi.fn().mockReturnValue('blob:http://localhost/test-blob');
      const revokeObjectURLMock = vi.fn();
      window.URL.createObjectURL = createObjectURLMock;
      window.URL.revokeObjectURL = revokeObjectURLMock;

      render(
        <ExecutiveSummaryPanel
          candidateId="cand-m2-test-1"
          candidateName="Thandar Aung"
        />
      );

      await user.click(screen.getByRole('button', { name: /Generate AI Summary/i }));

      await waitFor(() => {
        expect(screen.getByText('Lead Software Architect with 10+ Years Experience')).toBeInTheDocument();
      });

      const exportBtn = screen.getByRole('button', { name: /Export \(\.md\)/i });
      await user.click(exportBtn);

      expect(createObjectURLMock).toHaveBeenCalledTimes(1);
      const blobArg = createObjectURLMock.mock.calls[0][0];
      expect(blobArg).toBeInstanceOf(Blob);
      expect(blobArg.type).toContain('text/markdown');
      expect(revokeObjectURLMock).toHaveBeenCalledTimes(1);
    });
  });

  describe('4. 402 API Key Unconfigured Graceful Alert Banner Behavior', () => {
    it('renders 402 alert banner in Smart Match without crashing', async () => {
      vi.mocked(aiApi.matchCandidate).mockRejectedValueOnce(new ApiError(402, 'Claude API key is unconfigured.'));

      render(
        <SmartMatchBreakdown
          candidateId="cand-m2-test-1"
          jobPostingId="job-101"
        />
      );

      await waitFor(() => {
        expect(screen.getByTestId('smart-match-402-banner')).toBeInTheDocument();
        expect(screen.getByText('AI Features Unconfigured: API key required')).toBeInTheDocument();
        expect(screen.getByText(/An AI Provider API Key \(Claude\) has not been configured/i)).toBeInTheDocument();
      });
    });

    it('renders 402 alert banner in Executive Summary without crashing', async () => {
      const user = userEvent.setup();
      vi.mocked(aiApi.generateExecutiveSummary).mockRejectedValueOnce(new ApiError(402, 'Gemini API key is unconfigured.'));

      render(
        <ExecutiveSummaryPanel
          candidateId="cand-m2-test-1"
          jobPostingId="job-101"
        />
      );

      await user.click(screen.getByRole('button', { name: /Generate AI Summary/i }));

      await waitFor(() => {
        expect(screen.getByTestId('executive-summary-402-banner')).toBeInTheDocument();
        expect(screen.getByText('AI Features Unconfigured: API key required')).toBeInTheDocument();
        expect(screen.getByText(/An AI Provider API Key \(Gemini\) has not been configured/i)).toBeInTheDocument();
      });
    });

    it('keeps CandidateSlideOver drawer interactive and non-blocked when both AI endpoints return 402', async () => {
      const user = userEvent.setup();
      vi.mocked(aiApi.matchCandidate).mockRejectedValueOnce(new ApiError(402, 'Claude API key unconfigured.'));
      vi.mocked(aiApi.generateExecutiveSummary).mockRejectedValueOnce(new ApiError(402, 'Gemini API key unconfigured.'));

      render(
        <CandidateSlideOver
          candidate={mockCandidate}
          jobPostingId="job-101"
          isOpen={true}
          onClose={vi.fn()}
          initialTab="ai"
        />
      );

      // Verify AI Insights displays 402 banner for Smart Match
      await waitFor(() => {
        expect(screen.getByTestId('smart-match-402-banner')).toBeInTheDocument();
      });

      // Click Generate Summary to trigger 402 in Executive Summary as well
      await user.click(screen.getByRole('button', { name: /Generate AI Summary/i }));

      await waitFor(() => {
        expect(screen.getByTestId('executive-summary-402-banner')).toBeInTheDocument();
      });

      // Switch to Overview tab and verify candidate details are fully operational
      const overviewTab = screen.getByRole('button', { name: /^Overview$/i });
      await user.click(overviewTab);

      expect(screen.getAllByText('Thandar Aung')[0]).toBeInTheDocument();
      expect(screen.getByText('thandar.aung@example.com')).toBeInTheDocument();
      expect(screen.getByText('Experienced Lead Backend Engineer')).toBeInTheDocument();
    });
  });
});
