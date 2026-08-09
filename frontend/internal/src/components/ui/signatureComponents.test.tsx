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

describe('Design System Signature Components (Milestone 1)', () => {
  describe('StatusPill Extended Vocabulary', () => {
    it('renders Sent to Client with info styling', () => {
      const { container } = render(<StatusPill status="Sent to Client" />);
      expect(screen.getByText('Sent to Client')).toBeInTheDocument();
      const pill = container.firstChild as HTMLElement;
      expect(pill.className).toContain('bg-info-100');
      expect(pill.className).toContain('text-info-600');
    });

    it('renders SentToClient PascalCase variant correctly', () => {
      const { container } = render(<StatusPill status="SentToClient" />);
      expect(screen.getByText('Sent To Client')).toBeInTheDocument();
      const pill = container.firstChild as HTMLElement;
      expect(pill.className).toContain('bg-info-100');
    });

    it('renders Placed and Accepted with success styling', () => {
      const { container: c1 } = render(<StatusPill status="Placed" />);
      expect(screen.getByText('Placed')).toBeInTheDocument();
      expect((c1.firstChild as HTMLElement).className).toContain('bg-success-100');

      const { container: c2 } = render(<StatusPill status="Accepted" />);
      expect(screen.getByText('Accepted')).toBeInTheDocument();
      expect((c2.firstChild as HTMLElement).className).toContain('bg-success-100');
    });

    it('renders Need More Info with warning styling', () => {
      const { container } = render(<StatusPill status="Need More Info" />);
      expect(screen.getByText('Need More Info')).toBeInTheDocument();
      const pill = container.firstChild as HTMLElement;
      expect(pill.className).toContain('bg-warning-100');
      expect(pill.className).toContain('text-warning-600');
    });

    it('renders Active with success styling', () => {
      const { container } = render(<StatusPill status="Active" />);
      expect(screen.getByText('Active')).toBeInTheDocument();
      expect((container.firstChild as HTMLElement).className).toContain('bg-success-100');
    });

    it('renders Expiring Soon with warning styling', () => {
      const { container } = render(<StatusPill status="Expiring Soon" />);
      expect(screen.getByText('Expiring Soon')).toBeInTheDocument();
      expect((container.firstChild as HTMLElement).className).toContain('bg-warning-100');
    });

    it('renders Expired with danger styling', () => {
      const { container } = render(<StatusPill status="Expired" />);
      expect(screen.getByText('Expired')).toBeInTheDocument();
      expect((container.firstChild as HTMLElement).className).toContain('bg-danger-100');
      expect((container.firstChild as HTMLElement).className).toContain('text-danger-600');
    });
  });

  describe('PipelineStageRail', () => {
    it('renders default pipeline stage items with mono counts', () => {
      render(<PipelineStageRail />);
      expect(screen.getByText('Sourced')).toBeInTheDocument();
      expect(screen.getByText('24')).toBeInTheDocument();
      expect(screen.getByText('Shortlisted')).toBeInTheDocument();
      expect(screen.getByText('8')).toBeInTheDocument();
      expect(screen.getByText('Sent to Client')).toBeInTheDocument();
      expect(screen.getByText('5')).toBeInTheDocument();
      expect(screen.getByText('Interview')).toBeInTheDocument();
      expect(screen.getByText('2')).toBeInTheDocument();
      expect(screen.getByText('Placed')).toBeInTheDocument();
      expect(screen.getByText('1')).toBeInTheDocument();
    });

    it('highlights the active stage chip', () => {
      render(<PipelineStageRail activeStage="Sent to Client" />);
      const activeBtn = screen.getByRole('button', { name: /Sent to Client/i });
      expect(activeBtn.className).toContain('bg-primary-100');
      expect(activeBtn.className).toContain('text-primary-700');
    });

    it('triggers onStageClick callback when stage is clicked', () => {
      const handleClick = vi.fn();
      render(<PipelineStageRail onStageClick={handleClick} />);
      const shortlistedBtn = screen.getByRole('button', { name: /Shortlisted/i });
      fireEvent.click(shortlistedBtn);
      expect(handleClick).toHaveBeenCalledWith('Shortlisted');
    });
  });

  describe('ExpiryAttentionCard', () => {
    it('renders card title and contract items with urgency classes', () => {
      const items = [
        { id: '1', clientName: 'Critical Client', tier: 'Gold' as const, daysRemaining: 5 },
        { id: '2', clientName: 'Warning Client', tier: 'Silver' as const, daysRemaining: 15 },
        { id: '3', clientName: 'Normal Client', tier: 'Bronze' as const, daysRemaining: 40 },
      ];
      render(<ExpiryAttentionCard title="Contracts Nearing Expiry" items={items} />);

      expect(screen.getByText('Contracts Nearing Expiry')).toBeInTheDocument();
      expect(screen.getByText('Critical Client')).toBeInTheDocument();
      expect(screen.getByText('Warning Client')).toBeInTheDocument();
      expect(screen.getByText('Normal Client')).toBeInTheDocument();

      // Check countdown text and urgency color coding
      const dangerBadge = screen.getByText('5 days');
      expect(dangerBadge.className).toContain('bg-danger-100');

      const warningBadge = screen.getByText('15 days');
      expect(warningBadge.className).toContain('bg-accent-100');

      const normalBadge = screen.getByText('40 days');
      expect(normalBadge.className).toContain('bg-surface-50');
    });

    it('invokes onRenew callback when Renew button is clicked', () => {
      const handleRenewItem = vi.fn();
      const itemRenew = vi.fn();
      const items = [
        { id: '1', clientName: 'Acme Corp', daysRemaining: 5, onRenew: itemRenew },
      ];
      render(<ExpiryAttentionCard items={items} onRenewItem={handleRenewItem} />);

      const renewBtn = screen.getByRole('button', { name: 'Renew' });
      fireEvent.click(renewBtn);

      expect(itemRenew).toHaveBeenCalledTimes(1);
      expect(handleRenewItem).toHaveBeenCalledWith(items[0]);
    });
  });

  describe('ClientFeedbackBar', () => {
    it('renders feedback action buttons when no status selected', () => {
      const handleSelect = vi.fn();
      render(<ClientFeedbackBar onSelectStatus={handleSelect} />);

      expect(screen.getByRole('button', { name: 'Accept for Interview' })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Need More Info' })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Reject' })).toBeInTheDocument();

      fireEvent.click(screen.getByRole('button', { name: 'Accept for Interview' }));
      expect(handleSelect).toHaveBeenCalledWith('Accepted');
    });

    it('renders confirmed status pill state when selectedStatus is provided', () => {
      render(<ClientFeedbackBar selectedStatus="Accepted" />);
      expect(screen.getByText('Feedback recorded:')).toBeInTheDocument();
      expect(screen.getByText('Accepted')).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Change' })).toBeInTheDocument();
    });
  });

  describe('ClientPortalCard', () => {
    const candidate: ClientPortalCandidate = {
      id: 'c-101',
      name: 'Kyaw Kyaw',
      role: 'Senior Full Stack Engineer',
      experience: '7 years',
      expectedSalary: '$3,500/mo',
      noticePeriod: '1 month',
      location: 'Yangon, Myanmar',
      summary: 'Experienced web engineer specializing in React and Node.js.',
      skills: ['React', 'TypeScript', 'Tailwind', 'Node.js'],
    };

    it('renders candidate facts, skills, CV button, and feedback bar', () => {
      const handleFeedback = vi.fn();
      const handleViewCv = vi.fn();

      render(
        <ClientPortalCard
          candidate={candidate}
          onFeedback={handleFeedback}
          onViewCv={handleViewCv}
        />
      );

      expect(screen.getByText('Kyaw Kyaw')).toBeInTheDocument();
      expect(screen.getByText('Senior Full Stack Engineer')).toBeInTheDocument();
      expect(screen.getByText('7 years')).toBeInTheDocument();
      expect(screen.getByText('$3,500/mo')).toBeInTheDocument();
      expect(screen.getByText('Yangon, Myanmar')).toBeInTheDocument();
      expect(screen.getByText('React')).toBeInTheDocument();
      expect(screen.getByText('TypeScript')).toBeInTheDocument();

      const cvBtn = screen.getByRole('button', { name: /View Attached CV/i });
      fireEvent.click(cvBtn);
      expect(handleViewCv).toHaveBeenCalledWith(candidate);

      const acceptBtn = screen.getByRole('button', { name: 'Accept for Interview' });
      fireEvent.click(acceptBtn);
      expect(handleFeedback).toHaveBeenCalledWith('Accepted');
    });
  });
});
