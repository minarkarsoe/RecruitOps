import React, { useEffect } from 'react';

export interface DialogProps {
  isOpen: boolean;
  onClose: () => void;
  title?: React.ReactNode;
  description?: React.ReactNode;
  children: React.ReactNode;
  size?: 'sm' | 'md' | 'lg' | 'xl';
  className?: string;
}

const SIZE_CLASSES = {
  sm: 'max-w-sm',
  md: 'max-w-md',
  lg: 'max-w-lg',
  xl: 'max-w-2xl',
};

export function Dialog({
  isOpen,
  onClose,
  title,
  description,
  children,
  size = 'md',
  className = '',
}: DialogProps) {
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
    <div className="fixed inset-0 z-50 overflow-y-auto">
      {/* Backdrop */}
      <div
        className="fixed inset-0 bg-ink-900/50 backdrop-blur-xs transition-opacity"
        onClick={onClose}
        aria-hidden="true"
        data-testid="dialog-backdrop"
      />

      {/* Modal Alignment Wrapper */}
      <div className="flex min-h-full items-center justify-center p-4 text-center sm:p-0">
        <div
          className={`relative transform overflow-hidden rounded-md bg-white border border-line shadow-overlay transition-all w-full ${SIZE_CLASSES[size]} text-left ${className}`}
          role="dialog"
          aria-modal="true"
        >
          {/* Header if title or description passed */}
          {(title || description) ? (
            <div className="border-b border-line px-6 py-4 flex items-start justify-between">
              <div>
                {title && (
                  <h3 className="font-sans text-lg font-semibold leading-6 text-ink-900">
                    {title}
                  </h3>
                )}
                {description && (
                  <p className="mt-1 text-sm text-ink-600">{description}</p>
                )}
              </div>
              <button
                type="button"
                onClick={onClose}
                className="rounded-md p-1 text-ink-400 hover:bg-canvas hover:text-ink-600 focus:outline-none focus:ring-2 focus:ring-brand-600"
                aria-label="Close dialog"
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
                className="rounded-md p-1 text-ink-400 hover:bg-canvas hover:text-ink-600 focus:outline-none focus:ring-2 focus:ring-brand-600"
                aria-label="Close dialog"
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

          {/* Body / Content */}
          <div>{children}</div>
        </div>
      </div>
    </div>
  );
}

export function DialogHeader({
  children,
  className = '',
}: {
  children: React.ReactNode;
  className?: string;
}) {
  return (
    <div className={`border-b border-line px-6 py-4 ${className}`}>
      {children}
    </div>
  );
}

export function DialogTitle({
  children,
  className = '',
}: {
  children: React.ReactNode;
  className?: string;
}) {
  return (
    <h3 className={`font-sans text-lg font-semibold text-ink-900 ${className}`}>
      {children}
    </h3>
  );
}

export function DialogDescription({
  children,
  className = '',
}: {
  children: React.ReactNode;
  className?: string;
}) {
  return <p className={`mt-1 text-sm text-ink-600 ${className}`}>{children}</p>;
}

export function DialogBody({
  children,
  className = '',
}: {
  children: React.ReactNode;
  className?: string;
}) {
  return <div className={`p-6 ${className}`}>{children}</div>;
}

export function DialogFooter({
  children,
  className = '',
}: {
  children: React.ReactNode;
  className?: string;
}) {
  return (
    <div
      className={`border-t border-line bg-canvas px-6 py-3 flex items-center justify-end gap-3 ${className}`}
    >
      {children}
    </div>
  );
}
