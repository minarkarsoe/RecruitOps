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
      expect((container.firstChild as HTMLElement).className).toContain('bg-positive-50');
    });
  });

  describe('StatusPill — requisition, posting and interview lifecycles', () => {
    it('humanises PendingApproval without inventing a second vocabulary', () => {
      const { container } = render(<StatusPill status="PendingApproval" />);
      expect(screen.getByText('Pending Approval')).toBeInTheDocument();
      expect((container.firstChild as HTMLElement).className).toContain('bg-warn-50');
    });

    it('styles Rejected as danger across every lifecycle that uses it', () => {
      const { container } = render(<StatusPill status="Rejected" />);
      expect((container.firstChild as HTMLElement).className).toContain('bg-critical-50');
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
  // below the 4.5:1 floor. Text on a tint uses `-700`, and that rule survived the V1.0 rebrand.
  //
  // V1.0 moved the tint from `-100` to `-50` (ADR-0025). Every pair was re-measured rather than
  // assumed, 2026-08-21: ink-600/canvas 7.24, brand-800/brand-50 7.27, info-700/info-50 6.16,
  // critical-700/critical-50 5.91, positive-700/positive-50 5.21, warn-700/warn-50 4.84. All
  // pass; warn has the least headroom and is the one that breaks first.
  //
  // Pinned here because the failure is invisible — the pill looks fine and simply is not
  // readable enough.
  describe('StatusPill — tint text meets contrast floor', () => {
    it.each([
      ['Applied', 'bg-info-50', 'text-info-700'],
      ['Interview', 'bg-warn-50', 'text-warn-700'],
      ['Hired', 'bg-positive-50', 'text-positive-700'],
      ['Rejected', 'bg-critical-50', 'text-critical-700'],
      ['Offer', 'bg-warn-50', 'text-warn-700'],
      ['Shortlisted', 'bg-brand-50', 'text-brand-800'],
    ])('%s pairs a -50 tint with a dark enough text step', (status, tint, text) => {
      const { container } = render(<StatusPill status={status} />);
      const pill = container.firstChild as HTMLElement;
      expect(pill.className).toContain(tint);
      expect(pill.className).toContain(text);
      // The -500 step is the one that fails on a light tint. It must never be the text colour.
      expect(pill.className).not.toContain(text.replace(/-\d00$/, '-500'));
    });

    it('never renders body text at ink-400, which fails contrast on canvas', () => {
      for (const status of ['Cancelled', 'Closed', 'Sourced', 'Draft'] as const) {
        const { container, unmount } = render(<StatusPill status={status} />);
        expect((container.firstChild as HTMLElement).className).not.toContain('text-ink-400');
        unmount();
      }
    });

    // The neutral pill is the only one with no tint to sit on, so it earns a border instead —
    // without it, a Draft pill on a white card is an invisible rectangle (ADR-0025 kit).
    it('gives the neutral statuses a border, since they have no tint', () => {
      for (const status of ['Sourced', 'Draft', 'Cancelled', 'Closed'] as const) {
        const { container, unmount } = render(<StatusPill status={status} />);
        const className = (container.firstChild as HTMLElement).className;
        expect(className).toContain('bg-canvas');
        expect(className).toContain('border-line');
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
      expect(activeBtn.className).toContain('bg-brand-100');
      expect(activeBtn.className).toContain('text-brand-700');
    });

    it('triggers onStageClick callback when a stage is clicked', () => {
      const handleClick = vi.fn();
      render(<PipelineStageRail onStageClick={handleClick} />);
      fireEvent.click(screen.getByRole('button', { name: /Shortlisted/i }));
      expect(handleClick).toHaveBeenCalledWith('Shortlisted');
    });
  });
});
