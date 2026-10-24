'use client';

import { useEffect, useState } from 'react';
import { tagged } from '../../../lib/channel';

/**
 * The share row under the form (`design/public/job.html`). Each social button shares the page with its
 * channel tagged (ADR-0032 §1) — Viber and Telegram open links in browsers that send no referrer, so
 * without the tag an applicant arriving from a share there would count as the career page.
 *
 * Built after mount: the address is this page's, which the server render does not know.
 */
export function ShareButtons({ token }: { token: string }) {
  const [url, setUrl] = useState<string | null>(null);
  const [copied, setCopied] = useState(false);

  useEffect(() => {
    setUrl(`${window.location.origin}/jobs/${encodeURIComponent(token)}`);
  }, [token]);

  if (!url) return null;

  const button =
    'inline-flex h-9 items-center rounded-md border border-line px-3 text-sm font-medium transition-colors hover:border-line-strong';

  async function copy() {
    try {
      await navigator.clipboard.writeText(url as string); // set before this button renders
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch {
      // No clipboard (an insecure context or a refused permission): the address bar still has it.
    }
  }

  return (
    <div className="mt-6 flex flex-wrap items-center gap-2 border-t border-line pt-4">
      <p className="mr-1 text-sm text-ink-600">Share:</p>
      <button type="button" onClick={copy} className={button}>
        {copied ? 'Copied' : 'Copy link'}
      </button>
      <a
        className={button}
        target="_blank"
        rel="noopener noreferrer"
        href={`https://www.facebook.com/sharer/sharer.php?u=${encodeURIComponent(tagged(url, 'facebook'))}`}
      >
        Facebook
      </a>
      <a className={button} href={`viber://forward?text=${encodeURIComponent(tagged(url, 'viber'))}`}>
        Viber
      </a>
      <a
        className={button}
        target="_blank"
        rel="noopener noreferrer"
        href={`https://t.me/share/url?url=${encodeURIComponent(tagged(url, 'telegram'))}`}
      >
        Telegram
      </a>
    </div>
  );
}
