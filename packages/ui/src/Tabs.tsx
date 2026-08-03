import React, { createContext, useContext } from 'react';

export interface TabItem {
  id: string;
  label: React.ReactNode;
  count?: number;
  disabled?: boolean;
}

export interface TabsProps {
  tabs?: TabItem[];
  activeTab?: string;
  onChange?: (id: string) => void;
  value?: string;
  onValueChange?: (value: string) => void;
  children?: React.ReactNode;
  className?: string;
}

const TabsContext = createContext<{
  activeTab: string;
  onChange: (id: string) => void;
}>({
  activeTab: '',
  onChange: () => {},
});

export function Tabs({
  tabs,
  activeTab,
  onChange,
  value,
  onValueChange,
  children,
  className = '',
}: TabsProps) {
  const currentActive = value !== undefined ? value : (activeTab || '');
  const handleTabChange = (id: string) => {
    if (onValueChange) onValueChange(id);
    if (onChange) onChange(id);
  };

  // If prop-driven tabs array is provided
  if (tabs) {
    return (
      <div className={`border-b border-line-200 ${className}`}>
        <nav className="-mb-px flex gap-6" aria-label="Tabs">
          {tabs.map((tab) => {
            const isActive = tab.id === currentActive;
            return (
              <button
                key={tab.id}
                type="button"
                disabled={tab.disabled}
                onClick={() => handleTabChange(tab.id)}
                className={`inline-flex items-center gap-2 border-b-2 py-3 text-sm transition-colors focus:outline-none ${
                  isActive
                    ? 'border-primary-600 font-semibold text-ink-900'
                    : 'border-transparent font-medium text-ink-600 hover:border-line-200 hover:text-ink-900'
                } ${tab.disabled ? 'cursor-not-allowed opacity-50' : ''}`}
                aria-current={isActive ? 'page' : undefined}
              >
                {tab.label}
                {tab.count !== undefined && (
                  <span
                    className={`rounded-full px-2 py-0.5 text-xs font-semibold ${
                      isActive
                        ? 'bg-primary-100 text-primary-700'
                        : 'bg-surface-50 text-ink-600'
                    }`}
                  >
                    {tab.count}
                  </span>
                )}
              </button>
            );
          })}
        </nav>
      </div>
    );
  }

  // Compound component pattern
  return (
    <TabsContext.Provider value={{ activeTab: currentActive, onChange: handleTabChange }}>
      <div className={className}>{children}</div>
    </TabsContext.Provider>
  );
}

export function TabsList({
  children,
  className = '',
}: {
  children: React.ReactNode;
  className?: string;
}) {
  return (
    <div className={`border-b border-line-200 ${className}`}>
      <nav className="-mb-px flex gap-6" aria-label="Tabs">
        {children}
      </nav>
    </div>
  );
}

export interface TabsTriggerProps {
  value: string;
  children: React.ReactNode;
  count?: number;
  disabled?: boolean;
  className?: string;
}

export function TabsTrigger({
  value,
  children,
  count,
  disabled = false,
  className = '',
}: TabsTriggerProps) {
  const { activeTab, onChange } = useContext(TabsContext);
  const isActive = activeTab === value;

  return (
    <button
      type="button"
      disabled={disabled}
      onClick={() => onChange(value)}
      className={`inline-flex items-center gap-2 border-b-2 py-3 text-sm transition-colors focus:outline-none ${
        isActive
          ? 'border-primary-600 font-semibold text-ink-900'
          : 'border-transparent font-medium text-ink-600 hover:border-line-200 hover:text-ink-900'
      } ${disabled ? 'cursor-not-allowed opacity-50' : ''} ${className}`}
      aria-current={isActive ? 'page' : undefined}
    >
      {children}
      {count !== undefined && (
        <span
          className={`rounded-full px-2 py-0.5 text-xs font-semibold ${
            isActive
              ? 'bg-primary-100 text-primary-700'
              : 'bg-surface-50 text-ink-600'
          }`}
        >
          {count}
        </span>
      )}
    </button>
  );
}

export interface TabsContentProps {
  value: string;
  children: React.ReactNode;
  className?: string;
}

export function TabsContent({ value, children, className = '' }: TabsContentProps) {
  const { activeTab } = useContext(TabsContext);
  if (activeTab !== value) return null;
  return <div className={`pt-4 ${className}`}>{children}</div>;
}
