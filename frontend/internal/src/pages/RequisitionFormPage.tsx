import { useEffect, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { Button, Card } from '@recruitops/ui';
import type {
  CreateRequisitionRequest, DepartmentListItem, JdTemplate, RequisitionDetail,
} from '@recruitops/types';
import { api } from '../lib/api';

/**
 * Create and edit share one component because they are the same form against the same
 * fields — the only differences are where the initial values come from and which verb
 * is sent. Duplicating it would guarantee the two drift the first time a field is added.
 *
 * Editing is only offered on a Draft; the backend returns 409 otherwise, because once a
 * requisition is submitted the approvers are deciding on its contents.
 */
export function RequisitionFormPage({ mode }: { mode: 'create' | 'edit' }) {
  const navigate = useNavigate();
  const { id } = useParams<{ id: string }>();
  const isEdit = mode === 'edit';

  const [departments, setDepartments] = useState<DepartmentListItem[]>([]);
  const [templates, setTemplates] = useState<JdTemplate[]>([]);
  const [form, setForm] = useState<CreateRequisitionRequest>({
    departmentId: '', title: '', jobDescription: '', headcount: 1, salaryBudget: null,
  });
  const [loading, setLoading] = useState(isEdit);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    // A Hiring Manager only gets their own departments back, so the picker is
    // already scoped by the API — no client-side filtering needed (ADR-0003).
    api<DepartmentListItem[]>('/departments').then(setDepartments).catch(() => setDepartments([]));
    api<JdTemplate[]>('/jdtemplates').then(setTemplates).catch(() => setTemplates([]));
  }, []);

  useEffect(() => {
    if (!isEdit || !id) return;
    api<RequisitionDetail>(`/requisitions/${id}`)
      .then((r) => {
        setForm({
          departmentId: r.departmentId,
          title: r.title,
          jobDescription: r.jobDescription,
          headcount: r.headcount,
          salaryBudget: r.salaryBudget,
        });
        // Bounce rather than let someone fill in a form the API will reject with 409.
        if (r.status !== 'Draft') {
          setError(`This requisition is ${r.status} and can no longer be edited.`);
        }
      })
      .catch((e) => setError(e instanceof Error ? e.message : 'Could not load the requisition.'))
      .finally(() => setLoading(false));
  }, [isEdit, id]);

  function applyTemplate(templateId: string) {
    const t = templates.find((x) => x.id === templateId);
    if (t) setForm((f) => ({ ...f, title: f.title || t.title, jobDescription: t.content }));
  }

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      const saved = isEdit
        ? await api<RequisitionDetail>(`/requisitions/${id}`, {
            method: 'PUT', body: JSON.stringify(form),
          })
        : await api<RequisitionDetail>('/requisitions', {
            method: 'POST', body: JSON.stringify(form),
          });
      navigate(`/requisitions/${saved.id}`);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not save the requisition.');
    } finally {
      setBusy(false);
    }
  }

  const field = 'h-10 w-full rounded-md border border-line px-3 focus:outline-none focus:ring-2 focus:ring-brand-700';

  if (loading) return <p className="text-ink-600">Loading…</p>;

  return (
    <>
      <header className="mb-6">
        {isEdit && (
          <Link to={`/requisitions/${id}`} className="mb-2 inline-block text-sm text-brand-700 hover:underline">
            ← Back to requisition
          </Link>
        )}
        <h1 className="text-xl font-semibold tracking-tight">
          {isEdit ? 'Edit draft' : 'New requisition'}
        </h1>
      </header>

      {error && <p role="alert" className="mb-4 text-md text-critical-700">{error}</p>}

      <Card>
        <form onSubmit={submit} className="space-y-4">
          <div>
            <label htmlFor="department" className="mb-1 block text-sm font-semibold">Department</label>
            <select
              id="department" required className={field} value={form.departmentId}
              onChange={(e) => setForm({ ...form, departmentId: e.target.value })}
            >
              <option value="">Select a department…</option>
              {departments.map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
            </select>
          </div>

          {templates.length > 0 && (
            <div>
              <label htmlFor="template" className="mb-1 block text-sm font-semibold">
                {isEdit ? 'Replace the JD from a template' : 'Start from a JD template'}{' '}
                <span className="font-normal text-ink-400">(optional)</span>
              </label>
              <select id="template" className={field} onChange={(e) => applyTemplate(e.target.value)}>
                <option value="">None</option>
                {templates.map((t) => <option key={t.id} value={t.id}>{t.title}</option>)}
              </select>
            </div>
          )}

          <div>
            <label htmlFor="title" className="mb-1 block text-sm font-semibold">Position title</label>
            <input
              id="title" required minLength={2} maxLength={200} className={field}
              value={form.title} onChange={(e) => setForm({ ...form, title: e.target.value })}
            />
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div>
              <label htmlFor="headcount" className="mb-1 block text-sm font-semibold">Headcount</label>
              <input
                id="headcount" type="number" min={1} max={1000} required className={field}
                value={form.headcount}
                onChange={(e) => setForm({ ...form, headcount: Number(e.target.value) })}
              />
            </div>
            <div>
              <label htmlFor="salary" className="mb-1 block text-sm font-semibold">
                Salary budget <span className="font-normal text-ink-400">(optional)</span>
              </label>
              <input
                id="salary" type="number" min={0} className={field}
                value={form.salaryBudget ?? ''}
                onChange={(e) =>
                  setForm({ ...form, salaryBudget: e.target.value ? Number(e.target.value) : null })
                }
              />
            </div>
          </div>

          <div>
            <label htmlFor="jd" className="mb-1 block text-sm font-semibold">Job description</label>
            <textarea
              id="jd" rows={8} required
              className="w-full rounded-md border border-line p-3 focus:outline-none focus:ring-2 focus:ring-brand-700"
              value={form.jobDescription}
              onChange={(e) => setForm({ ...form, jobDescription: e.target.value })}
            />
          </div>

          <Button type="submit" disabled={busy}>
            {busy ? 'Saving…' : isEdit ? 'Save changes' : 'Create draft'}
          </Button>
        </form>
      </Card>
    </>
  );
}
