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
      <body>{children}</body>
    </html>
  );
}
