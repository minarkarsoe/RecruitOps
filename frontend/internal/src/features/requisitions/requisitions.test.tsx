import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { renderHook, act } from '@testing-library/react';
import type { RequisitionDetail, RequisitionListItem } from '@recruitops/types';
import { RequisitionTable } from './RequisitionTable';
import { RequisitionDrawer } from './RequisitionDrawer';
import { useRequisitions } from './useRequisitions';

const { apiMock } = vi.hoisted(() => ({ apiMock: vi.fn() }));

vi.mock('../../lib/api', async () => {
  const actual = await vi.importActual<typeof import('../../lib/api')>('../../lib/api');
  return { ...actual, api: apiMock };
});

const mockRequisitionItems: RequisitionListItem[] = [
  {
    id: 'req-1',
    departmentId: 'dept-1',
    departmentName: 'Engineering',
    title: 'Senior Frontend Engineer',
    headcount: 2,
    salaryBudget: 120000,
    status: 'PendingApproval',
    submittedAt: '2026-08-01T10:00:00Z',
    awaitingApprovalFrom: 'VP of Engineering',

    yourStepLabel: 'VP of Engineering',
  },
  {
    id: 'req-2',
    departmentId: 'dept-1',
    departmentName: 'Engineering',
    title: 'Backend Lead',
    headcount: 1,
    salaryBudget: 140000,
    status: 'Approved',
    submittedAt: '2026-07-20T10:00:00Z',
    awaitingApprovalFrom: null,

    yourStepLabel: null,
  },
];

const mockRequisitionDetail: RequisitionDetail = {
  ...mockRequisitionItems[0],
  jobDescription: 'We are seeking an experienced Frontend Developer to lead UI work.',
  decidedAt: null,
  requestedByUserId: 'user-1',
  approvals: [
    {
      round: 1,
      sequence: 1,
      label: 'Engineering Director',
      approverUserId: 'user-2',
      decision: 'Approved',
      decidedAt: '2026-08-01T12:00:00Z',
      comment: 'Looks good!',
      decidedByUserId: null,
    },
    {
      round: 1,
      sequence: 2,
      label: 'VP of Engineering',
      approverUserId: 'user-3',
      decision: 'Waiting',
      decidedAt: null,
      comment: null,
      decidedByUserId: null,
    },
  ],
};

describe('RequisitionTable', () => {
  it('renders high-density requisition columns and row data', () => {
    render(<RequisitionTable items={mockRequisitionItems} />);

    expect(screen.getByText('Senior Frontend Engineer')).toBeInTheDocument();
    expect(screen.getByText('Backend Lead')).toBeInTheDocument();
    expect(screen.getByText('VP of Engineering')).toBeInTheDocument();
    expect(screen.getByText('2 hires')).toBeInTheDocument();
    expect(screen.getByText('$120,000')).toBeInTheDocument();
  });

  it('triggers onSelectRequisition when a row or details button is clicked', async () => {
    const onSelect = vi.fn();
    const user = userEvent.setup();

    render(<RequisitionTable items={mockRequisitionItems} onSelectRequisition={onSelect} />);

    await user.click(screen.getByText('Senior Frontend Engineer'));
    expect(onSelect).toHaveBeenCalledWith('req-1');
  });

  it('filters requisitions when search query is entered', async () => {
    const onSearchChange = vi.fn();
    const user = userEvent.setup();

    render(
      <RequisitionTable
        items={mockRequisitionItems}
        searchQuery=""
        onSearchQueryChange={onSearchChange}
      />
    );

    const searchInput = screen.getByPlaceholderText('Filter requisitions...');
    await user.type(searchInput, 'Frontend');
    expect(onSearchChange).toHaveBeenCalled();
  });
});

describe('RequisitionDrawer', () => {
  it('renders requisition detail, core metrics, and timeline steps when open', () => {
    render(
      <RequisitionDrawer
        requisition={mockRequisitionDetail}
        isOpen={true}
        onClose={vi.fn()}
      />
    );

    expect(screen.getByText('Senior Frontend Engineer')).toBeInTheDocument();
    expect(screen.getByText('We are seeking an experienced Frontend Developer to lead UI work.')).toBeInTheDocument();
    expect(screen.getByText('Engineering Director')).toBeInTheDocument();
    expect(screen.getByText('VP of Engineering')).toBeInTheDocument();
    expect(screen.getByText(/Looks good!/)).toBeInTheDocument();
  });

  it('calls onClose when close button is clicked', async () => {
    const onClose = vi.fn();
    const user = userEvent.setup();

    render(
      <RequisitionDrawer
        requisition={mockRequisitionDetail}
        isOpen={true}
        onClose={onClose}
      />
    );

    await user.click(screen.getByRole('button', { name: 'Close' }));
    expect(onClose).toHaveBeenCalled();
  });
});

describe('useRequisitions hook', () => {
  it('fetches requisitions list and filters correctly', async () => {
    apiMock.mockResolvedValueOnce(mockRequisitionItems);

    const { result } = renderHook(() => useRequisitions());

    await act(async () => {
      await result.current.loadRequisitions();
    });

    expect(result.current.items).toHaveLength(2);
    expect(result.current.filteredItems).toHaveLength(2);

    act(() => {
      result.current.setStatusFilter('Approved');
    });

    expect(result.current.filteredItems).toHaveLength(1);
    expect(result.current.filteredItems[0].id).toBe('req-2');
  });

  it('loads detail when requisition is selected', async () => {
    apiMock.mockResolvedValueOnce(mockRequisitionDetail);

    const { result } = renderHook(() => useRequisitions());

    await act(async () => {
      await result.current.loadDetail('req-1');
    });

    expect(result.current.selectedId).toBe('req-1');
    expect(result.current.detail?.title).toBe('Senior Frontend Engineer');
  });
});
