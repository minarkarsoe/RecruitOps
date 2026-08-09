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
        <link
          href="https://fonts.googleapis.com/css2?family=Bricolage+Grotesque:opsz,wght@12..96,600;12..96,700&family=IBM+Plex+Mono:wght@400;600&family=Inter:wght@400;500;600;700&family=Noto+Sans+Myanmar:wght@400;600;700&display=swap"
          rel="stylesheet"
        />
      </head>
      <body>{children}</body>
    </html>
  );
}
