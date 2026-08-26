import type { ButtonHTMLAttributes } from 'react';

// V1.0 (ADR-0025), built against `design/internal/components.html` → "Buttons".
//
// One primary per view. Height 36, radius 8, 14px medium — not 15px semibold: at this density
// a heavier label makes every toolbar shout. Labels name the outcome ("Submit requisition",
// never "Submit"), which is a call-site rule this component cannot enforce.
type Variant = 'primary' | 'secondary' | 'ghost' | 'danger';

// The kit spells out hover AND active for the primary and secondary variants. Active matters
// more than it looks: on a touch screen there is no hover, so press feedback is the only
// confirmation the tap landed.
const VARIANTS: Record<Variant, string> = {
  primary: 'bg-brand-700 text-white hover:bg-brand-800 active:bg-brand-900',
  secondary: 'bg-white text-ink-900 border border-line hover:border-line-strong active:bg-canvas',
  ghost: 'bg-transparent text-brand-700 hover:bg-white',
  // critical-500 rather than -700 as the resting fill: a Reject button that is already at its
  // darkest has nowhere to go on hover, and this is a control people want confirmation from.
  danger: 'bg-critical-500 text-white hover:bg-critical-700',
};

export interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: Variant;
}

export function Button({ variant = 'primary', className = '', ...props }: ButtonProps) {
  return (
    <button
      className={`inline-flex h-9 items-center justify-center rounded-md px-3.5 text-base font-medium
        transition-colors disabled:cursor-not-allowed disabled:bg-ink-400/40 disabled:text-white
        focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-700 focus-visible:ring-offset-2
        ${VARIANTS[variant]} ${className}`}
      {...props}
    />
  );
}
