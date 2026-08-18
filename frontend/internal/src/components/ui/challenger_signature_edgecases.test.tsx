import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { StatusPill, PipelineStageRail } from './index';

// The ExpiryAttentionCard, ClientFeedbackBar and ClientPortalCard suites that used to live
// here were deleted on 2026-08-17 along with the components themselves: all three were
// agency-era surfaces (contract expiry, client feedback, the client CV-review portal) that
// ADR-0001 removed from the product in July 2026. They survived the pivot only because this
// file imported them, which kept them reachable and kept the suite green over dead code.
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

});
