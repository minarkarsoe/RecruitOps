import type { ReactNode } from 'react';

// V1.0 (ADR-0025), built against `design/internal/components.html` and the panels in
// `design/internal/requisition-detail.html`.
//
// White fill, 1px line border, radius 12, padding 20, no shadow beyond the hairline. Cards sit
// on their border, not on a shadow — a page of drop-shadowed boxes reads as a dashboard demo
// rather than as a tool. No nested cards.
//
// The title is `text-base font-semibold` — 14px. It used to be 19px in the display face, and
// V1.0 has no display face: a card heading is a label for the thing below it, not a headline.
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
    <section className="rounded-lg border border-line bg-white p-5">
      {(title || action) && (
        <header className="mb-4 flex items-center justify-between gap-3">
          {title && <h2 className="text-base font-semibold">{title}</h2>}
          {action}
        </header>
      )}
      {children}
    </section>
  );
}
