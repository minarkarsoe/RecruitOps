import type { Metadata } from 'next';
import './globals.css';

// Public surface: no navigation, no sidebar — the client portal is a single
// centred column (design system §4). Nothing here is authenticated.
export const metadata: Metadata = {
  title: 'Careers — RecruitOps',
  description: 'Open positions.',
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en">
      <head>
        <link rel="preconnect" href="https://fonts.googleapis.com" />
        <link rel="preconnect" href="https://fonts.gstatic.com" crossOrigin="anonymous" />
        {/* V1.0 (ADR-0025): Inter + Noto Sans Myanmar only. No Bricolage Grotesque — V1.0 has
            no display face — and no mono face at all on this surface: a candidate reading a job
            ad on a phone over a slow connection should not pay for one nobody sees. */}
        <link
          href="https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&family=Noto+Sans+Myanmar:wght@400;600;700&display=swap"
          rel="stylesheet"
        />
      </head>
      <body>{children}</body>
    </html>
  );
}
