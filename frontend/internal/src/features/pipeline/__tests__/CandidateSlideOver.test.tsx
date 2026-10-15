import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { CandidateSlideOver } from '../CandidateSlideOver';
import { resumeApi } from '../../../lib/api';

vi.mock('../../../lib/api', async () => {
  const actual = await vi.importActual<typeof import('../../../lib/api')>('../../../lib/api');
  return {
    ...actual,
    resumeApi: {
      uploadCandidateResume: vi.fn(),
      downloadCandidateResume: vi.fn(),
      confirmParsedProfile: vi.fn(),
      postBulkResumes: vi.fn(),
      getBulkResumeStatus: vi.fn(),
    },
  };
});

const mockCandidate = {
  id: 'app-123',
  candidateId: 'cand-456',
  candidateName: 'Jane Smith',
  email: 'jane@example.com',
  phone: '+95 912345678',
  status: 'Screening' as const,
  source: 'LinkedIn' as const,
  appliedAt: '2026-08-05T10:00:00Z',
  coverNote: null,
  customFieldsJson: null,
};

describe('CandidateSlideOver CV Viewer & Parsed Profile UI', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders CV Viewer tab and drag-and-drop upload zone', async () => {
    render(<CandidateSlideOver candidate={mockCandidate} isOpen={true} onClose={vi.fn()} initialTab="cv" />);

    expect(screen.getByRole('button', { name: /CV Viewer/i })).toBeInTheDocument();
    expect(screen.getByText(/Click to upload CV document/i)).toBeInTheDocument();
  });

  it('uploads CV, displays extracted text and Zawgyi normalization badge', async () => {
    const mockExtractionResult = {
      applicationId: 'app-123',
      fileKey: 'resumes/app-123.pdf',
      fileName: 'resume_myanmar.pdf',
      fileSizeBytes: 102450,
      extractedText: 'Mingalarbar John Doe Senior Developer',
      detectedLanguage: 'my',
      isZawgyiNormalized: true,
      parsedContactInfo: {
        candidateName: 'John Doe',
        email: 'john.doe@example.com',
        phone: '+95 998765432',
        yearsOfExperience: 6,
        skills: ['React', 'C#'],
      },
      processedAt: '2026-08-08T12:00:00Z',
    };

    vi.mocked(resumeApi.uploadCandidateResume).mockResolvedValueOnce(mockExtractionResult);

    render(<CandidateSlideOver candidate={mockCandidate} isOpen={true} onClose={vi.fn()} initialTab="cv" />);

    const file = new File(['fake pdf text content'], 'resume_myanmar.pdf', { type: 'application/pdf' });
    const input = screen.getByLabelText(/Click to upload CV document/i);

    fireEvent.change(input, { target: { files: [file] } });

    await waitFor(() => {
      expect(resumeApi.uploadCandidateResume).toHaveBeenCalledWith('app-123', file);
      expect(screen.getByText('Zawgyi → Unicode Normalized')).toBeInTheDocument();
      expect(screen.getByText(/Mingalarbar John Doe Senior Developer/i)).toBeInTheDocument();
    });
  });

  it('edits candidate profile fields and calls confirmParsedProfile upon button click', async () => {
    const user = userEvent.setup();
    vi.mocked(resumeApi.confirmParsedProfile).mockResolvedValueOnce(undefined);

    render(<CandidateSlideOver candidate={mockCandidate} isOpen={true} onClose={vi.fn()} initialTab="cv" />);

    const nameInput = screen.getByPlaceholderText('Candidate Full Name');
    await user.clear(nameInput);
    await user.type(nameInput, 'Jane Updated');

    const confirmBtn = screen.getByRole('button', { name: /Confirm & Apply to Profile/i });
    await user.click(confirmBtn);

    await waitFor(() => {
      expect(resumeApi.confirmParsedProfile).toHaveBeenCalledWith('app-123', expect.objectContaining({
        candidateName: 'Jane Updated',
      }));
      expect(screen.getByText(/Candidate profile updated and confirmed successfully!/i)).toBeInTheDocument();
    });
  });

  it('downloads original CV document when download button is clicked', async () => {
    const user = userEvent.setup();
    const mockBlob = new Blob(['fake content'], { type: 'application/pdf' });
    vi.mocked(resumeApi.downloadCandidateResume).mockResolvedValueOnce(mockBlob);

    // Mock URL methods
    window.URL.createObjectURL = vi.fn().mockReturnValue('blob:http://localhost/fake-blob');
    window.URL.revokeObjectURL = vi.fn();

    render(<CandidateSlideOver candidate={mockCandidate} isOpen={true} onClose={vi.fn()} initialTab="cv" />);

    const downloadBtn = screen.getByRole('button', { name: /Download Original CV Document/i });
    await user.click(downloadBtn);

    await waitFor(() => {
      expect(resumeApi.downloadCandidateResume).toHaveBeenCalledWith('app-123');
    });
  });
});
