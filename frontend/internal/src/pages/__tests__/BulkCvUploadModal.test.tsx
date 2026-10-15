import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { BulkCvUploadModal } from '../../features/pipeline/BulkCvUploadModal';
import { resumeApi } from '../../lib/api';

vi.mock('../../lib/api', async () => {
  const actual = await vi.importActual<typeof import('../../lib/api')>('../../lib/api');
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

describe('BulkCvUploadModal Component', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders bulk upload modal title and multi-file dropzone', () => {
    render(<BulkCvUploadModal jobPostingId="job-1" isOpen={true} onClose={vi.fn()} />);

    expect(screen.getByText('Bulk Upload CV Documents')).toBeInTheDocument();
    expect(screen.getByText(/Click to select up to 50 CV files/i)).toBeInTheDocument();
  });

  it('allows file selection and triggers postBulkResumes with status polling', async () => {
    const user = userEvent.setup();
    const mockResponse = {
      batchId: 'batch-777',
      jobPostingId: 'job-1',
      totalFiles: 2,
      status: 'Queued',
      createdAt: '2026-08-08T12:00:00Z',
    };

    const mockBatchStatus = {
      batchId: 'batch-777',
      jobPostingId: 'job-1',
      totalFiles: 2,
      processedCount: 2,
      successCount: 2,
      failedCount: 0,
      status: 'Completed',
      files: [
        { fileName: 'alice_cv.pdf', fileSizeBytes: 12000, status: 'Success' as const, candidateName: 'Alice Smith' },
        { fileName: 'bob_cv.docx', fileSizeBytes: 15000, status: 'Success' as const, candidateName: 'Bob Jones' },
      ],
      createdAt: '2026-08-08T12:00:00Z',
    };

    vi.mocked(resumeApi.postBulkResumes).mockResolvedValueOnce(mockResponse);
    vi.mocked(resumeApi.getBulkResumeStatus).mockResolvedValueOnce(mockBatchStatus);

    render(<BulkCvUploadModal jobPostingId="job-1" isOpen={true} onClose={vi.fn()} />);

    const file1 = new File(['pdf1'], 'alice_cv.pdf', { type: 'application/pdf' });
    const file2 = new File(['docx2'], 'bob_cv.docx', { type: 'application/vnd.openxmlformats-officedocument.wordprocessingml.document' });

    const input = screen.getByLabelText(/Click to select up to 50 CV files/i);
    fireEvent.change(input, { target: { files: [file1, file2] } });

    await waitFor(() => {
      expect(screen.getByText(/Selected Files \(2 \/ 50\)/i)).toBeInTheDocument();
      expect(screen.getByText('alice_cv.pdf')).toBeInTheDocument();
      expect(screen.getByText('bob_cv.docx')).toBeInTheDocument();
    });

    const startBtn = screen.getByRole('button', { name: /Start Bulk Upload \(2\)/i });
    await user.click(startBtn);

    await waitFor(() => {
      expect(resumeApi.postBulkResumes).toHaveBeenCalledWith('job-1', [file1, file2]);
      expect(resumeApi.getBulkResumeStatus).toHaveBeenCalledWith('job-1', 'batch-777');
      expect(screen.getByText(/2 \/ 2 Processed/i)).toBeInTheDocument();
      expect(screen.getByText('Candidate: Alice Smith')).toBeInTheDocument();
    });
  });
});
