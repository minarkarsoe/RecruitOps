import { useState, useEffect, useRef } from 'react';
import {
  Dialog,
  DialogHeader,
  DialogTitle,
  DialogBody,
  DialogFooter,
  Button,
  Badge,
} from '@recruitops/ui';
import type { BulkResumeBatchStatus, BulkFileStatus } from '@recruitops/types';
import { resumeApi } from '../../lib/api';

export interface BulkCvUploadModalProps {
  jobPostingId: string;
  isOpen: boolean;
  onClose: () => void;
  onUploadComplete?: () => void;
}

export function BulkCvUploadModal({
  jobPostingId,
  isOpen,
  onClose,
  onUploadComplete,
}: BulkCvUploadModalProps) {
  const [selectedFiles, setSelectedFiles] = useState<File[]>([]);
  const [batchId, setBatchId] = useState<string | null>(null);
  const [uploading, setUploading] = useState(false);
  const [batchStatus, setBatchStatus] = useState<BulkResumeBatchStatus | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [dragActive, setDragActive] = useState(false);
  const pollingRef = useRef<ReturnType<typeof setInterval> | null>(null);

  // Clear state when modal closes
  useEffect(() => {
    if (!isOpen) {
      setSelectedFiles([]);
      setBatchId(null);
      setBatchStatus(null);
      setError(null);
      setUploading(false);
      if (pollingRef.current) {
        clearInterval(pollingRef.current);
        pollingRef.current = null;
      }
    }
  }, [isOpen]);

  // Clean up polling interval on unmount
  useEffect(() => {
    return () => {
      if (pollingRef.current) {
        clearInterval(pollingRef.current);
      }
    };
  }, []);

  const handleFilesAdded = (files: FileList | File[]) => {
    const newFiles = Array.from(files);
    if (selectedFiles.length + newFiles.length > 50) {
      setError('Maximum 50 files allowed per bulk upload batch.');
      return;
    }
    const validFiles = newFiles.filter((f) => {
      const ext = f.name.substring(f.name.lastIndexOf('.')).toLowerCase();
      return ['.pdf', '.docx', '.png', '.jpg', '.jpeg'].includes(ext) && f.size <= 10 * 1024 * 1024;
    });

    if (validFiles.length < newFiles.length) {
      setError('Some files were ignored (exceeds 10MB or unsupported format).');
    } else {
      setError(null);
    }

    setSelectedFiles((prev) => [...prev, ...validFiles]);
  };

  const handleStartBulkUpload = async () => {
    if (selectedFiles.length === 0) return;
    setUploading(true);
    setError(null);

    try {
      const response = await resumeApi.postBulkResumes(jobPostingId, selectedFiles);
      setBatchId(response.batchId);

      // Initial status check
      try {
        const initialStatus = await resumeApi.getBulkResumeStatus(jobPostingId, response.batchId);
        setBatchStatus(initialStatus);
        if (
          initialStatus.status === 'Completed' ||
          initialStatus.status === 'PartialSuccess' ||
          initialStatus.status === 'Failed'
        ) {
          setUploading(false);
          if (onUploadComplete) onUploadComplete();
          return;
        }
      } catch {
        // Fallthrough to polling loop
      }

      // Start polling loop every 1.5s
      pollingRef.current = setInterval(async () => {
        try {
          const status = await resumeApi.getBulkResumeStatus(jobPostingId, response.batchId);
          setBatchStatus(status);
          if (
            status.status === 'Completed' ||
            status.status === 'PartialSuccess' ||
            status.status === 'Failed'
          ) {
            if (pollingRef.current) {
              clearInterval(pollingRef.current);
              pollingRef.current = null;
            }
            setUploading(false);
            if (onUploadComplete) onUploadComplete();
          }
        } catch {
          // Keep polling until completed or modal closed
        }
      }, 1500);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Bulk upload failed.');
      setUploading(false);
    }
  };

  const renderBadge = (status: BulkFileStatus) => {
    switch (status) {
      case 'Queued':
        return <Badge variant="zinc">Queued</Badge>;
      case 'Processing':
        return <Badge variant="cyan">Processing...</Badge>;
      case 'Success':
        return <Badge variant="teal">Success</Badge>;
      case 'Skipped':
        return <Badge variant="warning">Skipped</Badge>;
      case 'Failed':
        return <Badge variant="danger">Failed</Badge>;
      default:
        return <Badge variant="zinc">{status}</Badge>;
    }
  };

  return (
    <Dialog isOpen={isOpen} onClose={onClose} size="xl">
      <DialogHeader>
        <DialogTitle>Bulk Upload CV Documents</DialogTitle>
        <p className="text-xs text-ink-500 mt-1">
          Upload up to 50 CV files (.pdf, .docx, .png, .jpg) for automated extraction and pipeline creation.
        </p>
      </DialogHeader>

      <DialogBody className="space-y-4">
        {error && (
          <div className="rounded-md bg-danger-50 p-3 text-xs text-danger-700 font-medium">{error}</div>
        )}

        {!batchId ? (
          <>
            {/* File Selection Dropzone */}
            <div
              className={`flex flex-col items-center justify-center rounded-lg border-2 border-dashed p-8 text-center transition-colors ${
                dragActive ? 'border-primary-500 bg-primary-50/50' : 'border-line-300 bg-surface-50 hover:border-primary-400'
              }`}
              onDragOver={(e) => {
                e.preventDefault();
                setDragActive(true);
              }}
              onDragLeave={() => setDragActive(false)}
              onDrop={(e) => {
                e.preventDefault();
                setDragActive(false);
                if (e.dataTransfer.files) handleFilesAdded(e.dataTransfer.files);
              }}
            >
              <input
                type="file"
                id="bulk-cv-input"
                multiple
                className="hidden"
                accept=".pdf,.docx,.png,.jpg,.jpeg"
                onChange={(e) => e.target.files && handleFilesAdded(e.target.files)}
              />
              <label htmlFor="bulk-cv-input" className="cursor-pointer text-center">
                <svg className="h-10 w-10 text-ink-400 mx-auto mb-2" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-8l-4-4m0 0l-4 4m4-4v12" />
                </svg>
                <span className="text-sm font-semibold text-primary-600 hover:underline">
                  Click to select up to 50 CV files
                </span>
                <span className="block mt-1 text-xs text-ink-500">
                  Or drag and drop files directly into this area
                </span>
              </label>
            </div>

            {/* Selected File List */}
            {selectedFiles.length > 0 && (
              <div className="space-y-2">
                <div className="flex items-center justify-between">
                  <span className="text-xs font-semibold text-ink-700">
                    Selected Files ({selectedFiles.length} / 50)
                  </span>
                  <button
                    type="button"
                    className="text-xs text-danger-600 hover:underline"
                    onClick={() => setSelectedFiles([])}
                  >
                    Clear all
                  </button>
                </div>
                <div className="max-h-48 overflow-y-auto rounded-md border border-line-200 divide-y divide-line-100 bg-surface-0">
                  {selectedFiles.map((file, idx) => (
                    <div key={idx} className="flex items-center justify-between px-3 py-2 text-xs">
                      <span className="font-medium text-ink-900 truncate max-w-xs">{file.name}</span>
                      <span className="text-ink-500">{(file.size / 1024 / 1024).toFixed(2)} MB</span>
                    </div>
                  ))}
                </div>
              </div>
            )}
          </>
        ) : (
          /* Live Batch Progress View */
          <div className="space-y-4">
            <div className="flex items-center justify-between text-xs font-semibold text-ink-700">
              <span>Batch Status: {batchStatus?.status || 'Processing...'}</span>
              <span>
                {batchStatus?.processedCount ?? 0} / {batchStatus?.totalFiles ?? selectedFiles.length} Processed
              </span>
            </div>

            {/* Progress Bar */}
            <div className="w-full bg-line-200 h-2.5 rounded-full overflow-hidden">
              <div
                className="bg-primary-600 h-2.5 rounded-full transition-all duration-300"
                style={{
                  width: `${
                    batchStatus && batchStatus.totalFiles > 0
                      ? Math.round((batchStatus.processedCount / batchStatus.totalFiles) * 100)
                      : 10
                  }%`,
                }}
              />
            </div>

            {/* Per-File Progress List */}
            <div className="max-h-64 overflow-y-auto rounded-md border border-line-200 divide-y divide-line-100 bg-surface-0">
              {batchStatus?.files.map((fileItem, idx) => (
                <div key={idx} className="flex items-center justify-between px-3 py-2 text-xs">
                  <div className="flex flex-col">
                    <span className="font-medium text-ink-900 truncate max-w-xs">{fileItem.fileName}</span>
                    {fileItem.candidateName && (
                      <span className="text-[11px] text-ink-500">Candidate: {fileItem.candidateName}</span>
                    )}
                    {fileItem.errorMessage && (
                      <span className="text-[11px] text-danger-600">{fileItem.errorMessage}</span>
                    )}
                  </div>
                  <div>{renderBadge(fileItem.status)}</div>
                </div>
              ))}
            </div>
          </div>
        )}
      </DialogBody>

      <DialogFooter>
        {!batchId ? (
          <>
            <Button variant="secondary" onClick={onClose}>
              Cancel
            </Button>
            <Button
              onClick={handleStartBulkUpload}
              disabled={selectedFiles.length === 0 || uploading}
              className="bg-primary-600 text-white hover:bg-primary-700"
            >
              {uploading ? 'Starting Batch...' : `Start Bulk Upload (${selectedFiles.length})`}
            </Button>
          </>
        ) : (
          <Button
            onClick={onClose}
            disabled={uploading}
            className="bg-primary-600 text-white hover:bg-primary-700"
          >
            {uploading ? 'Processing in Background...' : 'Close & Refresh Pipeline'}
          </Button>
        )}
      </DialogFooter>
    </Dialog>
  );
}
