import { describe, expect, it, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { BulkCvUploadModal } from '../BulkCvUploadModal';
import { resumeApi } from '../../../lib/api';

vi.mock('../../../lib/api', async () => {
  const actual = await vi.importActual<typeof import('../../../lib/api')>('../../../lib/api');
  return {
    ...actual,
    resumeApi: {
      postBulkResumes: vi.fn(),
      getBulkResumeStatus: vi.fn(),
      uploadCandidateResume: vi.fn(),
      downloadCandidateResume: vi.fn(),
      confirmParsedProfile: vi.fn(),
    },
  };
});

describe('BulkCvUploadModal Empirical Stress & Edge Case Harness', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.useFakeTimers({ shouldAdvanceTime: true });
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  describe('1. File Drag-and-Drop Edge Cases', () => {
    it('0 files selected: upload button disabled and postBulkResumes not called on click', async () => {
      render(<BulkCvUploadModal jobPostingId="job-1" isOpen={true} onClose={vi.fn()} />);

      const startBtn = screen.getByRole('button', { name: /Start Bulk Upload \(0\)/i });
      expect(startBtn).toBeDisabled();

      fireEvent.click(startBtn);
      expect(resumeApi.postBulkResumes).not.toHaveBeenCalled();
    });

    it('1 file selected: renders file item, total 1/50, button enabled and clickable', async () => {
      render(<BulkCvUploadModal jobPostingId="job-1" isOpen={true} onClose={vi.fn()} />);

      const file = new File(['content'], 'single_resume.pdf', { type: 'application/pdf' });
      const input = screen.getByLabelText(/Click to select up to 50 CV files/i);

      fireEvent.change(input, { target: { files: [file] } });

      await waitFor(() => {
        expect(screen.getByText(/Selected Files \(1 \/ 50\)/i)).toBeInTheDocument();
        expect(screen.getByText('single_resume.pdf')).toBeInTheDocument();
      });

      const startBtn = screen.getByRole('button', { name: /Start Bulk Upload \(1\)/i });
      expect(startBtn).not.toBeDisabled();
    });

    it('50 files selected (exact boundary): accepts all 50 files without warning error', async () => {
      render(<BulkCvUploadModal jobPostingId="job-1" isOpen={true} onClose={vi.fn()} />);

      const files = Array.from({ length: 50 }, (_, i) =>
        new File(['cv data'], `candidate_${i + 1}.pdf`, { type: 'application/pdf' })
      );

      const input = screen.getByLabelText(/Click to select up to 50 CV files/i);
      fireEvent.change(input, { target: { files } });

      await waitFor(() => {
        expect(screen.getByText(/Selected Files \(50 \/ 50\)/i)).toBeInTheDocument();
        expect(screen.queryByText(/Maximum 50 files allowed per bulk upload batch/i)).not.toBeInTheDocument();
      });

      const startBtn = screen.getByRole('button', { name: /Start Bulk Upload \(50\)/i });
      expect(startBtn).not.toBeDisabled();
    });

    it('>50 files selected boundary warning: displays maximum file boundary error', async () => {
      render(<BulkCvUploadModal jobPostingId="job-1" isOpen={true} onClose={vi.fn()} />);

      const files = Array.from({ length: 51 }, (_, i) =>
        new File(['cv data'], `candidate_${i + 1}.pdf`, { type: 'application/pdf' })
      );

      const input = screen.getByLabelText(/Click to select up to 50 CV files/i);
      fireEvent.change(input, { target: { files } });

      await waitFor(() => {
        expect(screen.getByText('Maximum 50 files allowed per bulk upload batch.')).toBeInTheDocument();
        expect(screen.queryByText(/Selected Files/i)).not.toBeInTheDocument();
      });
    });

    it('filters out unsupported file formats and files exceeding 10MB limit with warning message', async () => {
      render(<BulkCvUploadModal jobPostingId="job-1" isOpen={true} onClose={vi.fn()} />);

      const validFile = new File(['valid'], 'good_cv.pdf', { type: 'application/pdf' });
      const hugeFile = new File([new ArrayBuffer(11 * 1024 * 1024)], 'huge_cv.pdf', { type: 'application/pdf' });
      const invalidExt = new File(['exe'], 'malicious.exe', { type: 'application/x-msdownload' });

      const input = screen.getByLabelText(/Click to select up to 50 CV files/i);
      fireEvent.change(input, { target: { files: [validFile, hugeFile, invalidExt] } });

      await waitFor(() => {
        expect(
          screen.getByText('Some files were ignored (over 10MB, or not a PDF or Word document).')
        ).toBeInTheDocument();
        expect(screen.getByText('good_cv.pdf')).toBeInTheDocument();
        expect(screen.queryByText('huge_cv.pdf')).not.toBeInTheDocument();
        expect(screen.queryByText('malicious.exe')).not.toBeInTheDocument();
        expect(screen.getByText(/Selected Files \(1 \/ 50\)/i)).toBeInTheDocument();
      });
    });

    // Added 2026-08-29 alongside dropping .png/.jpg/.jpeg. There is no OCR in this build: an
    // uploaded photo used to be accepted, turned into a fabricated "Image Document: …" string,
    // and imported as a nameless candidate that search could never find.
    it('rejects photos with a reason, not just an unsupported-format warning', async () => {
      render(<BulkCvUploadModal jobPostingId="job-1" isOpen={true} onClose={vi.fn()} />);

      const photo = new File(['jpegbytes'], 'cv_photo.jpg', { type: 'image/jpeg' });
      const input = screen.getByLabelText(/Click to select up to 50 CV files/i);
      fireEvent.change(input, { target: { files: [photo] } });

      await waitFor(() => {
        // The distinction that matters: a generic "unsupported format" reads as a file-type
        // quibble and sends the recruiter off to re-save the photo as a PDF — which lands in the
        // identical empty-text path and wastes the trip. The message has to say it cannot be read.
        expect(screen.getByText(/text recognition is not enabled/i)).toBeInTheDocument();
        expect(screen.queryByText('cv_photo.jpg')).not.toBeInTheDocument();
      });
    });
  });

  describe('2. Status Polling Lifecycle & Cleanup', () => {
    it('polls status periodically and stops polling on batch completion', async () => {
      const mockPostResponse = {
        batchId: 'batch-999',
        jobPostingId: 'job-1',
        totalFiles: 1,
        status: 'Queued',
        createdAt: '2026-08-08T12:00:00Z',
      };

      const mockIncompleteStatus = {
        batchId: 'batch-999',
        jobPostingId: 'job-1',
        totalFiles: 1,
        processedCount: 0,
        successCount: 0,
        failedCount: 0,
        status: 'Processing',
        files: [{ fileName: 'resume.pdf', fileSizeBytes: 5000, status: 'Processing' as const }],
        createdAt: '2026-08-08T12:00:00Z',
      };

      const mockCompletedStatus = {
        batchId: 'batch-999',
        jobPostingId: 'job-1',
        totalFiles: 1,
        processedCount: 1,
        successCount: 1,
        failedCount: 0,
        status: 'Completed',
        files: [{ fileName: 'resume.pdf', fileSizeBytes: 5000, status: 'Success' as const, candidateName: 'John Doe' }],
        createdAt: '2026-08-08T12:00:00Z',
      };

      vi.mocked(resumeApi.postBulkResumes).mockResolvedValueOnce(mockPostResponse);
      vi.mocked(resumeApi.getBulkResumeStatus)
        .mockResolvedValueOnce(mockIncompleteStatus) // initial check
        .mockResolvedValueOnce(mockIncompleteStatus) // poll 1 (1.5s)
        .mockResolvedValueOnce(mockCompletedStatus);  // poll 2 (3.0s)

      const onUploadComplete = vi.fn();
      render(
        <BulkCvUploadModal
          jobPostingId="job-1"
          isOpen={true}
          onClose={vi.fn()}
          onUploadComplete={onUploadComplete}
        />
      );

      const file = new File(['cv'], 'resume.pdf', { type: 'application/pdf' });
      const input = screen.getByLabelText(/Click to select up to 50 CV files/i);
      fireEvent.change(input, { target: { files: [file] } });

      const startBtn = await screen.findByRole('button', { name: /Start Bulk Upload \(1\)/i });
      fireEvent.click(startBtn);

      await waitFor(() => {
        expect(resumeApi.postBulkResumes).toHaveBeenCalledWith('job-1', [file]);
      });

      // Initial check called
      await waitFor(() => {
        expect(resumeApi.getBulkResumeStatus).toHaveBeenCalledTimes(1);
      });

      // Fast-forward 1.5s for Poll 1
      await vi.advanceTimersByTimeAsync(1500);
      await waitFor(() => {
        expect(resumeApi.getBulkResumeStatus).toHaveBeenCalledTimes(2);
      });

      // Fast-forward 1.5s for Poll 2 (returns Completed)
      await vi.advanceTimersByTimeAsync(1500);
      await waitFor(() => {
        expect(resumeApi.getBulkResumeStatus).toHaveBeenCalledTimes(3);
        expect(onUploadComplete).toHaveBeenCalledTimes(1);
      });

      // Fast-forward another 3s and verify no more calls (interval was cleared)
      await vi.advanceTimersByTimeAsync(3000);
      expect(resumeApi.getBulkResumeStatus).toHaveBeenCalledTimes(3);
    });

    it('cleans up polling interval on component unmount', async () => {
      const mockPostResponse = {
        batchId: 'batch-888',
        jobPostingId: 'job-1',
        totalFiles: 1,
        status: 'Queued',
        createdAt: '2026-08-08T12:00:00Z',
      };

      const mockIncompleteStatus = {
        batchId: 'batch-888',
        jobPostingId: 'job-1',
        totalFiles: 1,
        processedCount: 0,
        successCount: 0,
        failedCount: 0,
        status: 'Processing',
        files: [{ fileName: 'resume.pdf', fileSizeBytes: 5000, status: 'Processing' as const }],
        createdAt: '2026-08-08T12:00:00Z',
      };

      vi.mocked(resumeApi.postBulkResumes).mockResolvedValueOnce(mockPostResponse);
      vi.mocked(resumeApi.getBulkResumeStatus).mockResolvedValue(mockIncompleteStatus);

      const { unmount } = render(
        <BulkCvUploadModal jobPostingId="job-1" isOpen={true} onClose={vi.fn()} />
      );

      const file = new File(['cv'], 'resume.pdf', { type: 'application/pdf' });
      const input = screen.getByLabelText(/Click to select up to 50 CV files/i);
      fireEvent.change(input, { target: { files: [file] } });

      const startBtn = await screen.findByRole('button', { name: /Start Bulk Upload \(1\)/i });
      fireEvent.click(startBtn);

      await waitFor(() => {
        expect(resumeApi.getBulkResumeStatus).toHaveBeenCalledTimes(1);
      });

      // Unmount while polling
      unmount();

      // Fast-forward time
      await vi.advanceTimersByTimeAsync(4500);
      // Calls should not increase after unmount
      expect(resumeApi.getBulkResumeStatus).toHaveBeenCalledTimes(1);
    });
  });

  describe('3. Per-File Progress Rendering & Status Badge State Transitions', () => {
    it('renders all 5 status badges correctly (Queued, Processing, Success, Skipped, Failed)', async () => {
      const mockPostResponse = {
        batchId: 'batch-555',
        jobPostingId: 'job-1',
        totalFiles: 5,
        status: 'Processing',
        createdAt: '2026-08-08T12:00:00Z',
      };

      const mockBatchStatus = {
        batchId: 'batch-555',
        jobPostingId: 'job-1',
        totalFiles: 5,
        processedCount: 3,
        successCount: 1,
        failedCount: 1,
        status: 'Processing',
        files: [
          { fileName: 'file1_queued.pdf', fileSizeBytes: 1000, status: 'Queued' as const },
          { fileName: 'file2_proc.pdf', fileSizeBytes: 2000, status: 'Processing' as const },
          { fileName: 'file3_succ.pdf', fileSizeBytes: 3000, status: 'Success' as const, candidateName: 'Candidate A' },
          { fileName: 'file4_skip.pdf', fileSizeBytes: 4000, status: 'Skipped' as const, errorMessage: 'Duplicate file' },
          { fileName: 'file5_fail.pdf', fileSizeBytes: 5000, status: 'Failed' as const, errorMessage: 'Corrupt document format' },
        ],
        createdAt: '2026-08-08T12:00:00Z',
      };

      vi.mocked(resumeApi.postBulkResumes).mockResolvedValueOnce(mockPostResponse);
      vi.mocked(resumeApi.getBulkResumeStatus).mockResolvedValueOnce(mockBatchStatus);

      render(<BulkCvUploadModal jobPostingId="job-1" isOpen={true} onClose={vi.fn()} />);

      const file = new File(['cv'], 'file1_queued.pdf', { type: 'application/pdf' });
      const input = screen.getByLabelText(/Click to select up to 50 CV files/i);
      fireEvent.change(input, { target: { files: [file] } });

      const startBtn = await screen.findByRole('button', { name: /Start Bulk Upload \(1\)/i });
      fireEvent.click(startBtn);

      await waitFor(() => {
        expect(screen.getByText('Queued')).toBeInTheDocument();
        expect(screen.getByText('Processing...')).toBeInTheDocument();
        expect(screen.getByText('Success')).toBeInTheDocument();
        expect(screen.getByText('Skipped')).toBeInTheDocument();
        expect(screen.getByText('Failed')).toBeInTheDocument();

        expect(screen.getByText('Candidate: Candidate A')).toBeInTheDocument();
        expect(screen.getByText('Duplicate file')).toBeInTheDocument();
        expect(screen.getByText('Corrupt document format')).toBeInTheDocument();
        expect(screen.getByText(/3 \/ 5 Processed/i)).toBeInTheDocument();
      });
    });
  });
});
