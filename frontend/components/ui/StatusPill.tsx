// Signature component (design system §5.2). Fixed vocabulary — never invent labels.
const STYLES: Record<string, string> = {
  Sourced: 'bg-surface-50 text-ink-600',
  Shortlisted: 'bg-info-100 text-info-600',
  'Sent to Client': 'bg-info-100 text-info-600',
  Interview: 'bg-warning-100 text-warning-600',
  Placed: 'bg-success-100 text-success-600',
  Rejected: 'bg-danger-100 text-danger-600',
};

export function StatusPill({ label }: { label: keyof typeof STYLES | string }) {
  const cls = STYLES[label] ?? 'bg-surface-50 text-ink-600';
  return (
    <span className={`inline-flex h-6 items-center gap-1.5 rounded-full px-2.5 text-[13px] font-semibold ${cls}`}>
      <span className="h-1.5 w-1.5 rounded-full bg-current" />
      {label}
    </span>
  );
}
