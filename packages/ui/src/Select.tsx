import React, { forwardRef, useId } from 'react';

export interface SelectOption {
  value: string | number;
  label: string;
  disabled?: boolean;
}

export interface SelectProps extends React.SelectHTMLAttributes<HTMLSelectElement> {
  label?: React.ReactNode;
  error?: string;
  helperText?: React.ReactNode;
  options?: SelectOption[];
  placeholder?: string;
}

export const Select = forwardRef<HTMLSelectElement, SelectProps>(
  (
    {
      label,
      error,
      helperText,
      options,
      placeholder,
      children,
      className = '',
      id: customId,
      disabled,
      ...props
    },
    ref
  ) => {
    const generatedId = useId();
    const selectId = customId || generatedId;

    return (
      <div className="w-full">
        {label && (
          <label
            htmlFor={selectId}
            className="block text-sm font-medium text-ink-900 mb-1.5"
          >
            {label}
          </label>
        )}
        <div className="relative flex items-center">
          <select
            ref={ref}
            id={selectId}
            disabled={disabled}
            className={`h-10 w-full appearance-none rounded-sm border bg-surface-0 px-3 pr-8 text-[15px] text-ink-900 transition-colors focus:outline-none focus:ring-2 disabled:bg-surface-50 disabled:cursor-not-allowed ${
              error
                ? 'border-danger-600 focus:border-danger-600 focus:ring-danger-600'
                : 'border-line-200 focus:border-primary-600 focus:ring-primary-600'
            } ${className}`}
            {...props}
          >
            {placeholder && (
              <option value="" disabled selected hidden>
                {placeholder}
              </option>
            )}
            {options
              ? options.map((opt) => (
                  <option key={opt.value} value={opt.value} disabled={opt.disabled}>
                    {opt.label}
                  </option>
                ))
              : children}
          </select>
          <div className="pointer-events-none absolute right-3 flex items-center text-ink-400">
            <svg
              className="h-4 w-4"
              fill="none"
              viewBox="0 0 24 24"
              stroke="currentColor"
              strokeWidth="2"
            >
              <path strokeLinecap="round" strokeLinejoin="round" d="M19 9l-7 7-7-7" />
            </svg>
          </div>
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

Select.displayName = 'Select';
