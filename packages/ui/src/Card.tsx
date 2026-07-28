import type { ReactNode } from 'react';

// Design system §5.4: white background, 1px line border, radius 12, padding 24,
// shadow-card. Cards sit on their border, not on a shadow. No nested cards.
export function Card({
  title,
  action,
  children,
}: {
  title?: string;
  action?: ReactNode;
  children: ReactNode;
}) {
  return (
    <section className="rounded-md border border-line-200 bg-surface-0 p-6 shadow-card">
      {(title || action) && (
        <header className="mb-4 flex items-center justify-between">
          {title && <h2 className="font-display text-[19px] font-semibold leading-7">{title}</h2>}
          {action}
        </header>
      )}
      {children}
    </section>
  );
}
