import { useCallback, useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { Button, Card, StatusPill } from '@recruitops/ui';
import type {
  JobPostingDetail, PipelineItem, PipelineStatus, StageHistoryItem, UpdateJobPostingRequest,
} from '@recruitops/types';
import { api } from '../lib/api';
import { auth, hasPermission } from '../lib/auth';
import { FormFieldBuilder } from '../components/FormFieldBuilder';
import { ApplicationDebrief } from '../components/ApplicationDebrief';
import { BulkCvUploadModal } from '../features/pipeline/BulkCvUploadModal';
import { PipelineKanbanBoard } from '../features/pipeline/PipelineKanbanBoard';
import { CandidateSlideOver } from '../features/pipeline/CandidateSlideOver';

export function JobPostingDetailPage() {
  const { id } = useParams<{ id: string }>();
  const [posting, setPosting] = useState<JobPostingDetail | null>(null);
  const [pipeline, setPipeline] = useState<PipelineItem[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [editing, setEditing] = useState(false);
  const [form, setForm] = useState<UpdateJobPostingRequest | null>(null);
  const session = auth.get();
  const [isBulkModalOpen, setIsBulkModalOpen] = useState(false);

  // ── The board's selection, added 2026-08-29 when the kanban was wired ──────────────────
  //
  // `design/internal/board.html`: "Detail opens beside the board rather than replacing it, so
  // the recruiter never loses their place in the pipeline." One id, not a set — a drawer beside
  // the board is inherently one-at-a-time, and the previous row-expander's set existed to allow
  // comparing two candidates' rounds, which the board does better by keeping both cards visible.
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [stageHistory, setStageHistory] = useState<StageHistoryItem[]>([]);
  const [boardSearch, setBoardSearch] = useState('');

  const selected = pipeline.find((p) => p.id === selectedId) ?? null;

  // History is per candidate, so it is fetched on selection rather than with the board — forty
  // cards would otherwise mean forty requests before anyone has clicked anything. Failure is
  // deliberately quiet: an empty Stage History tab is a worse outcome than an error banner over
  // a drawer the recruiter opened to read something else.
  useEffect(() => {
    if (!selectedId) {
      setStageHistory([]);
      return;
    }
    let cancelled = false;
    api<StageHistoryItem[]>(`/applications/${selectedId}/history`)
      .then((h) => { if (!cancelled) setStageHistory(h); })
      .catch(() => { if (!cancelled) setStageHistory([]); });
    return () => { cancelled = true; };
  }, [selectedId]);

  const load = useCallback(async () => {
    if (!id) return;
    const detail = await api<JobPostingDetail>(`/jobpostings/${id}`);
    setPosting(detail);
    setPipeline(await api<PipelineItem[]>(`/jobpostings/${id}/pipeline`));
  }, [id]);

  useEffect(() => {
    load().catch((e) => setError(e instanceof Error ? e.message : 'Could not load the posting.'));
  }, [load]);

  async function act(run: () => Promise<unknown>) {
    setBusy(true);
    setError(null);
    try {
      await run();
      await load();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'That did not work.');
    } finally {
      setBusy(false);
    }
  }

  function startEditing() {
    if (!posting) return;
    setForm({
      title: posting.title,
      description: posting.description,
      location: posting.location,
      employmentType: posting.employmentType,
      headcount: posting.headcount,
      salaryMin: posting.salaryMin,
      salaryMax: posting.salaryMax,
      showSalary: posting.showSalary,
      applicationFormFieldsJson: posting.applicationFormFieldsJson,
    });
    setEditing(true);
  }

  const moveStage = (applicationId: string, toStatus: PipelineStatus) =>
    act(() => api(`/applications/${applicationId}/stage`, {
      method: 'POST', body: JSON.stringify({ toStatus }),
    }));

  if (error && !posting) return <p role="alert" className="text-critical-700">{error}</p>;
  if (!posting) return <p className="text-ink-600">Loading…</p>;

  const publicUrl = posting.publicToken
    ? `${window.location.protocol}//${window.location.hostname}:3000/jobs/${posting.publicToken}`
    : null;

  const field = 'h-10 w-full rounded-md border border-line px-3 focus:outline-none focus:ring-2 focus:ring-brand-700';

  return (
    <>
      <header className="mb-6">
        <Link to="/jobpostings" className="mb-2 inline-block text-sm text-brand-700 hover:underline">
          ← Back to postings
        </Link>
        <div className="flex items-center gap-3">
          <h1 className="text-xl font-semibold tracking-tight">{posting.title}</h1>
          <StatusPill status={posting.status} />
        </div>
        <p className="mt-1 text-sm text-ink-600">
          {posting.departmentName} ·{' '}
          <Link to={`/requisitions/${posting.requisitionId}`} className="text-brand-700 hover:underline">
            approved requisition
          </Link>
        </p>
      </header>

      {error && <p role="alert" className="mb-4 text-md text-critical-700">{error}</p>}

      <div className="space-y-6">
        {/* ── Advert ── */}
        <Card>
          {editing && form ? (
            <form
              className="space-y-4"
              onSubmit={(e) => {
                e.preventDefault();
                act(async () => {
                  await api(`/jobpostings/${id}`, { method: 'PUT', body: JSON.stringify(form) });
                  setEditing(false);
                });
              }}
            >
              <div>
                <label htmlFor="title" className="mb-1 block text-sm font-semibold">Title</label>
                <input id="title" required className={field} value={form.title}
                  onChange={(e) => setForm({ ...form, title: e.target.value })} />
              </div>

              <div className="grid grid-cols-3 gap-4">
                <div>
                  <label htmlFor="location" className="mb-1 block text-sm font-semibold">Location</label>
                  <input id="location" className={field} value={form.location ?? ''}
                    onChange={(e) => setForm({ ...form, location: e.target.value })} />
                </div>
                <div>
                  <label htmlFor="type" className="mb-1 block text-sm font-semibold">Employment type</label>
                  <select id="type" className={field} value={form.employmentType}
                    onChange={(e) => setForm({ ...form, employmentType: e.target.value as UpdateJobPostingRequest['employmentType'] })}>
                    <option value="FullTime">Full-time</option>
                    <option value="PartTime">Part-time</option>
                    <option value="Contract">Contract</option>
                    <option value="Internship">Internship</option>
                    <option value="Temporary">Temporary</option>
                  </select>
                </div>
                <div>
                  <label htmlFor="headcount" className="mb-1 block text-sm font-semibold">Headcount</label>
                  <input id="headcount" type="number" min={1} className={field} value={form.headcount}
                    onChange={(e) => setForm({ ...form, headcount: Number(e.target.value) })} />
                </div>
              </div>

              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label htmlFor="min" className="mb-1 block text-sm font-semibold">Salary from</label>
                  <input id="min" type="number" min={0} className={field} value={form.salaryMin ?? ''}
                    onChange={(e) => setForm({ ...form, salaryMin: e.target.value ? Number(e.target.value) : null })} />
                </div>
                <div>
                  <label htmlFor="max" className="mb-1 block text-sm font-semibold">Salary to</label>
                  <input id="max" type="number" min={0} className={field} value={form.salaryMax ?? ''}
                    onChange={(e) => setForm({ ...form, salaryMax: e.target.value ? Number(e.target.value) : null })} />
                </div>
              </div>

              <label className="flex items-center gap-2 text-md">
                <input type="checkbox" checked={form.showSalary}
                  onChange={(e) => setForm({ ...form, showSalary: e.target.checked })} />
                Show the salary on the public job page
              </label>
              <p className="text-sm text-ink-400">
                Off by default — the requisition&apos;s budget is internal, and publishing it
                would expose the company&apos;s pay bands.
              </p>

              <div>
                <label htmlFor="desc" className="mb-1 block text-sm font-semibold">
                  Description <span className="font-normal text-ink-400">(candidate-facing)</span>
                </label>
                <textarea id="desc" rows={10} required
                  className="w-full rounded-md border border-line p-3 focus:outline-none focus:ring-2 focus:ring-brand-700"
                  value={form.description}
                  onChange={(e) => setForm({ ...form, description: e.target.value })} />
              </div>

              <FormFieldBuilder
                json={form.applicationFormFieldsJson}
                onChange={(json) => setForm({ ...form, applicationFormFieldsJson: json })}
              />

              <div className="flex gap-3">
                <Button type="submit" disabled={busy}>{busy ? 'Saving…' : 'Save advert'}</Button>
                <Button variant="secondary" type="button" onClick={() => setEditing(false)}>Cancel</Button>
              </div>
            </form>
          ) : (
            <>
              <div className="mb-4 flex items-start justify-between">
                <dl className="grid grid-cols-3 gap-4 text-md">
                  <div>
                    <dt className="text-sm text-ink-600">Location</dt>
                    <dd>{posting.location ?? '—'}</dd>
                  </div>
                  <div>
                    <dt className="text-sm text-ink-600">Headcount</dt>
                    <dd className="font-mono">{posting.headcount}</dd>
                  </div>
                  <div>
                    <dt className="text-sm text-ink-600">Salary</dt>
                    <dd className="font-mono">
                      {posting.salaryMin?.toLocaleString() ?? '—'}
                      {posting.showSalary ? '' : ' (internal)'}
                    </dd>
                  </div>
                </dl>
                {posting.status !== 'Closed' && hasPermission(session, 'permission:postings:postings:update') && (
                  <Button variant="secondary" onClick={startEditing}>Edit advert</Button>
                )}
              </div>
              <div className="whitespace-pre-wrap text-md leading-relaxed">{posting.description}</div>
            </>
          )}
        </Card>

        {/* ── Publishing ── */}
        <Card>
          <h2 className="mb-3 text-base font-semibold">
            Public link
          </h2>
          {posting.status === 'Draft' && (
            <>
              <p className="mb-4 text-md text-ink-600">
                Publishing creates the shareable job link. The link is minted once and kept —
                re-publishing later will not invalidate anything already shared.
              </p>
              {hasPermission(session, 'permission:postings:postings:publish') && (
                <Button onClick={() => act(() => api(`/jobpostings/${id}/publish`, { method: 'POST' }))} disabled={busy}>
                  Publish
                </Button>
              )}
            </>
          )}
          {posting.status === 'Live' && publicUrl && (
            <>
              <p className="mb-2 text-md text-ink-600">Share this link:</p>
              <code className="block break-all rounded-md bg-canvas p-3 font-mono text-sm">
                {publicUrl}
              </code>
              <div className="mt-4 flex gap-3">
                <Button variant="secondary" onClick={() => navigator.clipboard.writeText(publicUrl)}>
                  Copy link
                </Button>
                {hasPermission(session, 'permission:postings:postings:update') && (
                  <Button variant="danger" onClick={() => act(() => api(`/jobpostings/${id}/close`, { method: 'POST' }))} disabled={busy}>
                    Close vacancy
                  </Button>
                )}
              </div>
            </>
          )}
          {posting.status === 'Closed' && (
            <p className="text-md text-ink-600">
              Closed{posting.closedAt ? ` on ${new Date(posting.closedAt).toLocaleDateString()}` : ''}.
              New applications are refused; the ones already received are untouched.
            </p>
          )}
        </Card>

        {/* ── Pipeline ── */}
        <Card>
          <div className="mb-4 flex items-center justify-between">
            <h2 className="text-base font-semibold">
              Pipeline · {pipeline.length} {pipeline.length === 1 ? 'candidate' : 'candidates'}
            </h2>
            <Button
              variant="secondary"
              className="flex items-center gap-1.5 text-xs h-8 px-3"
              onClick={() => setIsBulkModalOpen(true)}
            >
              <svg className="h-4 w-4 text-brand-700" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M7 16a4 4 0 01-.88-7.903A5 5 0 1115.9 6L16 6a5 5 0 011 9.9M15 13l-3-3m0 0l-3 3m3-3v12" />
              </svg>
              Bulk Upload CVs
            </Button>
          </div>


          {/* The kanban from `design/internal/board.html`, wired 2026-08-29. It replaces a flat
              list with a "Move to…" select per row — which worked, but made the one question the
              screen exists to answer ("where is everyone?") something you had to reconstruct by
              reading. The board answers it by shape.

              `onMoveStage` is passed only with the permission, so a hiring manager gets a
              read-only board rather than controls that 403. Terminal stages are the board's own
              rule (the API refuses the move, so offering it would only produce an error). */}
          <PipelineKanbanBoard
            postingId={id}
            items={pipeline}
            applicationFormFieldsJson={posting.applicationFormFieldsJson}
            isLoading={false}
            isMoving={busy}
            searchQuery={boardSearch}
            onSearchQueryChange={setBoardSearch}
            onSelectCandidate={setSelectedId}
            onMoveStage={
              hasPermission(session, 'permission:applications:applications:move_stage')
                ? moveStage
                : undefined
            }
          />
        </Card>
      </div>

      {/* The board's detail drawer. `interviewsSlot` is the load-bearing prop: the drawer's own
          Interviews tab can only *list* rounds, while `ApplicationDebrief` is what schedules,
          reschedules, edits a panel and cancels. Wiring the drawer without it would have looked
          like a finished screen and silently removed interview scheduling from the pipeline —
          the flow, not just a control. board.html puts "Schedule interview" in the drawer, so
          this is also where the kit says it belongs.

          Scheduling moves the stage, so `onChanged` reloads the whole board rather than one card. */}
      <CandidateSlideOver
        candidate={selected}
        jobPostingId={id}
        isOpen={selected !== null}
        onClose={() => setSelectedId(null)}
        stageHistory={stageHistory}
        applicationFormFieldsJson={posting.applicationFormFieldsJson}
        onProfileUpdated={() => { load().catch(() => {}); }}
        interviewsSlot={
          selected ? (
            <ApplicationDebrief
              applicationId={selected.id}
              onChanged={() => { load().catch(() => { /* surfaced by the page banner */ }); }}
            />
          ) : undefined
        }
      />

      <BulkCvUploadModal
        jobPostingId={id ?? ''}
        isOpen={isBulkModalOpen}
        onClose={() => setIsBulkModalOpen(false)}
        onUploadComplete={() => {
          load().catch(() => {});
        }}
      />
    </>
  );
}
