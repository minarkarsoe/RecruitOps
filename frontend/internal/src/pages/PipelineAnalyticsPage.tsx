import { useEffect, useState, type ReactNode } from 'react';
import type {
  ChannelGroup, ChannelShareDto, FilterOptionDto, FunnelBandDto, LeaderboardRowDto, PipelineAnalyticsDto, PipelineWindow,
} from '@recruitops/types';
import { ApiError } from '../lib/api';
import { analyticsApi } from '../features/analytics';

/**
 * Module 5.2 — Pipeline & source, built from `design/internal/analytics-pipeline.html` (ADR-0032).
 *
 * Every number is the server's. A Recruiter's response carries no leaderboard at all, so the table is
 * absent because the data is; a Hiring Manager has no access to the page (the API answers 404).
 *
 * Not built from the design: the Recruiter filter — nothing assigns a recruiter to a candidate, so the
 * leaderboard is the per-recruiter view (ADR-0032 §2) — and the two notes the designer wrote about the
 * sample numbers.
 */

const WINDOWS: { value: PipelineWindow; label: string }[] = [
  { value: '90d', label: 'Last 90 days' },
  { value: 'year', label: 'This year' },
];

const CHANNEL_LABEL: Record<ChannelGroup, string> = {
  Facebook: 'Facebook',
  LinkedIn: 'LinkedIn',
  CareerPage: 'Career page',
  ManualUpload: 'Manual upload',
  Other: 'Other',
};

/**
 * The design's validated categorical set, assigned by channel identity in a fixed order — never by rank,
 * so filtering a channel out does not repaint the survivors. "Other" is neutral: it is a remainder, not
 * an identity. The same colour marks the same channel in both source charts, which is the one place on
 * this page where colour carries identity.
 */
const CHANNEL_COLOUR: Record<ChannelGroup, string> = {
  Facebook: '#0D9488',
  LinkedIn: '#7C3AED',
  CareerPage: '#D97706',
  ManualUpload: '#0369A1',
  Other: '#94A3B8',
};

/**
 * A whole percentage of two counts, rounded once. Not `Math.round` of the API's one-decimal figure: 5 of
 * 11 is 45.45%, which the API sends as 45.5 and a second rounding would show as 46%.
 */
const percent = (part: number, whole: number): number | null => (whole === 0 ? null : Math.round((100 * part) / whole));
const pct = (n: number | null) => (n === null ? '—' : `${n}%`);

/** A ring of the channels' shares, with a 2px gap between segments as the design draws it. */
function Donut({ sources, total }: { sources: ChannelShareDto[]; total: number }) {
  const r = 70;
  const circumference = 2 * Math.PI * r;
  const present = sources.filter((s) => s.candidates > 0);
  const gap = present.length > 1 ? 2 : 0;
  let offset = 0;
  const label = present.map((s) => `${CHANNEL_LABEL[s.channel]} ${pct(percent(s.candidates, total))}`).join(', ');

  return (
    <svg width="168" height="168" viewBox="0 0 168 168" className="shrink-0" role="img"
      aria-label={`Share of candidates by source: ${label}`}>
      <g transform="rotate(-90 84 84)" fill="none" strokeWidth="22">
        {present.map((s) => {
          const length = (s.candidates / total) * circumference;
          const arc = Math.max(length - gap, 0);
          const circle = (
            <circle key={s.channel} cx="84" cy="84" r={r} stroke={CHANNEL_COLOUR[s.channel]}
              strokeDasharray={`${arc} ${circumference - arc}`} strokeDashoffset={-offset} />
          );
          offset += length;
          return circle;
        })}
      </g>
    </svg>
  );
}

function Section({ title, aside, children, footer }: {
  title: string; aside?: ReactNode; children: ReactNode; footer?: ReactNode;
}) {
  return (
    <section className="rounded-lg border border-line bg-white" aria-label={title}>
      <header className="flex h-12 items-center gap-3 border-b border-line px-5">
        <h2 className="text-base font-semibold">{title}</h2>
        {aside}
      </header>
      {children}
      {footer}
    </section>
  );
}

function Sources({ data }: { data: PipelineAnalyticsDto }) {
  const shown = data.sources.filter((s) => s.channel !== 'Other' || s.candidates > 0);
  return (
    <Section title="Where candidates come from"
      aside={<span className="tnum ml-auto text-sm text-ink-600">{data.candidates} candidate{data.candidates === 1 ? '' : 's'}</span>}>
      {data.candidates === 0 ? (
        <p className="p-5 text-center text-sm text-ink-600">No applications arrived in this period.</p>
      ) : (
        <div className="flex flex-col items-center gap-6 p-5 sm:flex-row">
          <Donut sources={shown} total={data.candidates} />
          {/* A legend, because there are several series — and each is labelled with its value, so identity
              is never colour alone. */}
          <ul className="w-full min-w-0 flex-1 space-y-2.5">
            {shown.map((s) => (
              <li key={s.channel} className="flex items-center gap-2.5">
                <span className="h-3 w-3 shrink-0 rounded-sm" style={{ background: CHANNEL_COLOUR[s.channel] }} aria-hidden="true" />
                <span className="min-w-0 flex-1 truncate text-base text-ink-700"
                  title={s.channel === 'Other' ? 'Telegram, Viber, referrals and Excel imports' : undefined}>
                  {CHANNEL_LABEL[s.channel]}
                </span>
                <span className="tnum font-mono text-base font-medium">{pct(percent(s.candidates, data.candidates))}</span>
              </li>
            ))}
          </ul>
        </div>
      )}
    </Section>
  );
}

/** A separate chart on its own scale, as the design insists: volume is not quality. */
function Hired({ data }: { data: PipelineAnalyticsDto }) {
  const rate = (s: ChannelShareDto) => s.hired / s.candidates;
  const rows = data.sources.filter((s) => s.candidates > 0).sort((a, b) => rate(b) - rate(a));
  const max = Math.max(...rows.map(rate), 0);

  return (
    <Section title="Which of them get hired" aside={<span className="ml-auto text-sm text-ink-600">% of that channel&apos;s candidates</span>}>
      {rows.length === 0 ? (
        <p className="p-5 text-center text-sm text-ink-600">No applications arrived in this period.</p>
      ) : (
        <div className="space-y-2.5 p-5" role="img" aria-label="Share of each channel's candidates hired, horizontal bar chart">
          {rows.map((s) => (
            <div key={s.channel} className="grid grid-cols-[120px_1fr_52px] items-center gap-3"
              title={`${CHANNEL_LABEL[s.channel]} — ${s.hired} of ${s.candidates} hired`}>
              <span className="flex min-w-0 items-center gap-2">
                <span className="h-3 w-3 shrink-0 rounded-sm" style={{ background: CHANNEL_COLOUR[s.channel] }} aria-hidden="true" />
                <span className="truncate text-base text-ink-700">{CHANNEL_LABEL[s.channel]}</span>
              </span>
              <span className="bar-track block h-6">
                <span className="block h-full rounded-r"
                  style={{ width: `${max > 0 ? (rate(s) / max) * 100 : 0}%`, background: CHANNEL_COLOUR[s.channel] }} />
              </span>
              <span className="tnum text-right font-mono text-base font-medium">{pct(percent(s.hired, s.candidates))}</span>
            </div>
          ))}
        </div>
      )}
    </Section>
  );
}

/** A drop-off of half or more is red, as the design marks it; a smaller one is ink. */
function DropOff({ before, count }: { before: number | null; count: number }) {
  const whole = before === null ? null : percent(before - count, before);
  if (whole === null) return null;
  if (whole === 0) return <span className="font-sans text-sm font-normal text-ink-600">no drop-off</span>;
  return <span className={`text-sm font-normal ${whole >= 50 ? 'text-critical-700' : 'text-ink-600'}`}>−{whole}%</span>;
}

function Funnel({ bands, period }: { bands: FunnelBandDto[]; period: PipelineWindow }) {
  const first = bands[0]?.count ?? 0;
  return (
    <Section
      title="Where candidates drop off"
      aside={<span className="ml-auto text-sm text-ink-600">{WINDOWS.find((w) => w.value === period)?.label}</span>}
      footer={
        // The mapping, drawn: the requirement's four bands are not the pipeline's eight statuses.
        <footer className="border-t border-line bg-canvas px-5 py-4">
          <p className="text-sm font-medium">How these four bands map to the eight pipeline statuses</p>
          <div className="mt-2.5 flex flex-wrap gap-x-6 gap-y-2 text-sm">
            <span className="text-ink-600"><span className="font-medium text-ink-900">Sourcing</span> = Sourced + Applied</span>
            <span className="text-ink-600"><span className="font-medium text-ink-900">Screened</span> = Screening + Shortlisted</span>
            <span className="text-ink-600"><span className="font-medium text-ink-900">Interview</span> = Interview</span>
            <span className="text-ink-600"><span className="font-medium text-ink-900">Offered</span> = Offer + Hired</span>
          </div>
          <p className="mt-2.5 max-w-[80ch] text-sm text-ink-600">
            <span className="font-medium text-ink-900">Rejected has no band</span> — it is an exit from every stage.
            Each band counts everyone who reached it, so a candidate rejected after an interview is counted up to
            Interview.
          </p>
        </footer>
      }
    >
      {first === 0 ? (
        <p className="p-5 text-center text-sm text-ink-600">No applications arrived in this period.</p>
      ) : (
        <div className="space-y-3 p-5" role="img" aria-label="Candidates reaching each band, funnel chart">
          {bands.map((b, i) => (
            <div key={b.band} className="grid grid-cols-[120px_1fr_130px] items-center gap-3"
              title={`${b.band} — ${b.count} candidate${b.count === 1 ? '' : 's'}`}>
              <span className="text-base text-ink-700">{b.band}</span>
              <span className="bar-track block h-8">
                <span className="bar-fill block" style={{ width: `${(b.count / first) * 100}%` }} />
              </span>
              <span className="tnum font-mono text-base font-medium">
                {b.count} <DropOff before={i === 0 ? null : bands[i - 1].count} count={b.count} />
              </span>
            </div>
          ))}
        </div>
      )}
    </Section>
  );
}

function Leaderboard({ rows }: { rows: LeaderboardRowDto[] }) {
  return (
    <Section
      title="Recruiter leaderboard"
      aside={
        <span className="inline-flex h-6 items-center rounded-full bg-warn-50 px-2.5 text-xs font-medium text-warn-700">
          Staff performance data
        </span>
      }
      footer={
        <footer className="border-t border-line bg-warn-50 px-5 py-4">
          <p className="max-w-[84ch] text-sm text-warn-700">
            <strong className="font-semibold">Listed by name, not ranked.</strong> This is personal data about
            employees, visible to HR Directors and Admins only. Volume and conversion pull in different
            directions, so no single column says who is doing best.
          </p>
        </footer>
      }
    >
      {rows.length === 0 ? (
        <p className="p-5 text-center text-sm text-ink-600">
          Nobody uploaded a CV, held an interview or had an offer accepted in this period.
        </p>
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full text-base">
            <thead>
              <tr className="border-b border-line bg-canvas text-left text-sm">
                <th className="px-5 py-2.5 font-medium text-ink-600">Recruiter</th>
                <th className="px-5 py-2.5 text-right font-medium text-ink-600">CVs in</th>
                <th className="px-5 py-2.5 text-right font-medium text-ink-600">Interviews</th>
                <th className="px-5 py-2.5 text-right font-medium text-ink-600">Hires</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((r) => (
                <tr key={r.userId} className="border-b border-line last:border-b-0">
                  <td className="px-5 py-3">{r.name}</td>
                  <td className="tnum px-5 py-3 text-right font-mono">{r.cvsIn}</td>
                  <td className="tnum px-5 py-3 text-right font-mono">{r.interviews}</td>
                  <td className="tnum px-5 py-3 text-right font-mono font-medium">{r.hires}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </Section>
  );
}

export function PipelineAnalyticsPage() {
  const [period, setPeriod] = useState<PipelineWindow>('90d');
  const [postingId, setPostingId] = useState('');
  const [source, setSource] = useState<ChannelGroup | ''>('');
  const [data, setData] = useState<PipelineAnalyticsDto | null>(null);
  // The position choices outlive a failed load, so an error never takes away the filter that caused it.
  const [positions, setPositions] = useState<FilterOptionDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    // A slower answer to an earlier filter must not overwrite a newer one.
    let current = true;
    setLoading(true);
    setError(null);
    analyticsApi
      .getPipeline({ window: period, postingId: postingId || undefined, source: source || undefined })
      .then((dto) => {
        if (!current) return;
        setData(dto);
        setPositions(dto.positions);
      })
      .catch((e: unknown) => {
        if (!current) return;
        setData(null);
        setError(e instanceof ApiError && e.status === 404
          ? 'Nothing on this page is within your reach.'
          : e instanceof Error ? e.message : 'Could not load the analytics.');
      })
      .finally(() => { if (current) setLoading(false); });
    return () => { current = false; };
  }, [period, postingId, source]);

  const select = 'h-8 rounded-md border border-line bg-white px-2.5 text-sm';

  return (
    <>
      <div className="mb-4 flex flex-wrap items-center gap-x-4 gap-y-2">
        <h1 className="text-xl font-semibold tracking-tight">Pipeline &amp; source analytics</h1>
        <span className="inline-flex h-6 items-center whitespace-nowrap rounded-full bg-critical-50 px-2.5 text-xs font-medium text-critical-700">
          Not visible to Hiring Managers
        </span>
      </div>

      <div className="mb-5 flex flex-wrap items-center gap-3">
        <select aria-label="Period" value={period} onChange={(e) => setPeriod(e.target.value as PipelineWindow)} className={select}>
          {WINDOWS.map((w) => <option key={w.value} value={w.value}>{w.label}</option>)}
        </select>
        <select aria-label="Position" value={postingId} onChange={(e) => setPostingId(e.target.value)} className={select}>
          <option value="">All positions</option>
          {positions.map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}
        </select>
        <select aria-label="Source" value={source} onChange={(e) => setSource(e.target.value as ChannelGroup | '')} className={select}>
          <option value="">All sources</option>
          {(Object.keys(CHANNEL_LABEL) as ChannelGroup[]).map((c) => <option key={c} value={c}>{CHANNEL_LABEL[c]}</option>)}
        </select>
      </div>

      {error && <p role="alert" className="mb-4 text-md text-critical-700">{error}</p>}

      {data === null && loading && (
        <div className="grid gap-5 lg:grid-cols-2" data-testid="pipeline-skeleton">
          {[0, 1].map((i) => (
            <div key={i} className="rounded-lg border border-line bg-white p-5">
              <span className="skeleton block h-4 w-40" />
              <span className="skeleton mt-4 block h-32 w-full" />
            </div>
          ))}
        </div>
      )}

      {data && (
        <div className={`space-y-5 transition-opacity ${loading ? 'opacity-60' : ''}`} aria-busy={loading}>
          <div className="grid gap-5 lg:grid-cols-2">
            <Sources data={data} />
            <Hired data={data} />
          </div>
          <Funnel bands={data.funnel} period={data.window} />
          {data.leaderboard && <Leaderboard rows={data.leaderboard} />}
        </div>
      )}
    </>
  );
}

export default PipelineAnalyticsPage;
