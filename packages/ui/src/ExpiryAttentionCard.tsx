import { Badge } from './Badge';
import { Button } from './Button';

export interface ExpiryItem {
  id: string;
  clientName: string;
  contractTitle?: string;
  tier?: 'Gold' | 'Silver' | 'Bronze';
  daysRemaining: number;
  onRenew?: () => void;
}

export interface ExpiryAttentionCardProps {
  title?: string;
  items?: ExpiryItem[];
  onRenewItem?: (item: ExpiryItem) => void;
  className?: string;
}

const DEFAULT_EXPIRY_ITEMS: ExpiryItem[] = [
  { id: '1', clientName: 'Acme Corp', contractTitle: 'Senior React Engineer', tier: 'Gold', daysRemaining: 5 },
  { id: '2', clientName: 'TechFlow Inc', contractTitle: 'Product Manager', tier: 'Silver', daysRemaining: 18 },
  { id: '3', clientName: 'Global Logistics', contractTitle: 'DevOps Lead', tier: 'Bronze', daysRemaining: 45 },
];

function getUrgencyBadgeStyle(days: number): string {
  if (days <= 7) {
    return 'bg-danger-100 text-danger-600 border-danger-200';
  }
  if (days <= 30) {
    return 'bg-accent-100 text-warning-600 border-warning-200';
  }
  return 'bg-surface-50 text-ink-900 border-line-200';
}

/**
 * Signature Component: Expiry Attention Card (Design System §6.4).
 * Dashboard card listing contracts nearing expiry with urgency color-coded
 * countdowns (>30d ink, 8-30d warning, <=7d danger) and a "Renew" action.
 */
export function ExpiryAttentionCard({
  title = 'Contracts Nearing Expiry',
  items = DEFAULT_EXPIRY_ITEMS,
  onRenewItem,
  className = '',
}: ExpiryAttentionCardProps) {
  return (
    <div
      className={`rounded-xl bg-surface-0 p-6 shadow-card border border-line-200 ${className}`}
    >
      <div className="flex items-center justify-between mb-4">
        <h3 className="text-base font-semibold text-ink-900">{title}</h3>
        <span className="text-xs text-ink-600 font-mono font-medium">
          {items.length} {items.length === 1 ? 'contract' : 'contracts'}
        </span>
      </div>

      <div className="divide-y divide-line-200">
        {items.map((item) => {
          const urgencyStyle = getUrgencyBadgeStyle(item.daysRemaining);
          const tierVariant = item.tier ? (item.tier.toLowerCase() as 'gold' | 'silver' | 'bronze') : undefined;

          return (
            <div
              key={item.id}
              className="flex items-center justify-between py-3 first:pt-0 last:pb-0 gap-4"
            >
              <div className="flex items-center gap-3 min-w-0">
                <div className="min-w-0">
                  <div className="flex items-center gap-2">
                    <span className="font-semibold text-ink-900 text-sm truncate">
                      {item.clientName}
                    </span>
                    {tierVariant && (
                      <Badge variant={tierVariant} size="sm">
                        {item.tier}
                      </Badge>
                    )}
                  </div>
                  {item.contractTitle && (
                    <p className="text-xs text-ink-600 truncate mt-0.5">
                      {item.contractTitle}
                    </p>
                  )}
                </div>
              </div>

              <div className="flex items-center gap-3 shrink-0">
                <span
                  className={`inline-flex items-center rounded-md border px-2.5 py-1 text-xs font-mono font-semibold ${urgencyStyle}`}
                >
                  {item.daysRemaining} {item.daysRemaining === 1 ? 'day' : 'days'}
                </span>

                <Button
                  className="h-8 px-3 text-xs"
                  onClick={() => {
                    item.onRenew?.();
                    onRenewItem?.(item);
                  }}
                >
                  Renew
                </Button>
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}
