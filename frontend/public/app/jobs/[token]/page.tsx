import type { Metadata } from 'next';
import { notFound } from 'next/navigation';
import type { PublicJob } from '@recruitops/types';
import { api } from '../../../lib/api';
import { ApplicationForm } from './ApplicationForm';

// Public job page (Module 2.1). This is the SSR half of the frontend split
// (ADR-0012): it exists as a server-rendered page specifically so that shares to
// Facebook, Telegram and Viber unfurl with an Open Graph preview card, which a
// client-rendered SPA cannot produce.
//
// ⚠️ Repurposed surface — in the agency model this route was a client CV-review
// portal. That meaning is gone (ADR-0001); this page is for APPLICANTS.

interface Props {
  params: { token: string };
}

/** Fetch failures are indistinguishable from "no such job" on purpose — the API
 *  already returns one 404 for unknown, revoked, expired and unpublished tokens, and
 *  the page must not undo that by rendering a different error for each. */
async function loadJob(token: string): Promise<PublicJob | null> {
  try {
    return await api<PublicJob>(`/public/jobs/${encodeURIComponent(token)}`);
  } catch {
    return null;
  }
}

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const job = await loadJob(params.token);

  if (!job) {
    return { title: 'Position not found', robots: { index: false, follow: false } };
  }

  const title = `${job.title} — ${job.companyName}`;
  // The JD is long-form text; an unfurl card gets one or two lines. Cut on a word
  // boundary so a share preview never ends mid-word.
  const description = summarize(job.description, 200);

  return {
    title,
    description,
    openGraph: {
      title,
      description,
      type: 'website',
      siteName: job.companyName,
    },
    twitter: { card: 'summary_large_image', title, description },
    // Unlisted links should not be indexed — the link is shared deliberately,
    // and a closed vacancy shouldn't linger in search results.
    robots: { index: false, follow: false },
  };
}

function summarize(text: string, max: number): string {
  const flat = text.replace(/\s+/g, ' ').trim();
  if (flat.length <= max) return flat;
  const cut = flat.slice(0, max);
  const lastSpace = cut.lastIndexOf(' ');
  return `${cut.slice(0, lastSpace > 0 ? lastSpace : max)}…`;
}

export default async function PublicJobPage({ params }: Props) {
  const job = await loadJob(params.token);
  if (!job) notFound();

  return (
    <main className="mx-auto max-w-[760px] px-6 py-12">
      <p className="text-[13px] font-semibold uppercase tracking-wide text-ink-600">
        {job.companyName}
      </p>
      <h1 className="mt-1 font-display text-[32px] font-bold leading-10">{job.title}</h1>

      <dl className="mt-4 flex flex-wrap gap-x-6 gap-y-2 text-[15px] text-ink-600">
        {job.location && (
          <div className="flex gap-2">
            <dt className="sr-only">Location</dt>
            <dd>{job.location}</dd>
          </div>
        )}
        <div className="flex gap-2">
          <dt className="sr-only">Employment type</dt>
          <dd>{humanizeEmploymentType(job.employmentType)}</dd>
        </div>
        {/* Only present when the posting opted in — see PublicJobDto. */}
        {job.salaryRange && (
          <div className="flex gap-2">
            <dt className="sr-only">Salary</dt>
            <dd className="font-mono">{job.salaryRange}</dd>
          </div>
        )}
      </dl>

      <article className="mt-10 whitespace-pre-wrap text-[15px] leading-relaxed">
        {job.description}
      </article>

      <div className="mt-12 rounded-lg border border-line-200 bg-surface-0 p-8 shadow-card">
        {job.isOpen ? (
          <ApplicationForm token={params.token} formFieldsJson={job.applicationFormFieldsJson} />
        ) : (
          <>
            <h2 className="font-display text-[19px] font-semibold">Applications are closed</h2>
            <p className="mt-2 text-[15px] text-ink-600">
              This vacancy is no longer accepting applications. The page is kept so that
              links already shared don&apos;t break.
            </p>
          </>
        )}
      </div>
    </main>
  );
}

function humanizeEmploymentType(value: PublicJob['employmentType']): string {
  const labels: Record<PublicJob['employmentType'], string> = {
    FullTime: 'Full-time',
    PartTime: 'Part-time',
    Contract: 'Contract',
    Internship: 'Internship',
    Temporary: 'Temporary',
  };
  return labels[value];
}
