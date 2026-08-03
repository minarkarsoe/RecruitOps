import {
  Badge,
  Button,
  Input,
  Select,
  StatusPill,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@recruitops/ui';
import type { RequisitionListItem } from '@recruitops/types';

export interface RequisitionTableProps {
  items: RequisitionListItem[];
  isLoading?: boolean;
  onSelectRequisition?: (id: string) => void;
  onEditDraft?: (id: string) => void;
  onNewRequisition?: () => void;
  statusFilter?: string;
  onStatusFilterChange?: (status: string) => void;
  searchQuery?: string;
  onSearchQueryChange?: (query: string) => void;
  canCreate?: boolean;
  className?: string;
}

export function RequisitionTable({
  items,
  isLoading = false,
  onSelectRequisition,
  onEditDraft,
  onNewRequisition,
  statusFilter = 'all',
  onStatusFilterChange,
  searchQuery = '',
  onSearchQueryChange,
  canCreate = false,
  className = '',
}: RequisitionTableProps) {
  const statusOptions = [
    { value: 'all', label: 'All Statuses' },
    { value: 'Draft', label: 'Draft' },
    { value: 'PendingApproval', label: 'Pending Approval' },
    { value: 'Approved', label: 'Approved' },
    { value: 'Rejected', label: 'Rejected' },
    { value: 'Cancelled', label: 'Cancelled' },
  ];

  return (
    <div className={`space-y-4 ${className}`}>
      {/* Toolbar controls */}
      <div className="flex flex-wrap items-center justify-between gap-3 bg-surface-0 p-1">
        <div className="flex flex-1 flex-wrap items-center gap-3">
          {onSearchQueryChange && (
            <div className="w-64 max-w-full">
              <Input
                type="search"
                placeholder="Filter requisitions..."
                value={searchQuery}
                onChange={(e) => onSearchQueryChange(e.target.value)}
                className="h-8 text-xs"
              />
            </div>
          )}
          {onStatusFilterChange && (
            <div className="w-48">
              <Select
                value={statusFilter}
                onChange={(e) => onStatusFilterChange(e.target.value)}
                options={statusOptions}
                className="h-8 text-xs"
              />
            </div>
          )}
        </div>

        {canCreate && onNewRequisition && (
          <Button onClick={onNewRequisition} className="h-8 px-3 text-xs">
            New requisition
          </Button>
        )}
      </div>

      {/* Scannable Data Table */}
      {isLoading ? (
        <div className="rounded-md border border-line-200 bg-surface-0 p-8 text-center text-sm text-ink-600">
          Loading requisitions...
        </div>
      ) : items.length === 0 ? (
        <div className="rounded-md border border-line-200 bg-surface-0 p-8 text-center">
          <h3 className="text-base font-semibold text-ink-900">No requisitions found</h3>
          <p className="mt-1 text-sm text-ink-600">
            {searchQuery || statusFilter !== 'all'
              ? 'Try adjusting your filters or search query.'
              : 'Raise a requisition to start the hiring process.'}
          </p>
          {canCreate && onNewRequisition && (
            <Button onClick={onNewRequisition} className="mt-4 h-8 px-3 text-xs">
              Create Requisition
            </Button>
          )}
        </div>
      ) : (
        <Table>
          <TableHeader>
            <TableRow hoverable={false}>
              <TableHead dense>Position Role</TableHead>
              <TableHead dense>Department</TableHead>
              <TableHead dense className="text-right">Target Hires</TableHead>
              <TableHead dense className="text-right">Salary Budget</TableHead>
              <TableHead dense>Status</TableHead>
              <TableHead dense>Awaiting Approver</TableHead>
              <TableHead dense className="text-right">Actions</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {items.map((r) => (
              <TableRow
                key={r.id}
                hoverable
                className="cursor-pointer transition-colors"
                onClick={() => onSelectRequisition?.(r.id)}
              >
                <TableCell dense className="font-semibold text-primary-700 hover:underline">
                  {r.title}
                </TableCell>
                <TableCell dense className="text-ink-600">
                  {r.departmentName}
                </TableCell>
                <TableCell dense className="text-right font-mono text-xs text-ink-900">
                  <Badge variant="cyan" size="sm">
                    {r.headcount} {r.headcount === 1 ? 'hire' : 'hires'}
                  </Badge>
                </TableCell>
                <TableCell dense className="text-right font-mono text-xs text-ink-700">
                  {r.salaryBudget != null ? `$${r.salaryBudget.toLocaleString()}` : '—'}
                </TableCell>
                <TableCell dense>
                  <StatusPill status={r.status} />
                </TableCell>
                <TableCell dense className="text-xs text-ink-600">
                  {r.awaitingApprovalFrom ? (
                    <Badge variant="warning" size="sm">
                      {r.awaitingApprovalFrom}
                    </Badge>
                  ) : (
                    '—'
                  )}
                </TableCell>
                <TableCell dense className="text-right" onClick={(e) => e.stopPropagation()}>
                  <div className="flex justify-end gap-2">
                    {r.status === 'Draft' && onEditDraft && (
                      <Button
                        variant="secondary"
                        className="h-7 px-2 text-xs"
                        onClick={(e) => {
                          e.stopPropagation();
                          onEditDraft(r.id);
                        }}
                      >
                        Edit
                      </Button>
                    )}
                    <Button
                      variant="ghost"
                      className="h-7 px-2 text-xs"
                      onClick={(e) => {
                        e.stopPropagation();
                        onSelectRequisition?.(r.id);
                      }}
                    >
                      Details →
                    </Button>
                  </div>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}
    </div>
  );
}
