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
    // 720px and the kit's rhythm, per `design/public/job.html`.
    <main className="mx-auto max-w-[720px] px-5 py-8">
      {/* No uppercase. The kit does not set a heading or an eyebrow in caps anywhere, and a
          company name is a proper noun — capitalising it costs the reader the word's own shape. */}
      <p className="text-sm font-medium text-ink-600">{job.companyName}</p>

      {/* 24px, down from 32. The kit's page title is `text-3xl font-bold tracking-tight`; 32px
          was a size the type scale does not contain. */}
      <h1 className="mt-3 text-3xl font-bold tracking-tight">{job.title}</h1>

      {/* Pills, per the kit. Each fact is a discrete thing about the job, and a row of
          space-separated phrases makes "Yangon" and "Full-time" read as one sentence. */}
      <dl className="mt-3 flex flex-wrap items-center gap-2">
        {job.location && (
          <div className="inline-flex h-7 items-center rounded-full border border-line bg-white px-3 text-sm">
            <dt className="sr-only">Location</dt>
            <dd>{job.location}</dd>
          </div>
        )}
        <div className="inline-flex h-7 items-center rounded-full border border-line bg-white px-3 text-sm">
          <dt className="sr-only">Employment type</dt>
          <dd>{humanizeEmploymentType(job.employmentType)}</dd>
        </div>
        {/* Only present when the posting opted in — see PublicJobDto. */}
        {job.salaryRange && (
          <div className="inline-flex h-7 items-center rounded-full border border-line bg-white px-3 text-sm">
            <dt className="sr-only">Salary</dt>
            <dd className="font-mono tnum">{job.salaryRange}</dd>
          </div>
        )}
      </dl>

      <article className="mt-8 whitespace-pre-wrap text-md leading-relaxed text-ink-700">
        {job.description}
      </article>

      {/* Border, not a shadow. The kit's rule for every panel: cards sit on their border, and a
          page of drop-shadowed boxes reads as a dashboard demo rather than a job advert. */}
      <div className="mt-12 rounded-lg border border-line bg-white p-8">
        {job.isOpen ? (
          <ApplicationForm token={params.token} formFieldsJson={job.applicationFormFieldsJson} />
        ) : (
          <>
            <h2 className="text-xl font-semibold tracking-tight">Applications are closed</h2>
            <p className="mt-2 text-base text-ink-600">
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
