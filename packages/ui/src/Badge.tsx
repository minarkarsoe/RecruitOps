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

const VARIANT_CLASSES: Record<BadgeVariant, string> = {
  default: 'bg-surface-50 text-ink-900 border border-line-200',
  primary: 'bg-primary-100 text-primary-700',
  secondary: 'bg-surface-50 text-ink-600 border border-line-200',
  cyan: 'bg-primary-100 text-primary-700',
  teal: 'bg-primary-100 text-primary-700',
  zinc: 'bg-surface-50 text-ink-600 border border-line-200',
  success: 'bg-success-100 text-success-600',
  warning: 'bg-warning-100 text-warning-600',
  danger: 'bg-danger-100 text-danger-600',
  info: 'bg-info-100 text-info-600',
  // Client tier badges (design system §5.3)
  gold: 'bg-[#FBF3E1] text-[#B58226] border border-[#F2DBA8]',
  silver: 'bg-[#EFF2F5] text-[#5A6872] border border-[#D3DBE2]',
  bronze: 'bg-[#F6ECE3] text-[#8C5B32] border border-[#E8D3C3]',
};

const SIZE_CLASSES: Record<BadgeSize, string> = {
  sm: 'h-5 px-2 text-[11px] font-medium gap-1',
  md: 'h-6 px-2.5 text-[13px] font-semibold gap-1.5',
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
