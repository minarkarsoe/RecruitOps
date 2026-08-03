import { Breadcrumbs } from './Breadcrumbs';
import { auth, hasPermission } from '../lib/auth';
import { useNavigate } from 'react-router-dom';

interface HeaderProps {
  onOpenCommandPalette: () => void;
}

export function Header({ onOpenCommandPalette }: HeaderProps) {
  const session = auth.get();
  const navigate = useNavigate();
  const canCreateReq = hasPermission(session, 'permission:requisitions:requisitions:create');

  return (
    <header className="h-14 border-b border-line-200 bg-surface-0 px-6 flex items-center justify-between gap-4 sticky top-0 z-20 shadow-xs">
      {/* Left: Dynamic Route Breadcrumbs */}
      <div className="flex items-center min-w-0">
        <Breadcrumbs />
      </div>

      {/* Right: Search / Command Palette trigger + Quick Info & Actions */}
      <div className="flex items-center gap-3 shrink-0">
        {/* Command Palette Search Trigger Button */}
        <button
          type="button"
          onClick={onOpenCommandPalette}
          aria-label="Search commands"
          className="flex items-center gap-2 px-3 py-1.5 rounded-md border border-line-200 bg-surface-50 hover:bg-surface-100 text-ink-500 hover:text-ink-900 text-xs transition-colors shadow-xs group"
        >
          <svg
            className="h-3.5 w-3.5 text-ink-400 group-hover:text-ink-600 transition-colors"
            fill="none"
            viewBox="0 0 24 24"
            stroke="currentColor"
            strokeWidth="2"
          >
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z"
            />
          </svg>
          <span className="hidden sm:inline">Search or jump to...</span>
          <kbd className="font-mono text-[10px] font-semibold text-ink-400 bg-surface-0 border border-line-200 px-1.5 py-0.5 rounded shadow-2xs">
            Ctrl+K
          </kbd>
        </button>

        {/* Quick Action Button (if permitted) */}
        {canCreateReq && (
          <button
            type="button"
            onClick={() => navigate('/requisitions/new')}
            className="hidden md:inline-flex items-center gap-1 px-3 py-1.5 rounded-md bg-primary-600 hover:bg-primary-700 text-white text-xs font-semibold shadow-xs transition-colors"
          >
            <svg
              className="h-3.5 w-3.5"
              fill="none"
              viewBox="0 0 24 24"
              stroke="currentColor"
              strokeWidth="2.5"
            >
              <path strokeLinecap="round" strokeLinejoin="round" d="M12 4.5v15m7.5-7.5h-15" />
            </svg>
            <span>New Requisition</span>
          </button>
        )}

        {/* User / Department Badge */}
        {session && (
          <div className="flex items-center gap-2 pl-2 border-l border-line-200 text-xs">
            <div className="h-7 w-7 rounded-full bg-primary-100 text-primary-700 flex items-center justify-center font-bold text-xs border border-primary-200">
              {session.displayName ? session.displayName.charAt(0).toUpperCase() : 'U'}
            </div>
            <div className="hidden lg:block text-left">
              <div className="font-semibold text-ink-900 leading-tight">
                {session.displayName}
              </div>
              <div className="text-[11px] text-ink-400 leading-tight">
                {session.role}
              </div>
            </div>
          </div>
        )}
      </div>
    </header>
  );
}
