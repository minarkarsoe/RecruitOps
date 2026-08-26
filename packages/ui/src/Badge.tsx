import React from 'react';

export type BadgeVariant =
  | 'default'
  | 'primary'
  | 'secondary'
  | 'cyan'
  | 'teal'
  | 'zinc'
  | 'success'
  | 'warning'
  | 'danger'
  | 'info'
  | 'gold'
  | 'silver'
  | 'bronze';

export type BadgeSize = 'sm' | 'md';

export interface BadgeProps extends React.HTMLAttributes<HTMLSpanElement> {
  variant?: BadgeVariant;
  size?: BadgeSize;
  icon?: React.ReactNode;
  children: React.ReactNode;
}

// Every tinted variant is a -700 (or -800) step on its own -50 tint, which is the kit's rule:
// "Text on a -50/-100 tint always uses the -700 step."
//
// ⚠️ This file previously shipped `success`, `warning` and `danger` as a -500 on a -50 tint —
// the exact failure that rule exists to prevent, and it survived the packages/ui rebuild because
// the rebuild checked StatusPill and not Badge. Measured 2026-08-25 with the same script:
//
//     positive-500 on positive-50   2.41:1  FAIL   →  positive-700   5.21:1  PASS
//     warn-500     on warn-50       2.07:1  FAIL   →  warn-700       4.84:1  PASS
//     critical-500 on critical-50   3.44:1  FAIL   →  critical-700   5.91:1  PASS
//     brand-800    on brand-50                        7.27:1  PASS  (the kit's own chip)
//
// A -500 on white would have passed; on its own tint it never does. The colours were right and
// the STEPS were wrong, which is precisely why this needs measuring rather than reviewing.
const VARIANT_CLASSES: Record<BadgeVariant, string> = {
  default: 'bg-canvas text-ink-600 border border-line',
  primary: 'bg-brand-50 text-brand-800',
  secondary: 'bg-canvas text-ink-600 border border-line',
  cyan: 'bg-brand-50 text-brand-800',
  teal: 'bg-brand-50 text-brand-800',
  zinc: 'bg-canvas text-ink-600 border border-line',
  success: 'bg-positive-50 text-positive-700',
  warning: 'bg-warn-50 text-warn-700',
  danger: 'bg-critical-50 text-critical-700',
  info: 'bg-info-50 text-info-700',
  // ⚠️ STALE — `ClientTier` was an agency-era concept, removed by ADR-0001. No screen renders
  // these; only three test files reference them. Left in place because deleting them is a
  // migration change, not a design-token one — see docs/status/MIGRATION-PLAN.md.
  gold: 'bg-[#FBF3E1] text-[#B58226] border border-[#F2DBA8]',
  silver: 'bg-[#EFF2F5] text-[#5A6872] border border-[#D3DBE2]',
  bronze: 'bg-[#F6ECE3] text-[#8C5B32] border border-[#E8D3C3]',
};

const SIZE_CLASSES: Record<BadgeSize, string> = {
  sm: 'h-5 px-2 text-2xs font-medium gap-1',
  md: 'h-6 px-2.5 text-xs font-medium gap-1.5',
};

export function Badge({
  variant = 'default',
  size = 'md',
  icon,
  children,
  className = '',
  ...props
}: BadgeProps) {
  // Render default crown icon for gold tier if variant is gold and no custom icon provided
  const renderedIcon = icon || (variant === 'gold' ? (
    <svg className="h-3 w-3 fill-current" viewBox="0 0 24 24">
      <path d="M5 16L3 5l5.5 5L12 4l3.5 6L21 5l-2 11H5zm14 3c0 .6-.4 1-1 1H6c-.6 0-1-.4-1-1v-1h14v1z" />
    </svg>
  ) : null);

  return (
    <span
      className={`inline-flex items-center rounded-full transition-colors whitespace-nowrap ${VARIANT_CLASSES[variant]} ${SIZE_CLASSES[size]} ${className}`}
      {...props}
    >
      {renderedIcon}
      {children}
    </span>
  );
}
