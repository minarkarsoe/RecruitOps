import type { ButtonHTMLAttributes } from 'react';

// Design system §5.1. One primary button per view.
type Variant = 'primary' | 'secondary' | 'ghost' | 'danger';

const VARIANTS: Record<Variant, string> = {
  primary: 'bg-primary-600 text-white hover:bg-primary-700',
  secondary: 'bg-surface-0 text-ink-900 border border-line-200 hover:bg-surface-50',
  ghost: 'bg-transparent text-primary-600 hover:bg-primary-100',
  danger: 'bg-danger-600 text-white hover:opacity-90',
};

export interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: Variant;
}

export function Button({ variant = 'primary', className = '', ...props }: ButtonProps) {
  return (
    <button
      className={`inline-flex h-10 items-center justify-center rounded-md px-4 text-[15px] font-semibold
        transition-colors disabled:cursor-not-allowed disabled:opacity-50
        focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary-600
        ${VARIANTS[variant]} ${className}`}
      {...props}
    />
  );
}
