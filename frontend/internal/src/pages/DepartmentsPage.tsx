import { useCallback, useEffect, useState } from 'react';
import { Button, Card } from '@recruitops/ui';
import type {
  CreateDepartmentRequest, DepartmentDetail, DepartmentMember,
} from '@recruitops/types';
import { api } from '../lib/api';

/**
 * Department admin (Admin only).
 *
 * Two things happen here and they are not equally obvious: naming departments, and deciding
 * who can see the requisitions inside them. The second is the access-control axis of the
 * whole product (ADR-0003), so member editing is on the same screen rather than hidden
 * behind a settings page nobody opens.
 */
export function DepartmentsPage() {
  const [items, setItems] = useState<DepartmentDetail[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const [creating, setCreating] = useState(false);
  const [form, setForm] = useState<CreateDepartmentRequest>({ name: '', code: '' });

  const [editingId, setEditingId] = useState<string | null>(null);
  const [editForm, setEditForm] = useState<CreateDepartmentRequest>({ name: '', code: '' });

  const [membersFor, setMembersFor] = useState<string | null>(null);
  const [members, setMembers] = useState<DepartmentMember[]>([]);

  const load = useCallback(async () => {
    setItems(await api<DepartmentDetail[]>('/departments/admin'));
  }, []);

  useEffect(() => {
    load().catch((e) => setError(e instanceof Error ? e.message : 'Could not load departments.'));
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

  async function openMembers(id: string) {
    if (membersFor === id) { setMembersFor(null); return; }
    setError(null);
    try {
      setMembers(await api<DepartmentMember[]>(`/departments/${id}/members`));
      setMembersFor(id);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Could not load members.');
    }
  }

  const saveMembers = (id: string) =>
    act(async () => {
      const userIds = members.filter((m) => m.isMember).map((m) => m.userId);
      setMembers(await api<DepartmentMember[]>(`/departments/${id}/members`, {
        method: 'PUT', body: JSON.stringify({ userIds }),
      }));
    });

  const field = 'h-10 w-full rounded-sm border border-line-200 px-3 focus:outline-none focus:ring-2 focus:ring-primary-600';

  return (
    <>
      <div className="mb-6 flex items-center justify-between">
        <h1 className="font-display text-2xl font-bold">Departments</h1>
        {!creating && <Button onClick={() => setCreating(true)}>New department</Button>}
      </div>

      {error && <p role="alert" className="mb-4 text-[15px] text-danger-600">{error}</p>}

      {creating && (
        <div className="mb-6">
          <Card>
            <form
              className="space-y-4"
              onSubmit={(e) => {
                e.preventDefault();
                act(async () => {
                  await api('/departments', { method: 'POST', body: JSON.stringify(form) });
                  setForm({ name: '', code: '' });
                  setCreating(false);
                });
              }}
            >
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label htmlFor="name" className="mb-1 block text-[13px] font-semibold">Name</label>
                  <input id="name" required minLength={2} maxLength={200} className={field}
                    value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} />
                </div>
                <div>
                  <label htmlFor="code" className="mb-1 block text-[13px] font-semibold">
                    Code <span className="font-normal text-ink-400">(optional)</span>
                  </label>
                  <input id="code" maxLength={50} className={field}
                    value={form.code ?? ''} onChange={(e) => setForm({ ...form, code: e.target.value })} />
                </div>
              </div>
              <div className="flex gap-3">
                <Button type="submit" disabled={busy}>{busy ? 'Creating…' : 'Create'}</Button>
                <Button variant="secondary" type="button" onClick={() => setCreating(false)}>Cancel</Button>
              </div>
            </form>
          </Card>
        </div>
      )}

      {items === null && !error && <p className="text-ink-600">Loading…</p>}

      {items && (
        <Card>
          {items.length === 0 ? (
            <p className="text-[15px] text-ink-600">
              No departments yet. Requisitions are owned by a department, so at least one is
              needed before anyone can raise work.
            </p>
          ) : (
            <ul className="divide-y divide-line-200">
              {items.map((d) => (
                <li key={d.id} className="py-3">
                  {editingId === d.id ? (
                    <form
                      className="flex items-end gap-3"
                      onSubmit={(e) => {
                        e.preventDefault();
                        act(async () => {
                          await api(`/departments/${d.id}`, {
                            method: 'PUT', body: JSON.stringify(editForm),
                          });
                          setEditingId(null);
                        });
                      }}
                    >
                      <div className="flex-1">
                        <label className="mb-1 block text-[13px] font-semibold">Name</label>
                        <input required minLength={2} maxLength={200} className={field}
                          value={editForm.name}
                          onChange={(e) => setEditForm({ ...editForm, name: e.target.value })} />
                      </div>
                      <div className="w-40">
                        <label className="mb-1 block text-[13px] font-semibold">Code</label>
                        <input maxLength={50} className={field}
                          value={editForm.code ?? ''}
                          onChange={(e) => setEditForm({ ...editForm, code: e.target.value })} />
                      </div>
                      <Button type="submit" disabled={busy}>Save</Button>
                      <Button variant="secondary" type="button" onClick={() => setEditingId(null)}>
                        Cancel
                      </Button>
                    </form>
                  ) : (
                    <div className="flex items-center justify-between gap-4">
                      <div>
                        <p className="font-semibold">
                          {d.name}
                          {!d.isActive && (
                            <span className="ml-2 text-[13px] font-normal text-ink-400">(inactive)</span>
                          )}
                        </p>
                        <p className="text-[13px] text-ink-600">
                          <span className="font-mono">{d.code ?? '—'}</span>
                          {' · '}
                          {d.memberCount} {d.memberCount === 1 ? 'member' : 'members'}
                          {d.openRequisitionCount > 0 && ` · ${d.openRequisitionCount} in progress`}
                        </p>
                      </div>
                      <div className="flex shrink-0 gap-2">
                        <Button variant="ghost" onClick={() => openMembers(d.id)}>
                          {membersFor === d.id ? 'Hide members' : 'Members'}
                        </Button>
                        <Button
                          variant="secondary"
                          onClick={() => {
                            setEditingId(d.id);
                            setEditForm({ name: d.name, code: d.code ?? '' });
                          }}
                        >
                          Rename
                        </Button>
                        {d.isActive ? (
                          <Button
                            variant="danger"
                            disabled={busy}
                            onClick={() => act(() => api(`/departments/${d.id}/deactivate`, { method: 'POST' }))}
                          >
                            Deactivate
                          </Button>
                        ) : (
                          <Button
                            variant="secondary"
                            disabled={busy}
                            onClick={() => act(() => api(`/departments/${d.id}/activate`, { method: 'POST' }))}
                          >
                            Reactivate
                          </Button>
                        )}
                      </div>
                    </div>
                  )}

                  {membersFor === d.id && (
                    <div className="mt-3 rounded-sm border border-line-200 p-3">
                      <p className="mb-1 text-[13px] font-semibold">Who can see this department</p>
                      <p className="mb-3 text-[13px] text-ink-600">
                        Hiring Managers see only the departments they belong to. Admin, HR
                        Director and Recruiter see every department regardless of this list.
                      </p>
                      <ul className="mb-3 space-y-1">
                        {members.map((m) => (
                          <li key={m.userId}>
                            <label className="flex items-center gap-2 text-[15px]">
                              <input
                                type="checkbox" checked={m.isMember}
                                onChange={(e) =>
                                  setMembers(members.map((x) =>
                                    x.userId === m.userId ? { ...x, isMember: e.target.checked } : x))
                                }
                              />
                              {m.displayName}
                              <span className="text-[13px] text-ink-400">{m.role} · {m.email}</span>
                            </label>
                          </li>
                        ))}
                      </ul>
                      <Button onClick={() => saveMembers(d.id)} disabled={busy}>
                        {busy ? 'Saving…' : 'Save members'}
                      </Button>
                    </div>
                  )}
                </li>
              ))}
            </ul>
          )}
        </Card>
      )}
    </>
  );
}
