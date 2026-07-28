import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import type { Note } from '@recruitops/types';
import { ApplicationNotes } from './ApplicationNotes';
import { note } from '../test/fixtures';

/*
 * `bodyHtml` is injected with `dangerouslySetInnerHTML`, which is correct here and is the
 * reason the field exists — `MentionParser.ToSafeHtml` escapes the author's text and only
 * then inserts the mention markup it generated itself.
 *
 * Both ways of getting this wrong are silent. Escaping it again renders `&amp;lt;` at the
 * reader; rebuilding from `body` reopens the hole the field was added to close. Neither
 * throws, and neither is visible until somebody reads a mangled note — so they are pinned
 * here.
 */

const { apiMock } = vi.hoisted(() => ({ apiMock: vi.fn() }));

vi.mock('../lib/api', async () => {
  const actual = await vi.importActual<typeof import('../lib/api')>('../lib/api');
  return { ...actual, api: apiMock };
});

function serve(notes: Note[]) {
  apiMock.mockImplementation(() => Promise.resolve(notes));
}

describe('NoteBody', () => {
  it('renders server-generated mention markup as an element, not as text', async () => {
    serve([note({
      body: '@bo.bo take a look',
      bodyHtml: '<span class="mention">@bo.bo</span> take a look',
      mentions: [{ userId: 'u-other', displayName: 'Bo Bo' }],
    })]);

    const { container } = render(<ApplicationNotes applicationId="app-1" />);
    expect(await screen.findByText('@bo.bo')).toBeInTheDocument();

    // The class is styled in `index.css`, not as a Tailwind utility — the markup comes from
    // C# so the content scanner cannot see it. If this element stops existing, the style
    // has nothing to attach to either.
    expect(container.querySelector('span.mention')).not.toBeNull();
  });

  it('does not escape what the server already escaped', async () => {
    // The server sends `<script>` as `&lt;script&gt;`; the DOM decodes that to the literal
    // text `<script>`. Escaping it again would put `&lt;script&gt;` on the screen.
    serve([note({
      body: '<script>alert(1)</script> — see the note',
      bodyHtml: '&lt;script&gt;alert(1)&lt;/script&gt; — see the note',
    })]);

    const { container } = render(<ApplicationNotes applicationId="app-1" />);
    expect(await screen.findByText(/<script>alert\(1\)<\/script>/)).toBeInTheDocument();
    expect(container.querySelector('script')).toBeNull();
    expect(container.textContent).not.toContain('&lt;');
  });

  it('renders Burmese text untouched', async () => {
    // The escape/unescape round trip is where a non-Latin body would get mangled, and the
    // person who would notice is the one who wrote it.
    serve([note({ body: 'အင်တာဗျူး ကောင်းတယ်', bodyHtml: 'အင်တာဗျူး ကောင်းတယ်' })]);

    render(<ApplicationNotes applicationId="app-1" />);
    expect(await screen.findByText('အင်တာဗျူး ကောင်းတယ်')).toBeInTheDocument();
  });

  it('lists only the mentions the server actually resolved', async () => {
    // A handle matching nobody the author can reach is a silent no-op by design
    // (ADR-0018). Listing what the author *hoped for* would promise something the system
    // did not do.
    serve([note({
      body: '@bo.bo @finance.approver',
      bodyHtml: '<span class="mention">@bo.bo</span> @finance.approver',
      mentions: [{ userId: 'u-other', displayName: 'Bo Bo' }],
    })]);

    render(<ApplicationNotes applicationId="app-1" />);
    expect(await screen.findByText('Mentioned: Bo Bo')).toBeInTheDocument();
  });
});

describe('the thread', () => {
  it('shows only this round when pinned to one', async () => {
    serve([
      note({ id: 'n-1', interviewId: 'iv-1', body: 'about round 1', bodyHtml: 'about round 1' }),
      note({ id: 'n-2', interviewId: null, body: 'general', bodyHtml: 'general' }),
    ]);

    render(<ApplicationNotes applicationId="app-1" pinnedTo="iv-1" />);
    expect(await screen.findByText('about round 1')).toBeInTheDocument();
    expect(screen.queryByText('general')).not.toBeInTheDocument();
    expect(screen.getByText('Notes · 1')).toBeInTheDocument();
  });

  it('hides the round picker when the round is already the context', async () => {
    serve([]);
    render(<ApplicationNotes applicationId="app-1" pinnedTo="iv-1" />);

    await screen.findByText('Nothing yet.');
    expect(screen.queryByLabelText('Pin to a round')).not.toBeInTheDocument();
  });
});
