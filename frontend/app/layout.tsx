import type { Metadata } from 'next';
import './globals.css';

export const metadata: Metadata = {
  title: 'RecruitOps',
  description: 'Your agency, running on rails.',
};

// Internal-app shell. The client portal (app/portal) uses its own bare layout.
export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en">
      <body>{children}</body>
    </html>
  );
}
