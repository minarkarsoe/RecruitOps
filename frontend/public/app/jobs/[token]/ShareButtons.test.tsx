import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { ShareButtons } from './ShareButtons';

describe('ShareButtons', () => {
  it('shares the page with each channel tagged, so a share there is counted as that channel', async () => {
    render(<ShareButtons token="tok1" />);
    const page = `${window.location.origin}/jobs/tok1`;

    expect(await screen.findByRole('link', { name: 'Facebook' })).toHaveAttribute(
      'href', `https://www.facebook.com/sharer/sharer.php?u=${encodeURIComponent(`${page}?via=facebook`)}`);
    expect(screen.getByRole('link', { name: 'Telegram' })).toHaveAttribute(
      'href', `https://t.me/share/url?url=${encodeURIComponent(`${page}?via=telegram`)}`);
    expect(screen.getByRole('link', { name: 'Viber' })).toHaveAttribute(
      'href', `viber://forward?text=${encodeURIComponent(`${page}?via=viber`)}`);
    expect(screen.getByRole('button', { name: 'Copy link' })).toBeInTheDocument();
  });
});
