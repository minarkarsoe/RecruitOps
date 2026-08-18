import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { StatusPill, PipelineStageRail } from './index';

// This file used to open with a "StatusPill Extended Vocabulary" suite asserting `Sent to
// Client`, `Placed`, `Accepted`, `Need More Info`, `Active`, `Expiring Soon` and `Expired`,
// and PipelineStageRail suites asserting `Sent to Client` / `Placed` as default stages.
// Every one of those labels was deleted from the domain by ADR-0001 in July 2026. The tests
// outlived the product decision and were the reason the dead components stayed reachable.
// Rewritten 2026-08-17 against the four real enums.
describe('Design System Signature Components', () => {
  describe('StatusPill — candidate pipeline vocabulary', () => {
    it('renders every PipelineStatus stage with its own label', () => {
      const stages = [
        'Sourced', 'Applied', 'Screening', 'Shortlisted', 'Interview', 'Offer', 'Hired',
      ] as const;

      for (const stage of stages) {
        const { container, unmount } = render(<StatusPill status={stage} />);
        expect(screen.getByText(stage)).toBeInTheDocument();
        // Every stage must resolve to a real style, never the unknown-status fallback.
        expect((container.firstChild as HTMLElement).className).not.toBe('');
        unmount();
      }
    });

    it('renders Hired with success styling rather than the retired "Placed" label', () => {
      const { container } = render(<StatusPill status="Hired" />);
      expect(screen.getByText('Hired')).toBeInTheDocument();
      expect((container.firstChild as HTMLElement).className).toContain('bg-success-100');
    });
  });

  describe('StatusPill — requisition, posting and interview lifecycles', () => {
    it('humanises PendingApproval without inventing a second vocabulary', () => {
      const { container } = render(<StatusPill status="PendingApproval" />);
      expect(screen.getByText('Pending Approval')).toBeInTheDocument();
      expect((container.firstChild as HTMLElement).className).toContain('bg-warning-100');
    });

    it('styles Rejected as danger across every lifecycle that uses it', () => {
      const { container } = render(<StatusPill status="Rejected" />);
      expect((container.firstChild as HTMLElement).className).toContain('bg-danger-100');
    });

    it('renders the job-posting and interview lifecycles', () => {
      for (const status of ['Live', 'Closed', 'Scheduled', 'Completed', 'NoShow'] as const) {
        const { unmount } = render(<StatusPill status={status} />);
        // NoShow humanises to "No Show".
        expect(screen.getByText(status === 'NoShow' ? 'No Show' : status)).toBeInTheDocument();
        unmount();
      }
    });
  });

  // The design system used to mandate `-600` text on a `-100` tint and claim it was AA. It is
  // not: measured 2026-08-17, warning was 2.97:1, success 3.62, danger 4.08, info 4.23, all
  // below the 4.5:1 floor at this pill's 13px/600. Text on a tint now uses `-700`. Pinned here
  // because the failure is invisible — the pill looks fine and simply is not readable enough.
  describe('StatusPill — tint text meets contrast floor', () => {
    it.each([
      ['Applied', 'bg-info-100', 'text-info-700'],
      ['Interview', 'bg-warning-100', 'text-warning-700'],
      ['Hired', 'bg-success-100', 'text-success-700'],
      ['Rejected', 'bg-danger-100', 'text-danger-700'],
      ['Offer', 'bg-accent-100', 'text-accent-700'],
    ])('%s uses the -700 text step on its tint', (status, tint, text) => {
      const { container } = render(<StatusPill status={status} />);
      const pill = container.firstChild as HTMLElement;
      expect(pill.className).toContain(tint);
      expect(pill.className).toContain(text);
      expect(pill.className).not.toContain(text.replace('-700', '-600'));
    });

    it('never renders body text at ink-400, which fails contrast on surface-50', () => {
      for (const status of ['Cancelled', 'Closed', 'Sourced', 'Draft'] as const) {
        const { container, unmount } = render(<StatusPill status={status} />);
        expect((container.firstChild as HTMLElement).className).not.toContain('text-ink-400');
        unmount();
      }
    });
  });

  describe('PipelineStageRail', () => {
    it('renders the in-house funnel in PipelineStatus order', () => {
      render(<PipelineStageRail />);
      for (const stage of [
        'Sourced', 'Applied', 'Screening', 'Shortlisted', 'Interview', 'Offer', 'Hired',
      ]) {
        expect(screen.getByText(stage)).toBeInTheDocument();
      }
    });

    it('does not carry the retired agency stages', () => {
      render(<PipelineStageRail />);
      expect(screen.queryByText('Sent to Client')).not.toBeInTheDocument();
      expect(screen.queryByText('Placed')).not.toBeInTheDocument();
    });

    it('omits Rejected, which is an exit from the funnel rather than a stage in it', () => {
      render(<PipelineStageRail />);
      expect(screen.queryByText('Rejected')).not.toBeInTheDocument();
    });

    it('highlights the active stage chip', () => {
      render(<PipelineStageRail activeStage="Shortlisted" />);
      const activeBtn = screen.getByRole('button', { name: /Shortlisted/i });
      expect(activeBtn.className).toContain('bg-primary-100');
      expect(activeBtn.className).toContain('text-primary-700');
    });

    it('triggers onStageClick callback when a stage is clicked', () => {
      const handleClick = vi.fn();
      render(<PipelineStageRail onStageClick={handleClick} />);
      fireEvent.click(screen.getByRole('button', { name: /Shortlisted/i }));
      expect(handleClick).toHaveBeenCalledWith('Shortlisted');
    });
  });
});
