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
  id: 'app-challenger-1',
  candidateId: 'cand-challenger-1',
  candidateName: 'Aung Kyaw',
  email: 'aung.kyaw@example.com',
  phone: '+95 9111222333',
  status: 'Screening' as const,
  source: 'LinkedIn' as const,
  appliedAt: '2026-08-08T10:00:00Z',
  coverNote: null,
  customFieldsJson: null,
};

describe('Candidate 360 SlideOver CV Viewer & Human Review Empirical Stress Tests', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('1. Single CV Upload Progress Bar & Error Handling', () => {
    it('renders upload progress bar during active file extraction and displays success result', async () => {
      let resolveUpload: (val: any) => void = () => {};
      const uploadPromise = new Promise((resolve) => {
        resolveUpload = resolve;
      });

      vi.mocked(resumeApi.uploadCandidateResume).mockImplementationOnce(() => uploadPromise as any);

      render(<CandidateSlideOver candidate={mockCandidate} isOpen={true} onClose={vi.fn()} initialTab="cv" />);

      const file = new File(['sample pdf content'], 'cv_sample.pdf', { type: 'application/pdf' });
      const input = screen.getByLabelText(/Click to upload CV document/i);

      fireEvent.change(input, { target: { files: [file] } });

      // Check progress bar is displayed
      expect(screen.getByText(/Uploading and extracting text.../i)).toBeInTheDocument();

      // Resolve the API call
      resolveUpload({
        applicationId: 'app-challenger-1',
        fileKey: 'resumes/app-challenger-1.pdf',
        fileName: 'cv_sample.pdf',
        fileSizeBytes: 2048,
        extractedText: 'Extracted CV text content for Aung Kyaw',
        detectedLanguage: 'en',
        isZawgyiNormalized: false,
        parsedContactInfo: {
          candidateName: 'Aung Kyaw Updated',
          email: 'aung.updated@example.com',
          phone: '+95 99998888',
          yearsOfExperience: 4,
          skills: ['TypeScript', 'React'],
        },
        processedAt: '2026-08-08T12:00:00Z',
      });

      await waitFor(() => {
        expect(screen.queryByText(/Uploading and extracting text.../i)).not.toBeInTheDocument();
        expect(screen.getByText('Extracted CV text content for Aung Kyaw')).toBeInTheDocument();
      });
    });

    it('displays error when uploading a file exceeding 10MB size limit', async () => {
      render(<CandidateSlideOver candidate={mockCandidate} isOpen={true} onClose={vi.fn()} initialTab="cv" />);

      // Create dummy file > 10MB (10 * 1024 * 1024 + 1 bytes)
      const oversizedFile = new File(['a'], 'oversized_cv.pdf', { type: 'application/pdf' });
      Object.defineProperty(oversizedFile, 'size', { value: 10 * 1024 * 1024 + 1 });

      const input = screen.getByLabelText(/Click to upload CV document/i);
      fireEvent.change(input, { target: { files: [oversizedFile] } });

      await waitFor(() => {
        expect(screen.getByText(/File size exceeds maximum limit of 10MB./i)).toBeInTheDocument();
        expect(resumeApi.uploadCandidateResume).not.toHaveBeenCalled();
      });
    });

    it('displays error when uploading a file with an invalid file format extension', async () => {
      render(<CandidateSlideOver candidate={mockCandidate} isOpen={true} onClose={vi.fn()} initialTab="cv" />);

      const invalidFile = new File(['executable content'], 'malicious.exe', { type: 'application/x-msdownload' });

      const input = screen.getByLabelText(/Click to upload CV document/i);
      fireEvent.change(input, { target: { files: [invalidFile] } });

      await waitFor(() => {
        expect(
          screen.getByText(/Allowed formats: PDF, DOCX\. Scans and photos cannot be read yet\./i)
        ).toBeInTheDocument();
        expect(resumeApi.uploadCandidateResume).not.toHaveBeenCalled();
      });
    });

    it('handles network / API rejection during upload and displays error message', async () => {
      vi.mocked(resumeApi.uploadCandidateResume).mockRejectedValueOnce(new Error('Server storage quota exceeded'));

      render(<CandidateSlideOver candidate={mockCandidate} isOpen={true} onClose={vi.fn()} initialTab="cv" />);

      const file = new File(['valid pdf content'], 'cv_test.pdf', { type: 'application/pdf' });
      const input = screen.getByLabelText(/Click to upload CV document/i);

      fireEvent.change(input, { target: { files: [file] } });

      await waitFor(() => {
        expect(screen.getByText('Server storage quota exceeded')).toBeInTheDocument();
      });
    });
  });

  describe('2. Parsed Profile Editing & Explicit Recruiter Confirmation Requirement', () => {
    it('allows editing Name, Email, Phone, Experience, Skills without triggering API until explicit button click', async () => {
      const user = userEvent.setup();
      vi.mocked(resumeApi.confirmParsedProfile).mockResolvedValueOnce(undefined);

      render(<CandidateSlideOver candidate={mockCandidate} isOpen={true} onClose={vi.fn()} initialTab="cv" />);

      const nameInput = screen.getByPlaceholderText('Candidate Full Name');
      const emailInput = screen.getByPlaceholderText('candidate@example.com');
      const phoneInput = screen.getByPlaceholderText('+95 9...');
      const expInput = screen.getByPlaceholderText('e.g. 5');
      const skillInput = screen.getByPlaceholderText('Add skill (e.g. React)');
      const addSkillBtn = screen.getByRole('button', { name: /^Add$/i });

      // 1. Modify input fields
      await user.clear(nameInput);
      await user.type(nameInput, 'Aung Kyaw Senior');

      await user.clear(emailInput);
      await user.type(emailInput, 'aung.kyaw.senior@example.com');

      await user.clear(phoneInput);
      await user.type(phoneInput, '+95 988877766');

      await user.type(expInput, '7');

      await user.type(skillInput, 'DotNet Core');
      await user.click(addSkillBtn);

      await user.type(skillInput, 'PostgreSQL');
      await user.click(addSkillBtn);

      // Verify skills are visible in the form as tags
      expect(screen.getByText('DotNet Core')).toBeInTheDocument();
      expect(screen.getByText('PostgreSQL')).toBeInTheDocument();

      // EMPIRICAL ASSERTION: Modifying fields MUST NOT trigger confirmParsedProfile API automatically
      expect(resumeApi.confirmParsedProfile).not.toHaveBeenCalled();

      // 2. Click "Confirm & Apply to Profile" button explicitly
      const confirmBtn = screen.getByRole('button', { name: /Confirm & Apply to Profile/i });
      await user.click(confirmBtn);

      // EMPIRICAL ASSERTION: confirmParsedProfile IS triggered with full payload
      await waitFor(() => {
        expect(resumeApi.confirmParsedProfile).toHaveBeenCalledTimes(1);
        expect(resumeApi.confirmParsedProfile).toHaveBeenCalledWith('app-challenger-1', {
          candidateName: 'Aung Kyaw Senior',
          email: 'aung.kyaw.senior@example.com',
          phone: '+95 988877766',
          yearsOfExperience: 7,
          skills: ['DotNet Core', 'PostgreSQL'],
        });
      });
    }, 15000);

    it('requires Candidate Name and shows error if Candidate Name is blank on confirmation', async () => {
      const user = userEvent.setup();

      render(<CandidateSlideOver candidate={mockCandidate} isOpen={true} onClose={vi.fn()} initialTab="cv" />);

      const nameInput = screen.getByPlaceholderText('Candidate Full Name');
      await user.clear(nameInput);

      const confirmBtn = screen.getByRole('button', { name: /Confirm & Apply to Profile/i });
      await user.click(confirmBtn);

      await waitFor(() => {
        expect(screen.getByText(/Candidate Name is required./i)).toBeInTheDocument();
        expect(resumeApi.confirmParsedProfile).not.toHaveBeenCalled();
      });
    });

    it('allows removing skills from the parsed profile skills list before confirming', async () => {
      const user = userEvent.setup();
      vi.mocked(resumeApi.confirmParsedProfile).mockResolvedValueOnce(undefined);

      render(<CandidateSlideOver candidate={mockCandidate} isOpen={true} onClose={vi.fn()} initialTab="cv" />);

      const skillInput = screen.getByPlaceholderText('Add skill (e.g. React)');
      const addSkillBtn = screen.getByRole('button', { name: /^Add$/i });

      await user.type(skillInput, 'Python');
      await user.click(addSkillBtn);

      await user.type(skillInput, 'Docker');
      await user.click(addSkillBtn);

      expect(screen.getByText('Python')).toBeInTheDocument();
      expect(screen.getByText('Docker')).toBeInTheDocument();

      // Click remove button on Python
      const removeButtons = screen.getAllByRole('button', { name: '×' });
      await user.click(removeButtons[0]);

      expect(screen.queryByText('Python')).not.toBeInTheDocument();
      expect(screen.getByText('Docker')).toBeInTheDocument();

      const confirmBtn = screen.getByRole('button', { name: /Confirm & Apply to Profile/i });
      await user.click(confirmBtn);

      await waitFor(() => {
        expect(resumeApi.confirmParsedProfile).toHaveBeenCalledWith(
          'app-challenger-1',
          expect.objectContaining({
            skills: ['Docker'],
          })
        );
      });
    });
  });

  describe('3. Zawgyi Script Normalization Badge Rendering', () => {
    it('renders "Zawgyi → Unicode Normalized" badge when isZawgyiNormalized is true', async () => {
      const mockResultWithZawgyi = {
        applicationId: 'app-challenger-1',
        fileKey: 'resumes/app-challenger-1.pdf',
        fileName: 'zawgyi_cv.pdf',
        fileSizeBytes: 5000,
        extractedText: 'Zawgyi Text Converted To Unicode',
        detectedLanguage: 'my',
        isZawgyiNormalized: true,
        parsedContactInfo: null,
        processedAt: '2026-08-08T12:00:00Z',
      };

      vi.mocked(resumeApi.uploadCandidateResume).mockResolvedValueOnce(mockResultWithZawgyi);

      render(<CandidateSlideOver candidate={mockCandidate} isOpen={true} onClose={vi.fn()} initialTab="cv" />);

      const file = new File(['zawgyi text stream'], 'zawgyi_cv.pdf', { type: 'application/pdf' });
      const input = screen.getByLabelText(/Click to upload CV document/i);

      fireEvent.change(input, { target: { files: [file] } });

      await waitFor(() => {
        expect(screen.getByText('Zawgyi → Unicode Normalized')).toBeInTheDocument();
      });
    });

    it('does NOT render "Zawgyi → Unicode Normalized" badge when isZawgyiNormalized is false', async () => {
      const mockResultWithoutZawgyi = {
        applicationId: 'app-challenger-1',
        fileKey: 'resumes/app-challenger-1.pdf',
        fileName: 'standard_unicode_cv.pdf',
        fileSizeBytes: 5000,
        extractedText: 'Standard Unicode English CV',
        detectedLanguage: 'en',
        isZawgyiNormalized: false,
        parsedContactInfo: null,
        processedAt: '2026-08-08T12:00:00Z',
      };

      vi.mocked(resumeApi.uploadCandidateResume).mockResolvedValueOnce(mockResultWithoutZawgyi);

      render(<CandidateSlideOver candidate={mockCandidate} isOpen={true} onClose={vi.fn()} initialTab="cv" />);

      const file = new File(['standard pdf content'], 'standard_unicode_cv.pdf', { type: 'application/pdf' });
      const input = screen.getByLabelText(/Click to upload CV document/i);

      fireEvent.change(input, { target: { files: [file] } });

      await waitFor(() => {
        expect(screen.getByText('Standard Unicode English CV')).toBeInTheDocument();
        expect(screen.queryByText('Zawgyi → Unicode Normalized')).not.toBeInTheDocument();
      });
    });
  });
});
