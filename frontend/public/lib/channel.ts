import type { PublicSourceChannel } from '@recruitops/types';

// Where an applicant came from (ADR-0032 §1), resolved in the browser because only the browser knows its
// referrer. Order: the link's own `via` tag, Facebook's `fbclid`, then the referrer's host. Only the host
// is ever read, and none of it leaves the page — the form sends just the channel's name.

const TAGS = new Map<string, PublicSourceChannel>([
  ['facebook', 'Facebook'],
  ['linkedin', 'LinkedIn'],
  ['telegram', 'Telegram'],
  ['viber', 'Viber'],
  ['referral', 'Referral'],
]);

const HOSTS: [string, PublicSourceChannel][] = [
  ['facebook.com', 'Facebook'],
  ['fb.com', 'Facebook'],
  ['fb.me', 'Facebook'],
  ['linkedin.com', 'LinkedIn'],
  ['lnkd.in', 'LinkedIn'],
  ['t.me', 'Telegram'],
  ['telegram.me', 'Telegram'],
  ['telegram.org', 'Telegram'],
  ['viber.com', 'Viber'],
];

/** The channel for a page at `href` reached from `referrer`, or null for the career page. */
export function resolveChannel(href: string, referrer: string): PublicSourceChannel | null {
  let page: URL;
  try {
    page = new URL(href);
  } catch {
    return null;
  }

  const via = TAGS.get((page.searchParams.get('via') ?? '').trim().toLowerCase());
  if (via) return via;
  if (page.searchParams.has('fbclid')) return 'Facebook';

  let host: string;
  try {
    host = new URL(referrer).hostname.toLowerCase();
  } catch {
    return null;
  }
  const match = HOSTS.find(([domain]) => host === domain || host.endsWith(`.${domain}`));
  return match ? match[1] : null;
}

/** `url` carrying a channel tag, replacing any it had. */
export function tagged(url: string, channel: 'facebook' | 'linkedin' | 'telegram' | 'viber'): string {
  const out = new URL(url);
  out.searchParams.set('via', channel);
  return out.toString();
}
