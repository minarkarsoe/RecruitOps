import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { CandidateSlideOver } from '../CandidateSlideOver';
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
  id: 'app-999',
  candidateId: 'cand-888',
  candidateName: 'May Thu',
  email: 'may.thu@example.com',
  phone: '+95 911223344',
  status: 'Screening' as const,
  source: 'LinkedIn' as const,
  appliedAt: '2026-08-01T10:00:00Z',
  coverNote: 'Experienced full stack developer with React and C# expertise.',
  customFieldsJson: null,
};

const mockMatchAnalysis: CandidateMatchAnalysis = {
  candidateId: 'cand-888',
  jobPostingId: 'job-777',
  overallScore: 85,
  recommendation: 'StrongMatch',
  strengths: ['8+ years experience in C# / .NET Core', 'Strong React / TypeScript frontend skills'],
  gaps: ['Limited experience with GraphQL API design'],
  criteria: [
    { criterion: 'Technical Skills', score: 90, rationale: 'Excellent match for C# and React stack.' },
    { criterion: 'Years of Experience', score: 85, rationale: 'Exceeds the 5 years requirement.' },
  ],
  suggestedInterviewQuestions: [
    'How do you approach optimizing React rendering performance in large applications?',
    'Can you describe your experience with asynchronous messaging architectures?',
  ],
  summary: 'May Thu is a strong candidate with relevant experience in full-stack web application development.',
};

// The API's real shape (`ExecutiveSummaryDto`), corrected 2026-08-28. This fixture used to
// carry `candidateId`, `summary`, `keyStrengths`, `suggestedInterviewQuestions` and
// `isBilingual` — none of which the endpoint returns.
const mockExecSummary: ExecutiveSummaryResult = {
  headline: 'Senior Full Stack Engineer with 8+ Years Experience',
  executiveSummary: 'May Thu is a high-performing senior developer with extensive domain expertise in recruitment platforms.',
  keyHighlights: ['Full Stack proficiency (C#, React)', 'Proven track record of team leadership'],
  recommendedInterviewQuestions: ['Describe a complex architectural trade-off you recently made.'],
};

describe('Candidate 360 AI Smart Match & Executive Summary UI Tests', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('1. Renders Smart Match Badge in header and detailed criteria breakdown drawer panel', async () => {
    vi.mocked(aiApi.matchCandidate).mockResolvedValueOnce(mockMatchAnalysis);

    render(
      <CandidateSlideOver
        candidate={mockCandidate}
        jobPostingId="job-777"
        isOpen={true}
        onClose={vi.fn()}
        initialTab="ai"
      />
    );

    await waitFor(() => {
      expect(aiApi.matchCandidate).toHaveBeenCalledWith({
        candidateId: 'cand-888',
        jobPostingId: 'job-777',
      });
      expect(screen.getByText('85% Match (Strong Match)')).toBeInTheDocument();
      expect(screen.getByText('AI Smart Match Analysis')).toBeInTheDocument();
      expect(screen.getByText('Technical Skills')).toBeInTheDocument();
      expect(screen.getByText('8+ years experience in C# / .NET Core')).toBeInTheDocument();
      expect(screen.getByText('Limited experience with GraphQL API design')).toBeInTheDocument();
      expect(
        screen.getByText('How do you approach optimizing React rendering performance in large applications?')
      ).toBeInTheDocument();
    });
  });

  // ⚠️ REWRITTEN 2026-08-28. This case asserted that switching to MY sent `language: 'my'`
  // alongside `audience: 'internal'`, and that a "Burmese Enabled" badge appeared. The API's
  // request record is (CandidateId, JobPostingId, Tone) and its response carries no
  // `isBilingual`, so all three assertions described a contract that did not exist — the mock
  // simply agreed with them. `audience` is deleted (ADR-0001); `language` is a control the
  // request does not yet carry.
  it('2. Generates an Executive Summary and renders the API response through the slide-over', async () => {
    const user = userEvent.setup();
    vi.mocked(aiApi.generateExecutiveSummary).mockResolvedValueOnce(mockExecSummary);

    render(
      <CandidateSlideOver
        candidate={mockCandidate}
        jobPostingId="job-777"
        isOpen={true}
        onClose={vi.fn()}
        initialTab="ai"
      />
    );

    await user.click(screen.getByRole('button', { name: /Generate AI Summary/i }));

    await waitFor(() => {
      expect(aiApi.generateExecutiveSummary).toHaveBeenCalledWith({
        candidateId: 'cand-888',
        jobPostingId: 'job-777',
      });
      expect(screen.getByText(mockExecSummary.headline)).toBeInTheDocument();
      // Under the old field names this was `undefined` and the body rendered blank.
      expect(screen.getByText(mockExecSummary.executiveSummary)).toBeInTheDocument();
    });

    // Selecting Burmese still re-requests, and the panel renders whatever comes back — but the
    // choice is not on the wire, which is the gap this pins.
    vi.mocked(aiApi.generateExecutiveSummary).mockResolvedValueOnce({
      ...mockExecSummary,
      executiveSummary: 'မေသူသည် ပရိုဂရမ်းမင်းကျွမ်းကျင်သော စီနီယာအင်ဂျင်နီယာတစ်ဦးဖြစ်သည်။',
    });

    await user.click(screen.getByRole('button', { name: /MY \(Burmese\)/i }));
    await user.click(screen.getByRole('button', { name: /Generate AI Summary/i }));

    await waitFor(() => {
      const sent = vi.mocked(aiApi.generateExecutiveSummary).mock.calls.at(-1)![0];
      expect(sent).not.toHaveProperty('language');
      expect(sent).not.toHaveProperty('audience');
      expect(screen.getByText('မေသူသည် ပရိုဂရမ်းမင်းကျွမ်းကျင်သော စီနီယာအင်ဂျင်နီယာတစ်ဦးဖြစ်သည်။')).toBeInTheDocument();
    });
  });

  it('3. Displays animated skeleton loading state during pending async requests', async () => {
    let resolveMatch: (val: any) => void = () => {};
    const matchPromise = new Promise((resolve) => {
      resolveMatch = resolve;
    });
    vi.mocked(aiApi.matchCandidate).mockImplementationOnce(() => matchPromise as any);

    let resolveSummary: (val: any) => void = () => {};
    const summaryPromise = new Promise((resolve) => {
      resolveSummary = resolve;
    });
    vi.mocked(aiApi.generateExecutiveSummary).mockImplementationOnce(() => summaryPromise as any);

    render(
      <CandidateSlideOver
        candidate={mockCandidate}
        jobPostingId="job-777"
        isOpen={true}
        onClose={vi.fn()}
        initialTab="ai"
      />
    );

    // Verify Smart Match loading skeleton is visible
    expect(screen.getByTestId('smart-match-skeleton')).toBeInTheDocument();

    const user = userEvent.setup();
    const generateBtn = screen.getByRole('button', { name: /Generate AI Summary/i });
    await user.click(generateBtn);

    // Verify Executive Summary loading skeleton is visible
    expect(screen.getByTestId('executive-summary-skeleton')).toBeInTheDocument();

    // Resolve promises
    resolveMatch(mockMatchAnalysis);
    resolveSummary(mockExecSummary);

    await waitFor(() => {
      expect(screen.queryByTestId('smart-match-skeleton')).not.toBeInTheDocument();
      expect(screen.queryByTestId('executive-summary-skeleton')).not.toBeInTheDocument();
    });
  });

  it('4. Gracefully handles HTTP 402 Payment Required (Unconfigured API Key) without crashing UI', async () => {
    vi.mocked(aiApi.matchCandidate).mockRejectedValueOnce(new ApiError(402, 'Claude API key is unconfigured.'));
    vi.mocked(aiApi.generateExecutiveSummary).mockRejectedValueOnce(new ApiError(402, 'Gemini API key is unconfigured.'));

    render(
      <CandidateSlideOver
        candidate={mockCandidate}
        jobPostingId="job-777"
        isOpen={true}
        onClose={vi.fn()}
        initialTab="ai"
      />
    );

    await waitFor(() => {
      expect(screen.getByTestId('smart-match-402-banner')).toBeInTheDocument();
      expect(screen.getByText(/AI Features Unconfigured: API key required/i)).toBeInTheDocument();
      expect(screen.getByText(/An AI Provider API Key \(Claude\) has not been configured/i)).toBeInTheDocument();
    });

    const user = userEvent.setup();
    const generateBtn = screen.getByRole('button', { name: /Generate AI Summary/i });
    await user.click(generateBtn);

    await waitFor(() => {
      expect(screen.getByTestId('executive-summary-402-banner')).toBeInTheDocument();
      expect(screen.getByText(/An AI Provider API Key \(Gemini\) has not been configured/i)).toBeInTheDocument();
    });

    // EMPIRICAL ASSERTION: Candidate 360 SlideOver drawer UI does NOT crash and candidate details are still accessible
    expect(screen.getByText('May Thu')).toBeInTheDocument();
    expect(screen.getByText(/may.thu@example.com/i)).toBeInTheDocument();
  });

  it('5. Handles API errors and provides a retry mechanism', async () => {
    const user = userEvent.setup();
    vi.mocked(aiApi.matchCandidate).mockResolvedValueOnce(mockMatchAnalysis);
    vi.mocked(aiApi.generateExecutiveSummary)
      .mockRejectedValueOnce(new ApiError(500, 'Internal Server Error during summary generation.'))
      .mockResolvedValueOnce(mockExecSummary);

    render(
      <CandidateSlideOver
        candidate={mockCandidate}
        jobPostingId="job-777"
        isOpen={true}
        onClose={vi.fn()}
        initialTab="ai"
      />
    );

    const generateBtn = screen.getByRole('button', { name: /Generate AI Summary/i });
    await user.click(generateBtn);

    await waitFor(() => {
      expect(screen.getByText(/Internal Server Error during summary generation/i)).toBeInTheDocument();
    });

    // Click Retry
    const retryBtn = screen.getByRole('button', { name: /Retry/i });
    await user.click(retryBtn);

    await waitFor(() => {
      expect(aiApi.generateExecutiveSummary).toHaveBeenCalledTimes(2);
      expect(screen.getByText('Senior Full Stack Engineer with 8+ Years Experience')).toBeInTheDocument();
    });
  });

  it('6. Supports copying summary text to clipboard and exporting markdown document', async () => {
    const user = userEvent.setup();
    vi.mocked(aiApi.matchCandidate).mockResolvedValueOnce(mockMatchAnalysis);
    vi.mocked(aiApi.generateExecutiveSummary).mockResolvedValueOnce(mockExecSummary);

    // Mock clipboard and object URL
    const writeTextMock = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, 'clipboard', {
      value: { writeText: writeTextMock },
      writable: true,
      configurable: true,
    });
    window.URL.createObjectURL = vi.fn().mockReturnValue('blob:http://localhost/summary');
    window.URL.revokeObjectURL = vi.fn();

    render(
      <CandidateSlideOver
        candidate={mockCandidate}
        jobPostingId="job-777"
        isOpen={true}
        onClose={vi.fn()}
        initialTab="ai"
      />
    );

    const generateBtn = screen.getByRole('button', { name: /Generate AI Summary/i });
    await user.click(generateBtn);

    await waitFor(() => {
      expect(screen.getByText('Senior Full Stack Engineer with 8+ Years Experience')).toBeInTheDocument();
    });

    // Test Copy Summary button
    const copyBtn = screen.getByRole('button', { name: /Copy Summary/i });
    await user.click(copyBtn);

    expect(writeTextMock).toHaveBeenCalled();
    expect(screen.getByText('Copied!')).toBeInTheDocument();

    // Test Export button
    const exportBtn = screen.getByRole('button', { name: /Export \(\.md\)/i });
    await user.click(exportBtn);

    expect(window.URL.createObjectURL).toHaveBeenCalled();
  });
});
