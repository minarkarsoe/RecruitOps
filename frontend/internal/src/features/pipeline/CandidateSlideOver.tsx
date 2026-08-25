import { useState, useEffect, type ReactNode } from 'react';
import {
  Badge,
  Button,
  Input,
  Sheet,
  SheetBody,
  SheetHeader,
  SheetTitle,
  StatusPill,
  Tabs,
  TabsContent,
  TabsList,
  TabsTrigger,
} from '@recruitops/ui';
import type {
  CandidateMatchAnalysis,
  Interview,
  PipelineItem,
  ResumeExtractionResult,
  StageHistoryItem,
} from '@recruitops/types';
import { parseFormFields } from '@recruitops/types';
import { ApplicationNotes } from '../../components/ApplicationNotes';
import { resumeApi } from '../../lib/api';
import { SmartMatchBreakdown, getMatchBadgeConfig } from './SmartMatchBreakdown';
import { ExecutiveSummaryPanel } from './ExecutiveSummaryPanel';

export interface CandidateSlideOverProps {
  candidate: PipelineItem | null;
  jobPostingId?: string;
  isOpen: boolean;
  onClose: () => void;
  stageHistory?: StageHistoryItem[];
  interviews?: Interview[];
  onOpenScorecard?: (interviewId: string) => void;
  onMoveStage?: (applicationId: string, toStatus: any) => Promise<void>;
  onProfileUpdated?: () => void;
  applicationFormFieldsJson?: string | null;
  initialTab?: string;
  className?: string;
  initialMatchAnalysis?: CandidateMatchAnalysis | null;
}

// The drawer's section label, straight out of `design/internal/board.html`. This is the ONE
// place the kit uses uppercase micro-caps: a drawer is a stack of short unrelated sections and
// the label is a divider, not a heading. On a page the same treatment reads as a label for the
// thing beside it, which is why the pages were moved off it and this was not.
function SectionLabel({ children }: { children: ReactNode }) {
  return (
    <p className="text-2xs font-medium uppercase tracking-wider text-ink-500">{children}</p>
  );
}

function CustomAnswersView({
  answersJson,
  schemaJson,
}: {
  answersJson: string | null;
  schemaJson?: string | null;
}) {
  if (!answersJson) return <p className="text-sm text-ink-600">No custom response submitted.</p>;

  let answers: Record<string, unknown>;
  try {
    answers = JSON.parse(answersJson) as Record<string, unknown>;
  } catch {
    return <p className="text-sm text-ink-600">Unable to parse custom responses.</p>;
  }

  const entries = Object.entries(answers);
  if (entries.length === 0) return <p className="text-sm text-ink-600">No custom response submitted.</p>;

  const labels = new Map(parseFormFields(schemaJson).map((f) => [f.key, f.label]));

  return (
    <dl className="grid grid-cols-1 gap-x-4 gap-y-2 sm:grid-cols-2">
      {entries.map(([key, value]) => (
        <div key={key} className="rounded-md border border-line bg-canvas p-2.5">
          <dt className="text-sm text-ink-600">{labels.get(key) ?? key}</dt>
          <dd className="mt-0.5 text-base font-medium text-ink-900">
            {typeof value === 'boolean' ? (value ? 'Yes' : 'No') : String(value)}
          </dd>
        </div>
      ))}
    </dl>
  );
}

export function CvAndDocumentsTab({
  candidate,
  onProfileUpdated,
}: {
  candidate: PipelineItem;
  onProfileUpdated?: () => void;
}) {
  const [uploading, setUploading] = useState(false);
  const [uploadProgress, setUploadProgress] = useState(0);
  const [extractionResult, setExtractionResult] = useState<ResumeExtractionResult | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [dragActive, setDragActive] = useState(false);
  const [confirming, setConfirming] = useState(false);
  const [confirmSuccess, setConfirmSuccess] = useState(false);

  const [form, setForm] = useState({
    candidateName: candidate.candidateName || '',
    email: candidate.email || '',
    phone: candidate.phone || '',
    yearsOfExperience: '' as number | '',
    skills: [] as string[],
    newSkillInput: '',
  });

  const handleFileUpload = async (file: File) => {
    if (file.size > 10 * 1024 * 1024) {
      setError('File size exceeds maximum limit of 10MB.');
      return;
    }
    const validExts = ['.pdf', '.docx', '.png', '.jpg', '.jpeg'];
    const ext = file.name.substring(file.name.lastIndexOf('.')).toLowerCase();
    if (!validExts.includes(ext)) {
      setError('Invalid file format. Allowed formats: PDF, DOCX, PNG, JPG, JPEG.');
      return;
    }

    setUploading(true);
    setUploadProgress(30);
    setError(null);
    setConfirmSuccess(false);

    try {
      setUploadProgress(60);
      const result = await resumeApi.uploadCandidateResume(candidate.id, file);
      setUploadProgress(100);
      setExtractionResult(result);

      const info = result.parsedContactInfo;
      if (info) {
        setForm((prev) => ({
          ...prev,
          candidateName: info.candidateName || prev.candidateName,
          email: info.email || prev.email,
          phone: info.phone || prev.phone,
          yearsOfExperience: info.yearsOfExperience ?? prev.yearsOfExperience,
          skills: info.skills ?? prev.skills,
        }));
      }
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to upload and extract CV.');
    } finally {
      setUploading(false);
    }
  };

  const handleConfirmProfile = async () => {
    if (!form.candidateName.trim()) {
      setError('Candidate Name is required.');
      return;
    }
    setConfirming(true);
    setError(null);
    try {
      await resumeApi.confirmParsedProfile(candidate.id, {
        candidateName: form.candidateName.trim(),
        email: form.email.trim() || null,
        phone: form.phone.trim() || null,
        yearsOfExperience: form.yearsOfExperience === '' ? null : Number(form.yearsOfExperience),
        skills: form.skills,
      });
      setConfirmSuccess(true);
      if (onProfileUpdated) onProfileUpdated();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to confirm profile updates.');
    } finally {
      setConfirming(false);
    }
  };

  const handleDownloadCv = async () => {
    try {
      const blob = await resumeApi.downloadCandidateResume(candidate.id);
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `${candidate.candidateName.replace(/\s+/g, '_')}_CV.pdf`;
      document.body.appendChild(a);
      a.click();
      a.remove();
      window.URL.revokeObjectURL(url);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Download failed.');
    }
  };

  return (
    <div className="space-y-6">
      {/* Candidate Resume File Header */}
      <div className="flex items-center justify-between rounded-md border border-line bg-canvas p-3">
        <div className="flex items-center gap-2">
          <svg className="h-5 w-5 text-brand-700" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
          </svg>
          <span className="text-base font-medium text-ink-900">
            {candidate.candidateName}_Resume.pdf
          </span>
        </div>
      </div>

      {/* Drag and drop single file upload zone */}
      <div
        className={`relative flex flex-col items-center justify-center rounded-lg border-2 border-dashed p-6 transition-colors ${
          dragActive
            ? 'border-brand-700 bg-brand-50'
            : 'border-line-strong bg-canvas hover:border-brand-600'
        }`}
        onDragOver={(e) => {
          e.preventDefault();
          setDragActive(true);
        }}
        onDragLeave={() => setDragActive(false)}
        onDrop={(e) => {
          e.preventDefault();
          setDragActive(false);
          if (e.dataTransfer.files?.[0]) handleFileUpload(e.dataTransfer.files[0]);
        }}
      >
        <input
          type="file"
          id="cv-single-upload"
          className="hidden"
          accept=".pdf,.docx,.png,.jpg,.jpeg"
          onChange={(e) => e.target.files?.[0] && handleFileUpload(e.target.files[0])}
        />
        <label
          htmlFor="cv-single-upload"
          className="flex cursor-pointer flex-col items-center justify-center text-center"
        >
          <svg className="h-10 w-10 text-ink-400 mb-2" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M7 16a4 4 0 01-.88-7.903A5 5 0 1115.9 6L16 6a5 5 0 011 9.9M15 13l-3-3m0 0l-3 3m3-3v12" />
          </svg>
          <span className="text-base font-medium text-brand-700 hover:underline">
            Click to upload CV document
          </span>
          <span className="mt-1 text-sm text-ink-500">
            Supports PDF, DOCX, PNG, JPG up to 10MB
          </span>
        </label>
      </div>

      {/* Upload progress bar */}
      {uploading && (
        <div className="space-y-1">
          <div className="flex justify-between text-sm text-ink-600">
            <span>Uploading and extracting text...</span>
            <span className="font-mono tnum">{uploadProgress}%</span>
          </div>
          <div className="h-2 w-full overflow-hidden rounded-full bg-line">
            <div
              className="h-2 rounded-full bg-brand-700 transition-all duration-300"
              style={{ width: `${uploadProgress}%` }}
            />
          </div>
        </div>
      )}

      {error && (
        <div className="rounded-md border border-critical-100 bg-critical-50 p-3 text-sm text-critical-700">
          {error}
        </div>
      )}
      {confirmSuccess && (
        <div className="rounded-md border border-positive-100 bg-positive-50 p-3 text-sm text-positive-700">
          Candidate profile updated and confirmed successfully!
        </div>
      )}

      {/* Side-by-side view: Raw extracted text & Human Review form */}
      <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
        {/* Extracted Raw CV Text */}
        <div className="flex flex-col rounded-md border border-line bg-white p-4">
          <div className="mb-3 flex items-center justify-between">
            <SectionLabel>Extracted raw CV text</SectionLabel>
            {extractionResult?.isZawgyiNormalized && (
              <span className="inline-flex h-5 items-center rounded-full bg-info-50 px-2 text-2xs font-medium text-info-700">
                Zawgyi → Unicode Normalized
              </span>
            )}
          </div>

          {extractionResult ? (
            <div className="max-h-80 flex-1 overflow-y-auto whitespace-pre-wrap rounded-md border border-line bg-canvas p-3 font-mono text-sm text-ink-900">
              {extractionResult.extractedText || 'No text extracted from document.'}
            </div>
          ) : (
            <div className="flex min-h-[220px] flex-col items-center justify-center rounded-md border border-line bg-canvas p-6 text-center">
              <p className="text-base font-medium text-ink-900">CV Document Preview</p>
              <p className="mt-1 text-sm text-ink-600">
                Upload a CV document above to extract and view readable text.
              </p>
            </div>
          )}

          {extractionResult && (
            <div className="mt-3 flex items-center justify-between text-sm text-ink-500">
              <span>File: {extractionResult.fileName}</span>
              <span>Language: {extractionResult.detectedLanguage || 'EN'}</span>
            </div>
          )}
        </div>

        {/* Parsed Profile Human Review Form */}
        <div className="space-y-4 rounded-md border border-line bg-white p-4">
          <div className="border-b border-line pb-2">
            <SectionLabel>Parsed profile — human review</SectionLabel>
          </div>

          <div className="space-y-3">
            <div>
              <label className="mb-1 block text-sm font-medium text-ink-700">Candidate Name *</label>
              <Input
                value={form.candidateName}
                onChange={(e) => setForm({ ...form, candidateName: e.target.value })}
                placeholder="Candidate Full Name"
              />
            </div>

            <div className="grid grid-cols-2 gap-3">
              <div>
                <label className="mb-1 block text-sm font-medium text-ink-700">Email Address</label>
                <Input
                  value={form.email}
                  onChange={(e) => setForm({ ...form, email: e.target.value })}
                  placeholder="candidate@example.com"
                />
              </div>
              <div>
                <label className="mb-1 block text-sm font-medium text-ink-700">Phone Number</label>
                <Input
                  value={form.phone}
                  onChange={(e) => setForm({ ...form, phone: e.target.value })}
                  placeholder="+95 9..."
                />
              </div>
            </div>

            <div>
              <label className="mb-1 block text-sm font-medium text-ink-700">Years of Experience</label>
              <Input
                type="number"
                value={form.yearsOfExperience}
                onChange={(e) =>
                  setForm({ ...form, yearsOfExperience: e.target.value === '' ? '' : Number(e.target.value) })
                }
                placeholder="e.g. 5"
              />
            </div>

            <div>
              <label className="mb-1 block text-sm font-medium text-ink-700">Skills</label>
              <div className="flex flex-wrap gap-1.5 mb-2">
                {form.skills.map((skill, idx) => (
                  <span
                    key={idx}
                    className="inline-flex h-5 items-center gap-1 rounded-full bg-brand-50 px-2 text-2xs font-medium text-brand-800"
                  >
                    {skill}
                    <button
                      type="button"
                      className="text-brand-700 hover:text-brand-900"
                      onClick={() => setForm({ ...form, skills: form.skills.filter((_, i) => i !== idx) })}
                    >
                      ×
                    </button>
                  </span>
                ))}
              </div>
              <div className="flex gap-2">
                <Input
                  value={form.newSkillInput}
                  onChange={(e) => setForm({ ...form, newSkillInput: e.target.value })}
                  placeholder="Add skill (e.g. React)"
                />
                <Button
                  type="button"
                  variant="secondary"
                  onClick={() => {
                    if (form.newSkillInput.trim()) {
                      setForm({
                        ...form,
                        skills: [...form.skills, form.newSkillInput.trim()],
                        newSkillInput: '',
                      });
                    }
                  }}
                >
                  Add
                </Button>
              </div>
            </div>
          </div>

          <div className="flex justify-end border-t border-line pt-2">
            {/* No className override: the Button's own `primary` variant IS brand-700, and
                overriding it was how the two drifted apart in the first place. */}
            <Button onClick={handleConfirmProfile} disabled={confirming}>
              {confirming ? 'Saving Profile...' : 'Confirm & Apply to Profile'}
            </Button>
          </div>
        </div>
      </div>

      {/* Download Original CV Document button */}
      <div className="flex justify-end pt-2">
        <Button variant="secondary" onClick={handleDownloadCv} className="gap-2">
          <svg className="h-4 w-4 text-brand-700" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4" />
          </svg>
          Download Original CV Document
        </Button>
      </div>
    </div>
  );
}

export function CandidateSlideOver({
  candidate,
  jobPostingId,
  isOpen,
  onClose,
  stageHistory = [],
  interviews = [],
  onOpenScorecard,
  onProfileUpdated,
  applicationFormFieldsJson,
  initialTab = 'overview',
  className = '',
  initialMatchAnalysis = null,
}: CandidateSlideOverProps) {
  const [activeTab, setActiveTab] = useState<string>(initialTab);
  const [matchAnalysis, setMatchAnalysis] = useState<CandidateMatchAnalysis | null>(initialMatchAnalysis);

  useEffect(() => {
    setActiveTab(initialTab);
  }, [initialTab]);

  if (!isOpen) return null;

  const matchBadgeConfig = matchAnalysis ? getMatchBadgeConfig(matchAnalysis.recommendation, matchAnalysis.overallScore) : null;

  return (
    <Sheet isOpen={isOpen} onClose={onClose} size="xl" className={className}>
      {!candidate ? (
        <SheetBody>
          <div className="py-12 text-center text-ink-600">No candidate profile selected.</div>
        </SheetBody>
      ) : (
        <Tabs value={activeTab} onValueChange={setActiveTab}>
          <div className="flex h-full flex-col">
            {/* Candidate 360 Header */}
            <SheetHeader>
              <div className="flex flex-wrap items-start justify-between gap-4">
                <div>
                  <div className="flex flex-wrap items-center gap-3">
                    <SheetTitle>{candidate.candidateName}</SheetTitle>
                    <StatusPill status={candidate.status} />
                    {candidate.source && <Badge variant="secondary">{candidate.source}</Badge>}

                    {/* Smart Match Header Badge */}
                    {matchBadgeConfig ? (
                      <button
                        type="button"
                        onClick={() => setActiveTab('ai')}
                        aria-label="View Smart Match Breakdown"
                        className="inline-flex items-center gap-1 transition-opacity hover:opacity-85"
                      >
                        <Badge variant={matchBadgeConfig.variant} className="cursor-pointer">
                          {matchAnalysis?.overallScore}% Match
                        </Badge>
                      </button>
                    ) : (
                      <button
                        type="button"
                        onClick={() => setActiveTab('ai')}
                        className="inline-flex items-center gap-1 transition-opacity hover:opacity-85"
                      >
                        <Badge variant="primary" className="cursor-pointer">
                          AI Smart Match
                        </Badge>
                      </button>
                    )}
                  </div>
                  <p className="mt-1 text-sm text-ink-600">
                    {candidate.email || 'No email'} · {candidate.phone || 'No phone'} · Applied{' '}
                    {new Date(candidate.appliedAt).toLocaleDateString()}
                  </p>
                </div>
              </div>

              {/* Navigation Tabs */}
              <div className="mt-4">
                <TabsList>
                  <TabsTrigger value="overview">Overview</TabsTrigger>
                  <TabsTrigger value="ai">AI Insights</TabsTrigger>
                  <TabsTrigger value="cv">CV Viewer</TabsTrigger>
                  <TabsTrigger value="history" count={stageHistory.length}>
                    Stage History
                  </TabsTrigger>
                  <TabsTrigger value="scorecards" count={interviews.length}>
                    Scorecards
                  </TabsTrigger>
                  <TabsTrigger value="notes">Notes & Debrief</TabsTrigger>
                </TabsList>
              </div>
            </SheetHeader>

            {/* Drawer Body Content */}
            <SheetBody className="flex-1 overflow-y-auto">
              {/* Tab: AI Insights */}
              <TabsContent value="ai" className="space-y-6">
                <SmartMatchBreakdown
                  candidateId={candidate.candidateId || candidate.id}
                  jobPostingId={jobPostingId}
                  initialAnalysis={matchAnalysis}
                  onAnalysisUpdated={setMatchAnalysis}
                />
                <ExecutiveSummaryPanel
                  candidateId={candidate.candidateId || candidate.id}
                  jobPostingId={jobPostingId}
                  candidateName={candidate.candidateName}
                />
              </TabsContent>
              {/* Tab 1: Overview */}
              <TabsContent value="overview" className="space-y-6">
                {/* Contact & Meta Card */}
                <div className="rounded-md border border-line bg-canvas p-4">
                  <div className="mb-3">
                    <SectionLabel>Candidate Profile Summary</SectionLabel>
                  </div>
                  <dl className="grid grid-cols-1 gap-4 sm:grid-cols-3">
                    <div>
                      <dt className="text-sm text-ink-600">Full Name</dt>
                      <dd className="text-base font-medium text-ink-900">{candidate.candidateName}</dd>
                    </div>
                    <div>
                      <dt className="text-sm text-ink-600">Email Address</dt>
                      <dd className="text-base font-medium text-ink-900">{candidate.email || '—'}</dd>
                    </div>
                    <div>
                      <dt className="text-sm text-ink-600">Phone Number</dt>
                      <dd className="text-base font-medium text-ink-900">{candidate.phone || '—'}</dd>
                    </div>
                  </dl>
                </div>

                {/* Cover Note */}
                <div>
                  <div className="mb-2">
                    <SectionLabel>Cover Letter / Application Note</SectionLabel>
                  </div>
                  <div className="whitespace-pre-wrap rounded-md border border-line bg-white p-4 text-base leading-6 text-ink-900">
                    {candidate.coverNote || 'No cover note submitted.'}
                  </div>
                </div>

                {/* Custom Application Form Answers */}
                <div>
                  <div className="mb-2">
                    <SectionLabel>Application Form Answers</SectionLabel>
                  </div>
                  <CustomAnswersView
                    answersJson={candidate.customFieldsJson}
                    schemaJson={applicationFormFieldsJson}
                  />
                </div>
              </TabsContent>

              {/* Tab 2: CV Viewer */}
              <TabsContent value="cv" className="space-y-4">
                <CvAndDocumentsTab candidate={candidate} onProfileUpdated={onProfileUpdated} />
              </TabsContent>

              {/* Tab 3: Stage History */}
              <TabsContent value="history" className="space-y-4">
                <SectionLabel>Recruitment Stage Timeline</SectionLabel>
                {stageHistory.length === 0 ? (
                  <p className="text-sm text-ink-600">No stage history recorded yet.</p>
                ) : (
                  /* The approval-chain rail (`.rail-step` in index.css), which the kit reuses for
                     stage history in `board.html` — it encodes sequence, and sequence is what a
                     stage history IS. The numbered circles it replaces encoded nothing: the
                     order was already the order, and the numbers competed with the stage names.
                     The final step is the current stage and gets the filled dot; everything
                     above it is done and gets a check. */
                  <div className="rounded-md border border-line bg-white p-4">
                    {stageHistory.map((item, idx) => {
                      const isCurrent = idx === stageHistory.length - 1;
                      return (
                        <div
                          key={idx}
                          className={`rail-step flex gap-2.5 ${isCurrent ? '' : 'is-done pb-3'}`}
                        >
                          <span
                            className={`relative z-10 grid h-[27px] w-[27px] shrink-0 place-items-center rounded-full ${
                              isCurrent ? 'bg-brand-700' : 'border border-brand-200 bg-brand-50'
                            }`}
                          >
                            {isCurrent ? (
                              <span className="h-1.5 w-1.5 rounded-full bg-white" />
                            ) : (
                              <svg className="h-3 w-3 text-brand-700" viewBox="0 0 12 12" fill="none">
                                <path
                                  d="M2.5 6.5l2.5 2.5 4.5-5.5"
                                  stroke="currentColor"
                                  strokeWidth="1.8"
                                  strokeLinecap="round"
                                  strokeLinejoin="round"
                                />
                              </svg>
                            )}
                          </span>
                          <div className="min-w-0">
                            <p className={`text-base ${isCurrent ? 'font-medium' : ''}`}>
                              Moved to {item.toStatus}
                              {item.fromStatus && (
                                <span className="text-ink-500"> (from {item.fromStatus})</span>
                              )}
                            </p>
                            <p className="text-sm text-ink-600">
                              {item.changedByName ? `By ${item.changedByName} · ` : ''}
                              <span className="font-mono tnum">
                                {new Date(item.changedAt).toLocaleString()}
                              </span>
                            </p>
                            {item.note && (
                              <p className="mt-1.5 rounded-md border border-line bg-canvas p-2 text-sm text-ink-700">
                                &ldquo;{item.note}&rdquo;
                              </p>
                            )}
                          </div>
                        </div>
                      );
                    })}
                  </div>
                )}
              </TabsContent>

              {/* Tab 4: Scorecard Summaries */}
              <TabsContent value="scorecards" className="space-y-4">
                <SectionLabel>Interview Rounds &amp; Panel Scorecards</SectionLabel>
                {interviews.length === 0 ? (
                  <p className="text-sm text-ink-600">No interview rounds scheduled yet.</p>
                ) : (
                  <div className="space-y-3">
                    {interviews.map((interview) => {
                      const submittedCount = interview.participants.filter((p) => p.hasSubmittedScorecard).length;

                      return (
                        <div
                          key={interview.id}
                          className="flex items-center justify-between gap-4 rounded-md border border-line bg-white p-4 transition-colors hover:border-line-strong"
                        >
                          <div className="min-w-0">
                            <div className="flex items-center gap-2">
                              <span className="text-base font-medium text-ink-900">
                                Round {interview.round}
                              </span>
                              <StatusPill status={interview.status} />
                            </div>
                            <p className="mt-1 text-sm text-ink-600">
                              {new Date(interview.scheduledStart).toLocaleString()} · {interview.durationMinutes} mins · {interview.mode}
                            </p>
                            <p className="mt-1 text-sm text-ink-500">
                              Panel: {interview.participants.map((p) => p.displayName).join(', ')} ({submittedCount}/{interview.participants.length} submitted)
                            </p>
                          </div>

                          {onOpenScorecard && (
                            <Button variant="secondary" onClick={() => onOpenScorecard(interview.id)}>
                              Open Scorecard →
                            </Button>
                          )}
                        </div>
                      );
                    })}
                  </div>
                )}
              </TabsContent>

              {/* Tab 5: Notes & Debrief */}
              <TabsContent value="notes" className="space-y-4">
                <ApplicationNotes applicationId={candidate.id} interviews={interviews} />
              </TabsContent>
            </SheetBody>
          </div>
        </Tabs>
      )}
    </Sheet>
  );
}

