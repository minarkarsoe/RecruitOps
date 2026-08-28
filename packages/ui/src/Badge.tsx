import React from 'react';

// `gold` / `silver` / `bronze` were removed on 2026-08-28 — MIGRATION-PLAN step 5, "remove tier
// badge". They rendered `ClientTier`, an agency-era concept deleted by ADR-0001 on 2026-07-27.
// No screen had rendered them since; only three test files kept them reachable, which is
// circular — the tests existed because the variants did.
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
  | 'info';

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
};
// Every value above is a V1.0 token. The three tier variants that used to sit here were the
// only hard-coded hexes left in this file — they predated the token system entirely and could
// never have been checked against it.

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
  // The crown icon that used to appear automatically for `variant="gold"` went with the tier
  // variants (MIGRATION-PLAN step 5). A badge now shows an icon only when one is passed in,
  // which is the rule every other variant already followed.
  return (
    <span
      className={`inline-flex items-center rounded-full transition-colors whitespace-nowrap ${VARIANT_CLASSES[variant]} ${SIZE_CLASSES[size]} ${className}`}
      {...props}
    >
      {icon}
      {children}
    </span>
  );
}
