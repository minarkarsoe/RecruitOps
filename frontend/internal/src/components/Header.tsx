import { Breadcrumbs } from './Breadcrumbs';
import { auth, hasPermission } from '../lib/auth';
import { useNavigate } from 'react-router-dom';

// Built against the top bar in `design/internal/board.html` (ADR-0025):
// `h-14 bg-white border-b border-line`, place on the left, actions on the right.
//
// ⚠️ No user badge here any more. The kit puts identity in the nav rail's footer and nowhere
// else — two avatars on one screen is two places to check who you are signed in as, and they
// can disagree while a session is being replaced.

interface HeaderProps {
  onOpenCommandPalette: () => void;
}

export function Header({ onOpenCommandPalette }: HeaderProps) {
  const session = auth.get();
  const navigate = useNavigate();
  const canCreateReq = hasPermission(session, 'permission:requisitions:requisitions:create');

  return (
    <header className="sticky top-0 z-20 flex h-14 shrink-0 items-center gap-4 border-b border-line bg-white px-5">
      <div className="flex min-w-0 items-center">
        <Breadcrumbs />
      </div>

      <div className="ml-auto flex shrink-0 items-center gap-2">
        <button
          type="button"
          onClick={onOpenCommandPalette}
          aria-label="Search commands"
          className="flex h-8 items-center gap-2 rounded-md border border-line bg-canvas px-2.5 text-sm
            text-ink-500 transition-colors hover:border-line-strong hover:text-ink-900 lg:w-56"
        >
          <svg className="h-3.5 w-3.5 shrink-0" viewBox="0 0 16 16" fill="none" aria-hidden="true">
            <circle cx="7" cy="7" r="4.5" stroke="currentColor" strokeWidth="1.4" />
            <path d="M10.5 10.5L14 14" stroke="currentColor" strokeWidth="1.4" strokeLinecap="round" />
          </svg>
          <span className="hidden sm:inline">Search or jump to…</span>
          {/* Auto margin, not a spacer: the label is hidden below `sm` and a fixed gap would
              leave the shortcut floating in the middle of an otherwise empty control. */}
          <kbd className="ml-auto hidden rounded border border-line bg-white px-1.5 py-0.5 font-mono text-2xs lg:inline">
            Ctrl+K
          </kbd>
        </button>

        {canCreateReq && (
          <button
            type="button"
            onClick={() => navigate('/requisitions/new')}
            className="hidden h-9 items-center gap-1.5 rounded-md bg-brand-700 px-3.5 text-base font-medium
              text-white transition-colors hover:bg-brand-800 active:bg-brand-900
              focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-700 focus-visible:ring-offset-2
              md:inline-flex"
          >
            <svg className="h-3.5 w-3.5" viewBox="0 0 16 16" fill="none" aria-hidden="true">
              <path d="M8 3v10M3 8h10" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" />
            </svg>
            {/* The label names the outcome, not the verb — the kit's rule for every button. */}
            New requisition
          </button>
        )}
      </div>
    </header>
  );
}
