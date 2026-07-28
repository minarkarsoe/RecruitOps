import { useCallback, useEffect, useState } from 'react';
import { Button, Card } from '@recruitops/ui';
import type {
  CriterionType, DepartmentListItem, JobPostingListItem, SaveScorecardTemplateRequest,
  ScorecardCriterionInput, ScorecardTemplate,
} from '@recruitops/types';
import { api } from '../lib/api';
import { auth, isRecruitmentStaff } from '../lib/auth';

/**
 * Module 3.3 configuration — the criteria interviews are scored against.
 *
 * Reads are open to any internal user (an interviewer should be able to see what they will
 * be asked before the day); writing is recruitment staff, because defining the criteria for
 * a department sets the standard everyone in it is compared against.
 */

const TYPES: { value: CriterionType; label: string; hint: string }[] = [
  { value: 'Rating', label: 'Rating 1–5', hint: 'The only type that produces a comparable number.' },
  { value: 'YesNo', label: 'Yes / no', hint: 'Pass or fail — "holds the required certification".' },
  { value: 'Text', label: 'Written answer', hint: 'Evidence, not a score.' },
];

type Scope = 'company' | 'department' | 'posting';

function scopeOf(t: ScorecardTemplate): Scope {
  if (t.jobPostingId) return 'posting';
  if (t.departmentId) return 'department';
  return 'company';
}

function scopeLabel(t: ScorecardTemplate, postings: JobPostingListItem[]): string {
  if (t.jobPostingId) {
    const posting = postings.find((p) => p.id === t.jobPostingId);
    return posting ? `Posting · ${posting.title}` : 'One posting';
  }
  if (t.departmentId) return `Department · ${t.departmentName ?? '—'}`;
  return 'Company-wide default';
}

interface FormState {
  name: string;
  description: string;
  scope: Scope;
  departmentId: string;
  jobPostingId: string;
  isActive: boolean;
  criteria: ScorecardCriterionInput[];
}

function emptyForm(): FormState {
  return {
    name: '',
    description: '',
    scope: 'company',
    departmentId: '',
    jobPostingId: '',
    isActive: true,
    criteria: [{ label: '', guidance: '', type: 'Rating', isRequired: true }],
  };
}

function formFrom(t: ScorecardTemplate): FormState {
  return {
    name: t.name,
    description: t.description ?? '',
    scope: scopeOf(t),
    departmentId: t.departmentId ?? '',
    jobPostingId: t.jobPostingId ?? '',
    isActive: t.isActive,
    criteria: t.criteria.map((c) => ({
      label: c.label,
      guidance: c.guidance ?? '',
      type: c.type,
      isRequired: c.isRequired,
    })),
  };
}

/**
 * The two scope ids are mutually exclusive server-side, so the form sends exactly one and
 * nulls the other — deriving both from a single `scope` choice rather than letting the user
 * populate two fields that contradict each other.
 */
function toRequest(form: FormState): SaveScorecardTemplateRequest {
  return {
    name: form.name.trim(),
    description: form.description.trim() || null,
    departmentId: form.scope === 'department' ? form.departmentId || null : null,
    jobPostingId: form.scope === 'posting' ? form.jobPostingId || null : null,
    isActive: form.isActive,
    criteria: form.criteria
      // A blank row is someone who started typing and changed their mind, not a criterion.
      .filter((c) => c.label.trim().length > 0)
      .map((c) => ({
        label: c.label.trim(),
        guidance: (c.guidance ?? '').trim() || null,
        type: c.type,
        isRequired: c.isRequired,
      })),
  };
}

const field =
  'h-10 w-full rounded-sm border border-line-200 px-3 text-[15px] focus:outline-none focus:ring-2 focus:ring-primary-600';

// ---------------------------------------------------------------------------

function CriteriaBuilder({
  criteria, onChange,
}: {
  criteria: ScorecardCriterionInput[];
  onChange: (next: ScorecardCriterionInput[]) => void;
}) {
  function update(index: number, patch: Partial<ScorecardCriterionInput>) {
    onChange(criteria.map((c, i) => (i === index ? { ...c, ...patch } : c)));
  }

  /** Order is the whole of `sequence` — the API derives it from this list, so gaps and
   *  duplicates cannot be expressed. Same approach as the approval-chain builder. */
  function move(index: number, delta: number) {
    const target = index + delta;
    if (target < 0 || target >= criteria.length) return;
    const next = [...criteria];
    [next[index], next[target]] = [next[target], next[index]];
    onChange(next);
  }

  return (
    <div>
      <p className="mb-2 text-[13px] font-semibold">Criteria</p>

      <ul className="space-y-3">
        {criteria.map((c, i) => (
          <li key={i} className="rounded-sm border border-line-200 p-3">
            <div className="flex gap-3">
              <span className="mt-2.5 font-mono text-[13px] text-ink-400">{i + 1}</span>

              <div className="flex-1 space-y-2">
                <input
                  aria-label={`Criterion ${i + 1} label`}
                  placeholder="What is being assessed"
                  className={field}
                  value={c.label}
                  onChange={(e) => update(i, { label: e.target.value })}
                />
                <input
                  aria-label={`Criterion ${i + 1} guidance`}
                  placeholder="Guidance for the interviewer (optional)"
                  className={field}
                  value={c.guidance ?? ''}
                  onChange={(e) => update(i, { guidance: e.target.value })}
                />

                <div className="flex flex-wrap items-center gap-4">
                  <select
                    aria-label={`Criterion ${i + 1} type`}
                    className="h-10 rounded-sm border border-line-200 px-3 text-[15px]"
                    value={c.type}
                    onChange={(e) => update(i, { type: e.target.value as CriterionType })}
                  >
                    {TYPES.map((t) => <option key={t.value} value={t.value}>{t.label}</option>)}
                  </select>

                  <label className="flex items-center gap-2 text-[15px]">
                    <input
                      type="checkbox"
                      checked={c.isRequired}
                      onChange={(e) => update(i, { isRequired: e.target.checked })}
                    />
                    Required to submit
                  </label>

                  <span className="text-[13px] text-ink-400">
                    {TYPES.find((t) => t.value === c.type)?.hint}
                  </span>
                </div>
              </div>

              <div className="flex flex-col gap-1">
                <button
                  type="button" aria-label={`Move criterion ${i + 1} up`}
                  className="h-7 w-7 rounded-sm border border-line-200 text-[13px] hover:bg-surface-50 disabled:opacity-40"
                  disabled={i === 0} onClick={() => move(i, -1)}
                >
                  ↑
                </button>
                <button
                  type="button" aria-label={`Move criterion ${i + 1} down`}
                  className="h-7 w-7 rounded-sm border border-line-200 text-[13px] hover:bg-surface-50 disabled:opacity-40"
                  disabled={i === criteria.length - 1} onClick={() => move(i, 1)}
                >
                  ↓
                </button>
                <button
                  type="button" aria-label={`Remove criterion ${i + 1}`}
                  className="h-7 w-7 rounded-sm border border-line-200 text-[13px] text-danger-600 hover:bg-surface-50 disabled:opacity-40"
                  disabled={criteria.length === 1}
                  onClick={() => onChange(criteria.filter((_, j) => j !== i))}
                >
                  ×
                </button>
              </div>
            </div>
          </li>
        ))}
      </ul>

      <Button
        variant="secondary"
        type="button"
        onClick={() => onChange([...criteria, { label: '', guidance: '', type: 'Rating', isRequired: true }])}
      >
        Add criterion
      </Button>

      <p className="mt-2 text-[13px] text-ink-400">
        Editing these does not change evaluations already written — each answer keeps the
        wording it was given, so a rename cannot rewrite what someone was asked.
      </p>
    </div>
  );
}

// ---------------------------------------------------------------------------

export function ScorecardTemplatesPage() {
  const role = auth.get()?.role;
  const canEdit = role ? isRecruitmentStaff(role) : false;

  const [templates, setTemplates] = useState<ScorecardTemplate[] | null>(null);
  const [departments, setDepartments] = useState<DepartmentListItem[]>([]);
  const [postings, setPostings] = useState<JobPostingListItem[]>([]);
  const [editingId, setEditingId] = useState<string | 'new' | null>(null);
  const [form, setForm] = useState<FormState | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setTemplates(await api<ScorecardTemplate[]>('/scorecardtemplates'));
  }, []);

  useEffect(() => {
    load().catch((e) =>
      setError(e instanceof Error ? e.message : 'Could not load the templates.'));
  }, [load]);

  // Scope pickers are only meaningful to someone who can write, and both endpoints would
  // otherwise be fetched for a reader who has nothing to do with them.
  useEffect(() => {
    if (!canEdit) return;
    api<DepartmentListItem[]>('/departments').then(setDepartments).catch(() => setDepartments([]));
    api<JobPostingListItem[]>('/jobpostings').then(setPostings).catch(() => setPostings([]));
  }, [canEdit]);

  function startNew() {
    setForm(emptyForm());
    setEditingId('new');
    setError(null);
  }

  function startEdit(t: ScorecardTemplate) {
    setForm(formFrom(t));
    setEditingId(t.id);
    setError(null);
  }

  async function save() {
    if (!form || editingId === null) return;
    setBusy(true);
    setError(null);
    try {
      const body = JSON.stringify(toRequest(form));
      if (editingId === 'new') {
        await api('/scorecardtemplates', { method: 'POST', body });
      } else {
        await api(`/scorecardtemplates/${editingId}`, { method: 'PUT', body });
      }
      await load();
      setEditingId(null);
      setForm(null);
    } catch (e) {
      // A 409 here is almost always "one active template per scope" — the message from the
      // API says which, so it is shown as-is rather than replaced with a guess.
      setError(e instanceof Error ? e.message : 'The template was not saved.');
    } finally {
      setBusy(false);
    }
  }

  if (error && templates === null) return <p role="alert" className="text-danger-600">{error}</p>;
  if (templates === null) return <p className="text-ink-600">Loading…</p>;

  const nameless = form !== null && form.name.trim().length === 0;
  const noCriteria = form !== null && toRequest(form).criteria.length === 0;
  const scopeMissing = form !== null
    && ((form.scope === 'department' && !form.departmentId)
      || (form.scope === 'posting' && !form.jobPostingId));

  return (
    <>
      <header className="mb-6 flex items-start justify-between">
        <div>
          <h1 className="font-display text-2xl font-bold">Scorecard templates</h1>
          <p className="mt-1 max-w-[70ch] text-[13px] text-ink-600">
            An interview is scored against the most specific active template that applies:
            the posting&apos;s own, then its department&apos;s, then the company-wide default.
            Only one template can be active per scope, so there is never a question of which
            one an interviewer will be shown.
          </p>
        </div>
        {canEdit && editingId === null && <Button onClick={startNew}>New template</Button>}
      </header>

      {error && <p role="alert" className="mb-4 text-[15px] text-danger-600">{error}</p>}

      {editingId !== null && form && (
        <Card>
          <form className="space-y-5" onSubmit={(e) => { e.preventDefault(); void save(); }}>
            <div>
              <label htmlFor="name" className="mb-1 block text-[13px] font-semibold">Name</label>
              <input
                id="name" required className={field} value={form.name}
                onChange={(e) => setForm({ ...form, name: e.target.value })}
              />
            </div>

            <div>
              <label htmlFor="desc" className="mb-1 block text-[13px] font-semibold">
                Description <span className="font-normal text-ink-400">(optional)</span>
              </label>
              <input
                id="desc" className={field} value={form.description}
                onChange={(e) => setForm({ ...form, description: e.target.value })}
              />
            </div>

            <div>
              <label htmlFor="scope" className="mb-1 block text-[13px] font-semibold">Applies to</label>
              <select
                id="scope" className={`${field} max-w-sm`} value={form.scope}
                onChange={(e) => setForm({ ...form, scope: e.target.value as Scope })}
              >
                <option value="company">Everything without a more specific template</option>
                <option value="department">One department</option>
                <option value="posting">One job posting</option>
              </select>

              {form.scope === 'department' && (
                <select
                  aria-label="Department"
                  className={`${field} mt-2 max-w-sm`}
                  value={form.departmentId}
                  onChange={(e) => setForm({ ...form, departmentId: e.target.value })}
                >
                  <option value="">Choose a department…</option>
                  {departments.map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
                </select>
              )}

              {form.scope === 'posting' && (
                <select
                  aria-label="Job posting"
                  className={`${field} mt-2 max-w-sm`}
                  value={form.jobPostingId}
                  onChange={(e) => setForm({ ...form, jobPostingId: e.target.value })}
                >
                  <option value="">Choose a posting…</option>
                  {postings.map((p) => (
                    <option key={p.id} value={p.id}>{p.title} · {p.departmentName}</option>
                  ))}
                </select>
              )}
            </div>

            <label className="flex items-center gap-2 text-[15px]">
              <input
                type="checkbox" checked={form.isActive}
                onChange={(e) => setForm({ ...form, isActive: e.target.checked })}
              />
              Active
            </label>
            <p className="-mt-3 text-[13px] text-ink-400">
              Deactivating is how a template is retired — it stays attached to the evaluations
              already written against it, so nothing in the record is lost.
            </p>

            <CriteriaBuilder
              criteria={form.criteria}
              onChange={(criteria) => setForm({ ...form, criteria })}
            />

            <div className="flex gap-3">
              <Button type="submit" disabled={busy || nameless || noCriteria || scopeMissing}>
                {busy ? 'Saving…' : 'Save template'}
              </Button>
              <Button
                variant="secondary" type="button"
                onClick={() => { setEditingId(null); setForm(null); setError(null); }}
              >
                Cancel
              </Button>
            </div>
          </form>
        </Card>
      )}

      {editingId === null && (
        templates.length === 0 ? (
          <Card>
            <p className="text-[15px] text-ink-600">
              No templates yet. Until one exists, an interview has no criteria and the
              scorecard form has nothing to show.
            </p>
          </Card>
        ) : (
          <div className="space-y-3">
            {templates.map((t) => (
              <Card key={t.id}>
                <div className="flex items-start justify-between gap-4">
                  <div>
                    <div className="flex items-center gap-2">
                      <h2 className="text-[15px] font-semibold">{t.name}</h2>
                      {!t.isActive && (
                        <span className="text-[13px] text-ink-400">inactive</span>
                      )}
                    </div>
                    <p className="mt-0.5 text-[13px] text-ink-600">{scopeLabel(t, postings)}</p>
                    {t.description && (
                      <p className="mt-1 max-w-[60ch] text-[13px] text-ink-600">{t.description}</p>
                    )}

                    <ol className="mt-2 space-y-0.5 text-[13px] text-ink-600">
                      {t.criteria.map((c) => (
                        <li key={c.id}>
                          <span className="font-mono text-ink-400">{c.sequence}.</span> {c.label}
                          <span className="text-ink-400">
                            {' · '}{TYPES.find((x) => x.value === c.type)?.label}
                            {c.isRequired ? '' : ' · optional'}
                          </span>
                        </li>
                      ))}
                    </ol>
                  </div>

                  {canEdit && (
                    <Button variant="secondary" onClick={() => startEdit(t)}>Edit</Button>
                  )}
                </div>
              </Card>
            ))}
          </div>
        )
      )}
    </>
  );
}
