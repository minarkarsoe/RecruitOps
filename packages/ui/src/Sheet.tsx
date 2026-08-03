import React, { useEffect } from 'react';

export interface SheetProps {
  isOpen: boolean;
  onClose: () => void;
  title?: React.ReactNode;
  description?: React.ReactNode;
  children: React.ReactNode;
  size?: 'sm' | 'md' | 'lg' | 'xl' | 'full';
  className?: string;
}

const SIZE_CLASSES = {
  sm: 'max-w-md',
  md: 'max-w-lg',
  lg: 'max-w-2xl',
  xl: 'max-w-4xl',
  full: 'max-w-full',
};

export function Sheet({
  isOpen,
  onClose,
  title,
  description,
  children,
  size = 'lg',
  className = '',
}: SheetProps) {
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape' && isOpen) {
        onClose();
      }
    };
    if (isOpen) {
      document.body.style.overflow = 'hidden';
      window.addEventListener('keydown', handleKeyDown);
    }
    return () => {
      document.body.style.overflow = '';
      window.removeEventListener('keydown', handleKeyDown);
    };
  }, [isOpen, onClose]);

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-50 overflow-hidden">
      {/* Backdrop */}
      <div
        className="fixed inset-0 bg-ink-900/40 backdrop-blur-xs transition-opacity duration-200"
        onClick={onClose}
        aria-hidden="true"
        data-testid="sheet-backdrop"
      />

      {/* Drawer Container */}
      <div className="fixed inset-y-0 right-0 flex max-w-full pl-10">
        <div
          className={`w-screen ${SIZE_CLASSES[size]} transform bg-surface-0 shadow-pop transition-transform duration-200 ease-in-out flex flex-col ${className}`}
          role="dialog"
          aria-modal="true"
        >
          {/* Default header if title is passed */}
          {(title || description) ? (
            <div className="flex items-start justify-between border-b border-line-200 px-6 py-4">
              <div>
                {title && (
                  <h2 className="font-display text-lg font-semibold text-ink-900">
                    {title}
                  </h2>
                )}
                {description && (
                  <p className="mt-1 text-sm text-ink-600">{description}</p>
                )}
              </div>
              <button
                type="button"
                onClick={onClose}
                className="rounded-md p-1.5 text-ink-400 hover:bg-surface-50 hover:text-ink-600 focus:outline-none focus:ring-2 focus:ring-primary-600"
                aria-label="Close panel"
              >
                <svg
                  className="h-5 w-5"
                  fill="none"
                  viewBox="0 0 24 24"
                  strokeWidth="2"
                  stroke="currentColor"
                >
                  <path
                    strokeLinecap="round"
                    strokeLinejoin="round"
                    d="M6 18L18 6M6 6l12 12"
                  />
                </svg>
              </button>
            </div>
          ) : (
            <div className="absolute top-4 right-4 z-10">
              <button
                type="button"
                onClick={onClose}
                className="rounded-md p-1.5 text-ink-400 hover:bg-surface-50 hover:text-ink-600 focus:outline-none focus:ring-2 focus:ring-primary-600"
                aria-label="Close panel"
              >
                <svg
                  className="h-5 w-5"
                  fill="none"
                  viewBox="0 0 24 24"
                  strokeWidth="2"
                  stroke="currentColor"
                >
                  <path
                    strokeLinecap="round"
                    strokeLinejoin="round"
                    d="M6 18L18 6M6 6l12 12"
                  />
                </svg>
              </button>
            </div>
          )}

          {/* Drawer Body */}
          <div className="flex-1 overflow-y-auto">{children}</div>
        </div>
      </div>
    </div>
  );
}

export function SheetHeader({
  children,
  className = '',
}: {
  children: React.ReactNode;
  className?: string;
}) {
  return (
    <div className={`border-b border-line-200 px-6 py-4 ${className}`}>
      {children}
    </div>
  );
}

export function SheetTitle({
  children,
  className = '',
}: {
  children: React.ReactNode;
  className?: string;
}) {
  return (
    <h2 className={`font-display text-lg font-semibold text-ink-900 ${className}`}>
      {children}
    </h2>
  );
}

export function SheetDescription({
  children,
  className = '',
}: {
  children: React.ReactNode;
  className?: string;
}) {
  return <p className={`mt-1 text-sm text-ink-600 ${className}`}>{children}</p>;
}

export function SheetBody({
  children,
  className = '',
}: {
  children: React.ReactNode;
  className?: string;
}) {
  return <div className={`p-6 ${className}`}>{children}</div>;
}

export function SheetFooter({
  children,
  className = '',
}: {
  children: React.ReactNode;
  className?: string;
}) {
  return (
    <div
      className={`border-t border-line-200 bg-surface-50 px-6 py-4 flex items-center justify-end gap-3 ${className}`}
    >
      {children}
    </div>
  );
}
