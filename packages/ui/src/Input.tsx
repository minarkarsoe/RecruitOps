import React, { forwardRef, useId } from 'react';

export interface InputProps extends React.InputHTMLAttributes<HTMLInputElement> {
  label?: React.ReactNode;
  error?: string;
  helperText?: React.ReactNode;
  leftIcon?: React.ReactNode;
  rightIcon?: React.ReactNode;
}

export const Input = forwardRef<HTMLInputElement, InputProps>(
  (
    {
      label,
      error,
      helperText,
      leftIcon,
      rightIcon,
      className = '',
      id: customId,
      disabled,
      ...props
    },
    ref
  ) => {
    const generatedId = useId();
    const inputId = customId || generatedId;

    return (
      <div className="w-full">
        {label && (
          <label
            htmlFor={inputId}
            className="block text-sm font-medium text-ink-900 mb-1.5"
          >
            {label}
          </label>
        )}
        <div className="relative flex items-center">
          {leftIcon && (
            <div className="absolute left-3 text-ink-400 pointer-events-none flex items-center justify-center">
              {leftIcon}
            </div>
          )}
          <input
            ref={ref}
            id={inputId}
            disabled={disabled}
            className={`h-10 w-full rounded-sm border bg-surface-0 text-[15px] text-ink-900 placeholder-ink-400 transition-colors focus:outline-none focus:ring-2 disabled:bg-surface-50 disabled:cursor-not-allowed ${
              leftIcon ? 'pl-9' : 'px-3'
            } ${rightIcon ? 'pr-9' : 'px-3'} ${
              error
                ? 'border-danger-600 focus:border-danger-600 focus:ring-danger-600'
                : 'border-line-200 focus:border-primary-600 focus:ring-primary-600'
            } ${className}`}
            {...props}
          />
          {rightIcon && (
            <div className="absolute right-3 text-ink-400 pointer-events-none flex items-center justify-center">
              {rightIcon}
            </div>
          )}
        </div>
        {error ? (
          <p className="mt-1.5 text-xs text-danger-600 font-medium">{error}</p>
        ) : helperText ? (
          <p className="mt-1.5 text-xs text-ink-600">{helperText}</p>
        ) : null}
      </div>
    );
  }
);

Input.displayName = 'Input';
