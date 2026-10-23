import type { Metadata } from 'next';
import { OfferPortal } from './OfferPortal';

// The candidate's offer link (Module 4.3, ADR-0030 §5a), drawn in `design/public/offer-portal.html`.
//
// Unlike the job page, nothing is fetched while rendering on the server. Everything here is per
// candidate and private, so there is nothing to put in an Open Graph card — and a server-side call
// would reach the API from this app's address, putting every candidate in one rate-limit bucket.
// The browser calls the API itself, through the same-origin rewrite.

export const metadata: Metadata = {
  title: 'Your offer',
  robots: { index: false, follow: false },
  // The token is in the path. Never send it anywhere as a Referer.
  referrer: 'no-referrer',
};

export default function OfferPage({ params }: { params: { token: string } }) {
  return <OfferPortal token={params.token} />;
}
