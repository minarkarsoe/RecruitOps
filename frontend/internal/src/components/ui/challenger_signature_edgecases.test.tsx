import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import {
  StatusPill,
  PipelineStageRail,
  ExpiryAttentionCard,
  ClientFeedbackBar,
  ClientPortalCard,
  type ClientPortalCandidate,
} from './index';

describe('Challenger Empirical Edge-Case Tests — Signature UI Components', () => {

  // ==========================================
  // 1. StatusPill Edge Cases
  // ==========================================
  describe('StatusPill Edge Cases', () => {
    it('handles unknown/unrecognized status fallback gracefully', () => {
      const { container } = render(<StatusPill status="NonExistentStatus" />);
      expect(screen.getByText('Non Existent Status')).toBeInTheDocument();
      const pill = container.firstChild as HTMLElement;
      expect(pill.className).toContain('bg-surface-50');
      expect(pill.className).toContain('text-ink-600');
    });

    it('humanises PascalCase string with multiple words correctly', () => {
      render(<StatusPill status="PendingApproval" />);
      expect(screen.getByText('Pending Approval')).toBeInTheDocument();
    });

    it('handles single word status without modification', () => {
      render(<StatusPill status="Live" />);
      expect(screen.getByText('Live')).toBeInTheDocument();
    });

    it('handles empty string status gracefully', () => {
      const { container } = render(<StatusPill status="" />);
      expect(container.firstChild).toBeInTheDocument();
    });
  });

  // ==========================================
  // 2. PipelineStageRail Edge Cases
  // ==========================================
  describe('PipelineStageRail Edge Cases', () => {
    it('renders cleanly when stages prop is an empty array', () => {
      const { container } = render(<PipelineStageRail stages={[]} />);
      const rail = screen.getByLabelText('Pipeline Stages');
      expect(rail).toBeInTheDocument();
      expect(container.querySelectorAll('button')).toHaveLength(0);
    });

    it('handles undefined onStageClick handler without throwing when button clicked', () => {
      render(<PipelineStageRail onStageClick={undefined} />);
      const button = screen.getByRole('button', { name: /Sourced/i });
      expect(() => fireEvent.click(button)).not.toThrow();
    });

    it('does not render separator arrows for a single stage item', () => {
      const singleStage = [{ label: 'SingleStage', count: 10 }];
      render(<PipelineStageRail stages={singleStage} />);
      expect(screen.getByText('SingleStage')).toBeInTheDocument();
      expect(screen.getByText('10')).toBeInTheDocument();
      expect(screen.queryByText('→')).not.toBeInTheDocument();
    });

    it('activates stage matching activeStage by status fallback when label differs', () => {
      const stages = [{ label: 'Custom Label', count: 5, status: 'Interview' }];
      render(<PipelineStageRail stages={stages} activeStage="Interview" />);
      const activeBtn = screen.getByRole('button', { name: /Custom Label/i });
      expect(activeBtn.className).toContain('bg-primary-100');
    });

    it('renders zero counts and large counts correctly', () => {
      const stages = [
        { label: 'ZeroStage', count: 0 },
        { label: 'HugeStage', count: 999999 },
      ];
      render(<PipelineStageRail stages={stages} />);
      expect(screen.getByText('0')).toBeInTheDocument();
      expect(screen.getByText('999999')).toBeInTheDocument();
    });
  });

  // ==========================================
  // 3. ExpiryAttentionCard Urgency & Edge Cases
  // ==========================================
  describe('ExpiryAttentionCard Urgency & Edge Cases', () => {
    it('renders cleanly when items prop is an empty array', () => {
      render(<ExpiryAttentionCard items={[]} />);
      expect(screen.getByText('Contracts Nearing Expiry')).toBeInTheDocument();
      expect(screen.getByText('0 contracts')).toBeInTheDocument();
    });

    it('verifies boundary urgency color-coding accurately', () => {
      const testItems = [
        { id: '1', clientName: 'Zero Days', daysRemaining: 0 },
        { id: '2', clientName: 'Boundary 7 Days', daysRemaining: 7 },
        { id: '3', clientName: 'Boundary 8 Days', daysRemaining: 8 },
        { id: '4', clientName: 'Boundary 30 Days', daysRemaining: 30 },
        { id: '5', clientName: 'Boundary 31 Days', daysRemaining: 31 },
        { id: '6', clientName: 'Overdue Negative', daysRemaining: -3 },
      ];

      render(<ExpiryAttentionCard items={testItems} />);

      // <= 7 days (danger): 0, 7, -3
      const danger0 = screen.getByText('0 days');
      expect(danger0.className).toContain('bg-danger-100');

      const danger7 = screen.getByText('7 days');
      expect(danger7.className).toContain('bg-danger-100');

      const dangerNeg = screen.getByText('-3 days');
      expect(dangerNeg.className).toContain('bg-danger-100');

      // 8 - 30 days (warning): 8, 30
      const warn8 = screen.getByText('8 days');
      expect(warn8.className).toContain('bg-accent-100');

      const warn30 = screen.getByText('30 days');
      expect(warn30.className).toContain('bg-accent-100');

      // > 30 days (normal/ink): 31
      const normal31 = screen.getByText('31 days');
      expect(normal31.className).toContain('bg-surface-50');
    });

    it('handles singular "1 day" vs plural "N days" formatting correctly', () => {
      const items = [
        { id: '1', clientName: 'Single Day Client', daysRemaining: 1 },
        { id: '2', clientName: 'Multi Day Client', daysRemaining: 2 },
      ];
      render(<ExpiryAttentionCard items={items} />);

      expect(screen.getByText('1 day')).toBeInTheDocument();
      expect(screen.getByText('2 days')).toBeInTheDocument();
      expect(screen.getByText('2 contracts')).toBeInTheDocument();
    });

    it('handles singular "1 contract" total header when items.length === 1', () => {
      const items = [{ id: '1', clientName: 'Single Item', daysRemaining: 10 }];
      render(<ExpiryAttentionCard items={items} />);
      expect(screen.getByText('1 contract')).toBeInTheDocument();
    });

    it('handles undefined onRenewItem and undefined item.onRenew without errors', () => {
      const items = [{ id: '1', clientName: 'No Handler', daysRemaining: 5 }];
      render(<ExpiryAttentionCard items={items} onRenewItem={undefined} />);
      const renewBtn = screen.getByRole('button', { name: 'Renew' });
      expect(() => fireEvent.click(renewBtn)).not.toThrow();
    });

    it('renders items without tier or contractTitle gracefully', () => {
      const items = [{ id: '1', clientName: 'Plain Client', daysRemaining: 12 }];
      render(<ExpiryAttentionCard items={items} />);
      expect(screen.getByText('Plain Client')).toBeInTheDocument();
      expect(screen.queryByText(/Senior/i)).not.toBeInTheDocument();
    });
  });

  // ==========================================
  // 4. ClientFeedbackBar Edge Cases
  // ==========================================
  describe('ClientFeedbackBar Edge Cases', () => {
    it('handles null selectedStatus by rendering interactive action buttons', () => {
      render(<ClientFeedbackBar selectedStatus={null} />);
      expect(screen.getByRole('button', { name: 'Accept for Interview' })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Need More Info' })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Reject' })).toBeInTheDocument();
    });

    it('handles undefined onSelectStatus without error when buttons are clicked', () => {
      render(<ClientFeedbackBar onSelectStatus={undefined} />);
      const acceptBtn = screen.getByRole('button', { name: 'Accept for Interview' });
      expect(() => fireEvent.click(acceptBtn)).not.toThrow();
    });

    it('handles Change button click when selectedStatus is set', () => {
      const handleSelect = vi.fn();
      render(<ClientFeedbackBar selectedStatus="Rejected" onSelectStatus={handleSelect} />);

      expect(screen.getByText('Feedback recorded:')).toBeInTheDocument();
      expect(screen.getByText('Rejected')).toBeInTheDocument();

      const changeBtn = screen.getByRole('button', { name: 'Change' });
      fireEvent.click(changeBtn);
      expect(handleSelect).toHaveBeenCalledWith('Rejected');
    });
  });

  // ==========================================
  // 5. ClientPortalCard Edge Cases
  // ==========================================
  describe('ClientPortalCard Edge Cases', () => {
    it('renders minimal candidate without optional fields crashing', () => {
      const minimalCandidate: ClientPortalCandidate = {
        id: 'min-1',
        name: 'Aung Aung',
        role: 'Junior Developer',
      };

      render(<ClientPortalCard candidate={minimalCandidate} />);

      expect(screen.getByText('Aung Aung')).toBeInTheDocument();
      expect(screen.getByText('Junior Developer')).toBeInTheDocument();

      // Initials fallback check
      expect(screen.getByText('AA')).toBeInTheDocument();
    });

    it('handles undefined onFeedback and onViewCv handlers gracefully', () => {
      const candidate: ClientPortalCandidate = {
        id: 'c-1',
        name: 'Thida Win',
        role: 'UI/UX Designer',
      };

      render(<ClientPortalCard candidate={candidate} onFeedback={undefined} onViewCv={undefined} />);

      const cvBtn = screen.getByRole('button', { name: /View Attached CV/i });
      expect(() => fireEvent.click(cvBtn)).not.toThrow();

      const acceptBtn = screen.getByRole('button', { name: 'Accept for Interview' });
      expect(() => fireEvent.click(acceptBtn)).not.toThrow();
    });

    it('updates internal status state when feedback button is clicked', () => {
      const candidate: ClientPortalCandidate = {
        id: 'c-2',
        name: 'Min Ko',
        role: 'Data Scientist',
      };

      const handleFeedback = vi.fn();
      render(<ClientPortalCard candidate={candidate} onFeedback={handleFeedback} />);

      expect(screen.getByRole('button', { name: 'Accept for Interview' })).toBeInTheDocument();

      fireEvent.click(screen.getByRole('button', { name: 'Accept for Interview' }));

      expect(handleFeedback).toHaveBeenCalledWith('Accepted');
      // Collapses into recorded state
      expect(screen.getByText('Feedback recorded:')).toBeInTheDocument();
    });

    it('renders quiet chips only for fields that are defined', () => {
      const partialCandidate: ClientPortalCandidate = {
        id: 'c-3',
        name: 'Su Su',
        role: 'QA Engineer',
        experience: '5 years',
        // expectedSalary, noticePeriod, location omitted
      };

      render(<ClientPortalCard candidate={partialCandidate} />);

      expect(screen.getByText('Experience:')).toBeInTheDocument();
      expect(screen.getByText('5 years')).toBeInTheDocument();
      expect(screen.queryByText('Expected Salary:')).not.toBeInTheDocument();
      expect(screen.queryByText('Notice Period:')).not.toBeInTheDocument();
    });
  });
});
