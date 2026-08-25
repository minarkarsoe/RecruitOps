import { useEffect, useState } from 'react';
import { Button, Card } from '@recruitops/ui';
import type { CreateJdTemplateRequest, DepartmentListItem, JdTemplate } from '@recruitops/types';
import { api } from '../lib/api';

const EMPTY_FORM: CreateJdTemplateRequest = {
  title: '',
  content: '',
  departmentId: null,
};

export function JdTemplatesPage() {
  const [templates, setTemplates] = useState<JdTemplate[] | null>(null);
  const [departments, setDepartments] = useState<DepartmentListItem[]>([]);
  const [showForm, setShowForm] = useState(false);
  const [form, setForm] = useState<CreateJdTemplateRequest>(EMPTY_FORM);
  const [expanded, setExpanded] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    api<JdTemplate[]>('/jdtemplates').then(setTemplates).catch(() => setTemplates([]));
    api<DepartmentListItem[]>('/departments').then(setDepartments).catch(() => {});
  }, []);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      const created = await api<JdTemplate>('/jdtemplates', {
        method: 'POST',
        body: JSON.stringify(form),
      });
      setTemplates((prev) => [...(prev ?? []), created]);
      setForm(EMPTY_FORM);
      setShowForm(false);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not create template.');
    } finally {
      setBusy(false);
    }
  }

  const field = 'h-10 w-full rounded-md border border-line px-3 focus:outline-none focus:ring-2 focus:ring-brand-700 text-md';

  return (
    <>
      <header className="mb-6 flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold tracking-tight">JD templates</h1>
          <p className="mt-1 text-sm text-ink-400">
            Reusable job description templates to speed up requisition drafting.
          </p>
        </div>
        {!showForm && (
          <Button onClick={() => setShowForm(true)}>New template</Button>
        )}
      </header>

      {/* ── Create form ── */}
      {showForm && (
        <Card>
          <h2 className="mb-4 text-base font-semibold">
            New JD template
          </h2>
          {error && <p role="alert" className="mb-3 text-sm text-critical-700">{error}</p>}
          <form onSubmit={submit} className="space-y-4">
            <div className="grid grid-cols-2 gap-4">
              <div>
                <label className="mb-1 block text-sm font-semibold">Template title</label>
                <input required className={field} placeholder="e.g. Software Engineer"
                  value={form.title} onChange={(e) => setForm({ ...form, title: e.target.value })} />
              </div>
              <div>
                <label className="mb-1 block text-sm font-semibold">
                  Department <span className="font-normal text-ink-400">(empty = all departments)</span>
                </label>
                <select className={field}
                  value={form.departmentId ?? ''}
                  onChange={(e) => setForm({ ...form, departmentId: e.target.value || null })}>
                  <option value="">All departments</option>
                  {departments.map((d) => (
                    <option key={d.id} value={d.id}>{d.name}</option>
                  ))}
                </select>
              </div>
            </div>

            <div>
              <label className="mb-1 block text-sm font-semibold">Job description content</label>
              <textarea
                required rows={10}
                className="w-full rounded-md border border-line p-3 text-md focus:outline-none focus:ring-2 focus:ring-brand-700"
                placeholder="Describe the role, responsibilities, and requirements…"
                value={form.content}
                onChange={(e) => setForm({ ...form, content: e.target.value })}
              />
            </div>

            <div className="flex gap-3">
              <Button type="submit" disabled={busy}>
                {busy ? 'Creating…' : 'Create template'}
              </Button>
              <Button variant="ghost" type="button"
                onClick={() => { setShowForm(false); setError(null); }}>
                Cancel
              </Button>
            </div>
          </form>
        </Card>
      )}

      {/* ── Template list ── */}
      {templates === null && <p className="mt-6 text-ink-600">Loading…</p>}
      {templates?.length === 0 && !showForm && (
        <Card>
          <div className="py-6 text-center">
            <h3 className="text-base font-semibold">No templates yet</h3>
            <p className="mt-1 text-sm text-ink-600">
              Create a template to prefill job descriptions when raising requisitions.
            </p>
          </div>
        </Card>
      )}
      {templates && templates.length > 0 && (
        <div className="mt-6 space-y-3">
          {templates.map((t) => (
            <Card key={t.id}>
              <div className="flex items-start justify-between">
                <div>
                  <p className="font-semibold">{t.title}</p>
                  <p className="text-sm text-ink-400">
                    {t.departmentId
                      ? departments.find((d) => d.id === t.departmentId)?.name ?? 'Specific department'
                      : 'All departments'
                    }
                    {t.isActive ? '' : ' · Inactive'}
                  </p>
                </div>
                <button
                  onClick={() => setExpanded(expanded === t.id ? null : t.id)}
                  className="text-sm font-semibold text-brand-700 hover:text-brand-700"
                >
                  {expanded === t.id ? 'Hide' : 'Preview'}
                </button>
              </div>
              {expanded === t.id && (
                <pre className="mt-3 whitespace-pre-wrap rounded-md bg-canvas p-3 text-sm leading-relaxed text-ink-600">
                  {t.content}
                </pre>
              )}
            </Card>
          ))}
        </div>
      )}
    </>
  );
}
