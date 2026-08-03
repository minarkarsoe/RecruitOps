import { useState } from 'react';
import {
  Badge,
  Button,
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
import type { Interview, PipelineItem, StageHistoryItem } from '@recruitops/types';
import { parseFormFields } from '@recruitops/types';
import { ApplicationNotes } from '../../components/ApplicationNotes';

export interface CandidateSlideOverProps {
  candidate: PipelineItem | null;
  isOpen: boolean;
  onClose: () => void;
  stageHistory?: StageHistoryItem[];
  interviews?: Interview[];
  onOpenScorecard?: (interviewId: string) => void;
  onMoveStage?: (applicationId: string, toStatus: any) => Promise<void>;
  applicationFormFieldsJson?: string | null;
  initialTab?: string;
  className?: string;
}

function CustomAnswersView({
  answersJson,
  schemaJson,
}: {
  answersJson: string | null;
  schemaJson?: string | null;
}) {
  if (!answersJson) return <p className="text-xs text-ink-400">No custom response submitted.</p>;

  let answers: Record<string, unknown>;
  try {
    answers = JSON.parse(answersJson) as Record<string, unknown>;
  } catch {
    return <p className="text-xs text-ink-400">Unable to parse custom responses.</p>;
  }

  const entries = Object.entries(answers);
  if (entries.length === 0) return <p className="text-xs text-ink-400">No custom response submitted.</p>;

  const labels = new Map(parseFormFields(schemaJson).map((f) => [f.key, f.label]));

  return (
    <dl className="grid grid-cols-1 gap-x-4 gap-y-2 text-sm sm:grid-cols-2">
      {entries.map(([key, value]) => (
        <div key={key} className="rounded-md border border-line-200 bg-surface-50 p-2.5">
          <dt className="text-xs font-semibold text-ink-600">{labels.get(key) ?? key}</dt>
          <dd className="mt-0.5 font-medium text-ink-900">
            {typeof value === 'boolean' ? (value ? 'Yes' : 'No') : String(value)}
          </dd>
        </div>
      ))}
    </dl>
  );
}

export function CandidateSlideOver({
  candidate,
  isOpen,
  onClose,
  stageHistory = [],
  interviews = [],
  onOpenScorecard,
  applicationFormFieldsJson,
  initialTab = 'overview',
  className = '',
}: CandidateSlideOverProps) {
  const [activeTab, setActiveTab] = useState<string>(initialTab);

  if (!isOpen) return null;

  return (
    <Sheet isOpen={isOpen} onClose={onClose} size="xl" className={className}>
      {!candidate ? (
        <SheetBody>
          <div className="py-12 text-center text-ink-600">No candidate profile selected.</div>
        </SheetBody>
      ) : (
        <div className="flex h-full flex-col">
          {/* Candidate 360 Header */}
          <SheetHeader>
            <div className="flex flex-wrap items-start justify-between gap-4">
              <div>
                <div className="flex items-center gap-3">
                  <SheetTitle>{candidate.candidateName}</SheetTitle>
                  <StatusPill status={candidate.status} />
                  {candidate.source && <Badge variant="cyan">{candidate.source}</Badge>}
                </div>
                <p className="mt-1 text-sm text-ink-600">
                  {candidate.email || 'No email'} · {candidate.phone || 'No phone'} · Applied{' '}
                  {new Date(candidate.appliedAt).toLocaleDateString()}
                </p>
              </div>
            </div>

            {/* Navigation Tabs */}
            <div className="mt-4">
              <Tabs value={activeTab} onValueChange={setActiveTab}>
                <TabsList>
                  <TabsTrigger value="overview">Overview</TabsTrigger>
                  <TabsTrigger value="cv">CV Viewer</TabsTrigger>
                  <TabsTrigger value="history" count={stageHistory.length}>
                    Stage History
                  </TabsTrigger>
                  <TabsTrigger value="scorecards" count={interviews.length}>
                    Scorecards
                  </TabsTrigger>
                  <TabsTrigger value="notes">Notes & Debrief</TabsTrigger>
                </TabsList>
              </Tabs>
            </div>
          </SheetHeader>

          {/* Drawer Body Content */}
          <SheetBody className="flex-1 overflow-y-auto">
            <Tabs value={activeTab} onValueChange={setActiveTab}>
              {/* Tab 1: Overview */}
              <TabsContent value="overview" className="space-y-6">
                {/* Contact & Meta Card */}
                <div className="rounded-md border border-line-200 bg-surface-50 p-4">
                  <h3 className="mb-3 text-xs font-semibold uppercase tracking-wider text-ink-500">
                    Candidate Profile Summary
                  </h3>
                  <dl className="grid grid-cols-1 gap-4 text-sm sm:grid-cols-3">
                    <div>
                      <dt className="text-xs text-ink-500">Full Name</dt>
                      <dd className="font-semibold text-ink-900">{candidate.candidateName}</dd>
                    </div>
                    <div>
                      <dt className="text-xs text-ink-500">Email Address</dt>
                      <dd className="font-medium text-ink-900">{candidate.email || '—'}</dd>
                    </div>
                    <div>
                      <dt className="text-xs text-ink-500">Phone Number</dt>
                      <dd className="font-medium text-ink-900">{candidate.phone || '—'}</dd>
                    </div>
                  </dl>
                </div>

                {/* Cover Note */}
                <div>
                  <h3 className="mb-2 text-xs font-semibold uppercase tracking-wider text-ink-500">
                    Cover Letter / Application Note
                  </h3>
                  <div className="rounded-md border border-line-200 bg-surface-0 p-4 text-sm leading-relaxed text-ink-900 whitespace-pre-wrap">
                    {candidate.coverNote || 'No cover note submitted.'}
                  </div>
                </div>

                {/* Custom Application Form Answers */}
                <div>
                  <h3 className="mb-2 text-xs font-semibold uppercase tracking-wider text-ink-500">
                    Application Form Answers
                  </h3>
                  <CustomAnswersView
                    answersJson={candidate.customFieldsJson}
                    schemaJson={applicationFormFieldsJson}
                  />
                </div>
              </TabsContent>

              {/* Tab 2: CV Viewer */}
              <TabsContent value="cv" className="space-y-4">
                <div className="flex items-center justify-between rounded-md border border-line-200 bg-surface-50 p-3">
                  <div className="flex items-center gap-2">
                    <svg className="h-5 w-5 text-primary-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
                    </svg>
                    <span className="text-sm font-semibold text-ink-900">
                      {candidate.candidateName}_Resume.pdf
                    </span>
                  </div>
                </div>

                <div className="flex min-h-[380px] flex-col items-center justify-center rounded-md border border-line-200 bg-surface-50/50 p-8 text-center">
                  <svg className="h-12 w-12 text-ink-300 mb-3" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M7 21h10a2 2 0 002-2V9.414a1 1 0 00-.293-.707l-5.414-5.414A1 1 0 0012.586 3H7a2 2 0 00-2 2v14a2 2 0 002 2z" />
                  </svg>
                  <h4 className="text-base font-semibold text-ink-900">CV Document Preview</h4>
                  <p className="mt-1 max-w-sm text-xs text-ink-600">
                    Document preview is loaded.
                  </p>
                </div>
              </TabsContent>

              {/* Tab 3: Stage History */}
              <TabsContent value="history" className="space-y-4">
                <h3 className="text-xs font-semibold uppercase tracking-wider text-ink-500">
                  Recruitment Stage Timeline
                </h3>
                {stageHistory.length === 0 ? (
                  <div className="rounded-md border border-line-200 bg-surface-50 p-6 text-center text-sm text-ink-600">
                    No stage history recorded yet.
                  </div>
                ) : (
                  <div className="rounded-md border border-line-200 bg-surface-0 p-4">
                    <ol className="relative border-l border-line-200 ml-3 space-y-6">
                      {stageHistory.map((item, idx) => (
                        <li key={idx} className="ml-6">
                          <span className="absolute -left-3 flex h-6 w-6 items-center justify-center rounded-full bg-primary-100 ring-4 ring-surface-0 text-primary-700 text-xs font-bold">
                            {idx + 1}
                          </span>
                          <div className="flex items-center gap-2">
                            <span className="text-sm font-semibold text-ink-900">
                              Moved to {item.toStatus}
                            </span>
                            {item.fromStatus && (
                              <span className="text-xs text-ink-400">
                                (from {item.fromStatus})
                              </span>
                            )}
                          </div>
                          <p className="mt-0.5 text-xs text-ink-500">
                            {item.changedByName ? `By ${item.changedByName} · ` : ''}
                            {new Date(item.changedAt).toLocaleString()}
                          </p>
                          {item.note && (
                            <p className="mt-2 rounded bg-surface-50 p-2 text-xs italic text-ink-700 border border-line-200">
                              &ldquo;{item.note}&rdquo;
                            </p>
                          )}
                        </li>
                      ))}
                    </ol>
                  </div>
                )}
              </TabsContent>

              {/* Tab 4: Scorecard Summaries */}
              <TabsContent value="scorecards" className="space-y-4">
                <h3 className="text-xs font-semibold uppercase tracking-wider text-ink-500">
                  Interview Rounds & Panel Scorecards
                </h3>
                {interviews.length === 0 ? (
                  <div className="rounded-md border border-line-200 bg-surface-50 p-6 text-center text-sm text-ink-600">
                    No interview rounds scheduled yet.
                  </div>
                ) : (
                  <div className="space-y-3">
                    {interviews.map((interview) => {
                      const submittedCount = interview.participants.filter((p) => p.hasSubmittedScorecard).length;

                      return (
                        <div
                          key={interview.id}
                          className="flex items-center justify-between rounded-md border border-line-200 bg-surface-0 p-4 transition-colors hover:border-primary-600/40"
                        >
                          <div>
                            <div className="flex items-center gap-2">
                              <span className="font-semibold text-sm text-ink-900">
                                Round {interview.round}
                              </span>
                              <StatusPill status={interview.status} />
                            </div>
                            <p className="mt-1 text-xs text-ink-600">
                              {new Date(interview.scheduledStart).toLocaleString()} · {interview.durationMinutes} mins · {interview.mode}
                            </p>
                            <p className="mt-1 text-xs text-ink-400">
                              Panel: {interview.participants.map((p) => p.displayName).join(', ')} ({submittedCount}/{interview.participants.length} submitted)
                            </p>
                          </div>

                          {onOpenScorecard && (
                            <Button
                              variant="secondary"
                              className="h-8 px-3 text-xs"
                              onClick={() => onOpenScorecard(interview.id)}
                            >
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
            </Tabs>
          </SheetBody>
        </div>
      )}
    </Sheet>
  );
}
