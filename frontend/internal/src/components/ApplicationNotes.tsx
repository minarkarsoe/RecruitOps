import { useCallback, useEffect, useState } from 'react';
import { Button } from '@recruitops/ui';
import type { CreateNoteRequest, Interview, Note } from '@recruitops/types';
import { api } from '../lib/api';
import { auth } from '../lib/auth';

/**
 * The debrief thread on one job application (Module 3.4).
 *
 * Panel members can read and post here even from another department — that is what their
 * participation grant is for (ADR-0017 §4), and the API deliberately gates this on reach
 * rather than on `CanWrite`.
 */

/**
 * Renders a note body.
 *
 * `bodyHtml` arrives **already escaped by the server**: `MentionParser.ToSafeHtml` escapes
 * every character of the author's text and only then inserts the mention markup it generated
 * itself, so no path exists by which body text becomes an element. Injecting it is therefore
 * the correct thing to do here, and it is the reason the field exists at all — "escape on
 * output" is meant to be the default path rather than something each renderer remembers.
 *
 * Two things not to do:
 *  - do not escape it again: `&lt;` would render as literal `&amp;lt;` and a Myanmar note
 *    containing `<` would look mangled to the person who wrote it;
 *  - do not build your own HTML from `body`. `body` is the raw text, unescaped and safe only
 *    in JSON. It is here for editing and copying, not for the DOM.
 */
function NoteBody({ html }: { html: string }) {
  return (
    <div
      className="max-w-[60ch] whitespace-pre-wrap text-[15px] leading-relaxed"
      // eslint-disable-next-line react/no-danger -- server-escaped; see the note above.
      dangerouslySetInnerHTML={{ __html: html }}
    />
  );
}

function timestamp(iso: string): string {
  const d = new Date(iso);
  const today = new Date();
  const sameDay = d.toDateString() === today.toDateString();
  return sameDay
    ? d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
    : d.toLocaleString([], { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' });
}

export function ApplicationNotes({
  applicationId, interviews, pinnedTo,
}: {
  applicationId: string;
  /** Rounds this note can be pinned to. Omit to hide the picker entirely. */
  interviews?: Interview[];
  /** Pre-select (and lock) a round — used on the interview page, where the round is the context. */
  pinnedTo?: string;
}) {
  const myUserId = auth.get()?.userId;

  const [notes, setNotes] = useState<Note[] | null>(null);
  const [body, setBody] = useState('');
  const [interviewId, setInterviewId] = useState<string>(pinnedTo ?? '');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setNotes(await api<Note[]>(`/applications/${applicationId}/notes`));
  }, [applicationId]);

  useEffect(() => {
    load().catch((e) =>
      setError(e instanceof Error ? e.message : 'Could not load the thread.'));
  }, [load]);

  async function post() {
    if (!body.trim()) return;
    setBusy(true);
    setError(null);
    try {
      const request: CreateNoteRequest = {
        body: body.trim(),
        interviewId: interviewId || null,
      };
      await api(`/applications/${applicationId}/notes`, {
        method: 'POST', body: JSON.stringify(request),
      });
      setBody('');
      await load();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'The note was not posted.');
    } finally {
      setBusy(false);
    }
  }

  const roundLabel = (id: string | null) =>
    id ? interviews?.find((i) => i.id === id)?.round : undefined;

  if (error && notes === null) {
    return <p role="alert" className="text-[13px] text-danger-600">{error}</p>;
  }
  if (notes === null) return <p className="text-[13px] text-ink-600">Loading notes…</p>;

  const visible = pinnedTo ? notes.filter((n) => n.interviewId === pinnedTo) : notes;

  return (
    <div className="mt-3 rounded-sm border border-line-200 p-4">
      <h3 className="mb-3 text-[13px] font-semibold uppercase tracking-wide text-ink-600">
        Notes · {visible.length}
      </h3>

      {error && <p role="alert" className="mb-2 text-[13px] text-danger-600">{error}</p>}

      {visible.length === 0 ? (
        <p className="text-[13px] text-ink-600">Nothing yet.</p>
      ) : (
        <ul className="space-y-4">
          {visible.map((note) => {
            const round = roundLabel(note.interviewId);
            return (
              <li key={note.id}>
                <div className="flex items-baseline gap-2">
                  <span className="text-[15px] font-semibold">
                    {note.authorUserId === myUserId ? 'You' : note.authorName}
                  </span>
                  <span className="text-[13px] text-ink-400">{timestamp(note.createdAt)}</span>
                  {round !== undefined && (
                    <span className="text-[13px] text-ink-400">· round {round}</span>
                  )}
                </div>

                <NoteBody html={note.bodyHtml} />

                {/* Only what the server actually resolved. A handle that matched nobody the
                    author can see is a silent no-op by design (ADR-0018), so listing what we
                    hoped for would be a promise the system did not make. */}
                {note.mentions.length > 0 && (
                  <p className="mt-1 text-[13px] text-ink-400">
                    Mentioned: {note.mentions.map((m) => m.displayName).join(', ')}
                  </p>
                )}
              </li>
            );
          })}
        </ul>
      )}

      <form
        className="mt-4 space-y-2"
        onSubmit={(e) => { e.preventDefault(); void post(); }}
      >
        <label htmlFor={`note-${applicationId}`} className="sr-only">Add a note</label>
        <textarea
          id={`note-${applicationId}`}
          rows={3}
          placeholder="Add a note. Type @name to mention a colleague."
          className="w-full rounded-sm border border-line-200 p-3 text-[15px] focus:outline-none focus:ring-2 focus:ring-primary-600"
          value={body}
          onChange={(e) => setBody(e.target.value)}
        />

        <div className="flex items-center gap-3">
          <Button type="submit" disabled={busy || !body.trim()}>
            {busy ? 'Posting…' : 'Post note'}
          </Button>

          {/* Hidden when the round is already the context — offering a picker there would
              only let someone contradict the page they are on. */}
          {!pinnedTo && interviews && interviews.length > 0 && (
            <select
              aria-label="Pin to a round"
              className="h-10 rounded-sm border border-line-200 px-3 text-[13px]"
              value={interviewId}
              onChange={(e) => setInterviewId(e.target.value)}
            >
              <option value="">Not about a specific round</option>
              {interviews.map((i) => (
                <option key={i.id} value={i.id}>Round {i.round}</option>
              ))}
            </select>
          )}
        </div>

        <p className="text-[13px] text-ink-400">
          A mention only reaches someone who can already see this candidate — anyone else stays
          plain text.
        </p>
      </form>
    </div>
  );
}
