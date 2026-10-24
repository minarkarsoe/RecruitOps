import { useEffect, useState } from 'react';
import type {
  AcceptanceMetricDto, DepartmentVacanciesDto, DurationMetricDto, ExecutiveDashboardDto, ExecutiveDashboardScope,
  FilterOptionDto, ReportingPeriodDto, ReportingPeriodKind,
} from '@recruitops/types';
import { ApiError } from '../lib/api';
import { analyticsApi } from '../features/analytics';
import { calendarDate } from './offerFormat';

/**
 * Module 5.1 — the Executive dashboard, built from `design/internal/analytics-dashboard.html` (ADR-0031).
 *
 * Every number is the server's, computed over the viewer's own rows: a Recruiter's tiles are the
 * offers they made, a Hiring Manager's are their departments'. A Hiring Manager's response carries no
 * Time-to-Hire and no acceptance rate at all, so those tiles are absent because the data is — this
 * page hides nothing it was given.
 *
 * Two things the design draws are notes to the product owner, not screen: the warn box proposing a
 * fourth "time in approval" tile (outside the requirement, not built) and the "same screen for other
 * roles" panel (the rules above).
 */

const PERIODS: ReportingPeriodKind[] = ['Month', 'Quarter', 'Year'];

const SCOPE_LABEL: Record<ExecutiveDashboardScope, string> = {
  Company: 'Company-wide',
  Recruiter: 'Offers you made',
  Department: 'Your departments',
};

const yearOf = (date: string) => Number(date.slice(0, 4));
const monthOf = (date: string) => Number(date.slice(5, 7)); // 1–12

/** `Q3 2026`, `August 2026`, `2026` — with the year left off when it is the current period's. */
export function periodName(p: ReportingPeriodDto, currentYear?: number): string {
  const year = yearOf(p.from);
  const suffix = year === currentYear ? '' : ` ${year}`;
  if (p.kind === 'Year') return String(year);
  if (p.kind === 'Quarter') return `Q${Math.floor((monthOf(p.from) - 1) / 3) + 1}${suffix}`;
  const month = new Date(`${p.from}T00:00:00Z`).toLocaleDateString(undefined, { month: 'long', timeZone: 'UTC' });
  return `${month}${suffix}`;
}

/** `17.5`, `34` — the API rounds to one decimal; a whole number drops the `.0`. */
function days(n: number): string {
  return Number.isInteger(n) ? String(n) : n.toFixed(1);
}

type Tone = 'good' | 'bad' | 'same';

interface Delta {
  tone: Tone;
  text: string;
}

/** Fewer days is better. Null when there is nothing this period to compare. */
export function durationDelta(m: DurationMetricDto, than: string): Delta | null {
  if (m.days === null) {
    return m.previousDays === null ? null : { tone: 'same', text: `${days(m.previousDays)} days in ${than}` };
  }
  if (m.previousDays === null) return { tone: 'same', text: `No offers accepted in ${than}` };
  const diff = Math.round((m.days - m.previousDays) * 10) / 10;
  if (diff === 0) return { tone: 'same', text: `Unchanged from ${than}` };
  const n = Math.abs(diff);
  return { tone: diff < 0 ? 'good' : 'bad', text: `${days(n)} ${n === 1 ? 'day' : 'days'} ${diff < 0 ? 'faster' : 'slower'} than ${than}` };
}

/** In whole points, from the rates as shown — so the line never contradicts the two numbers. */
export function acceptanceDelta(a: AcceptanceMetricDto, than: string): Delta | null {
  if (a.rate === null) {
    return a.previousRate === null ? null : { tone: 'same', text: `${Math.round(a.previousRate)}% in ${than}` };
  }
  if (a.previousRate === null) return { tone: 'same', text: `No offers decided in ${than}` };
  const diff = Math.round(a.rate) - Math.round(a.previousRate);
  if (diff === 0) return { tone: 'same', text: `Unchanged from ${than}` };
  const n = Math.abs(diff);
  return { tone: diff > 0 ? 'good' : 'bad', text: `${n} ${n === 1 ? 'point' : 'points'} ${diff > 0 ? 'above' : 'below'} ${than}` };
}

const TONE_CLASS: Record<Tone, string> = {
  good: 'text-positive-700',
  bad: 'text-critical-700',
  same: 'text-ink-600',
};

/** The kit's arrows say whether the change is good news, not which way the number moved: a
 *  shorter Time-to-Hire is drawn as an up arrow. */
const TONE_PATH: Record<Tone, JSX.Element> = {
  good: <path d="M7 11V3M3.5 6.5L7 3l3.5 3.5" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" />,
  bad: <path d="M7 3v8M3.5 7.5L7 11l3.5-3.5" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" />,
  same: <path d="M3 7h8" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" />,
};

function Tile({ label, value, unit, empty, caption, delta }: {
  label: string;
  value: string | null;
  unit: string;
  /** Shown in place of the value when there is none — never a 0. */
  empty: string;
  caption: string;
  delta: Delta | null;
}) {
  return (
    <div className="rounded-lg border border-line bg-white p-5">
      <p className="text-sm text-ink-600">{label}</p>
      {value === null ? (
        <p className="mt-1.5 flex h-9 items-center text-xl font-medium text-ink-600">{empty}</p>
      ) : (
        <p className="tnum mt-1.5 text-3xl font-semibold">
          {value}
          <span className="text-xl font-medium text-ink-600">{unit === '%' ? unit : ` ${unit}`}</span>
        </p>
      )}
      <p className="mt-1.5 text-sm text-ink-600">{caption}</p>
      {delta && (
        <p className={`mt-3 inline-flex items-center gap-1.5 text-sm ${TONE_CLASS[delta.tone]}`}>
          <svg className="h-3.5 w-3.5" viewBox="0 0 14 14" fill="none" aria-hidden="true">{TONE_PATH[delta.tone]}</svg>
          {delta.text}
        </p>
      )}
    </div>
  );
}

function Vacancies({ rows, open }: { rows: DepartmentVacanciesDto[]; open: number }) {
  const [asTable, setAsTable] = useState(false);
  const max = Math.max(...rows.map((r) => r.open), 1);

  return (
    <section className="rounded-lg border border-line bg-white" aria-labelledby="vacancies-heading">
      <header className="flex h-12 items-center gap-3 border-b border-line px-5">
        <h2 id="vacancies-heading" className="text-base font-semibold">Active vacancies by department</h2>
        <span className="tnum text-sm text-ink-600">{open} open</span>
        {rows.length > 0 && (
          <button
            type="button"
            onClick={() => setAsTable((t) => !t)}
            className="ml-auto inline-flex h-8 items-center rounded-md border border-line bg-white px-3 text-sm font-medium transition-colors hover:border-line-strong"
          >
            {asTable ? 'View as chart' : 'View as table'}
          </button>
        )}
      </header>

      {rows.length === 0 ? (
        <p className="p-5 text-center text-sm text-ink-600">
          No open vacancies. A vacancy is a seat on an approved requisition that has no accepted offer yet.
        </p>
      ) : asTable ? (
        // Required, not an afterthought: it is how this data is read by anyone the chart fails, and
        // how it gets pasted into a board pack.
        <div className="p-5">
          <table className="w-full text-base">
            <thead>
              <tr className="border-b border-line text-left text-sm">
                <th className="pb-2 font-medium text-ink-600">Department</th>
                <th className="pb-2 text-right font-medium text-ink-600">Open vacancies</th>
                <th className="pb-2 text-right font-medium text-ink-600">Share</th>
              </tr>
            </thead>
            <tbody className="[&>tr:last-child]:border-0 [&>tr]:border-b [&>tr]:border-line">
              {rows.map((r) => (
                <tr key={r.departmentId}>
                  <td className="py-2">{r.departmentName}</td>
                  <td className="tnum py-2 text-right font-mono">{r.open}</td>
                  <td className="tnum py-2 text-right font-mono text-ink-600">{Math.round((r.open / open) * 100)}%</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : (
        <div className="p-5">
          <div className="space-y-2.5" role="img" aria-label="Active vacancies by department, horizontal bar chart">
            {rows.map((r) => (
              <div
                key={r.departmentId}
                className="bar-row grid grid-cols-[160px_1fr_44px] items-center gap-3"
                title={`${r.departmentName} — ${r.open} open ${r.open === 1 ? 'vacancy' : 'vacancies'}`}
              >
                <span className="truncate text-base text-ink-700">{r.departmentName}</span>
                <span className="bar-track block h-6">
                  <span className="bar-fill block" style={{ width: `${(r.open / max) * 100}%` }} />
                </span>
                <span className="tnum text-right font-mono text-base font-medium">{r.open}</span>
              </div>
            ))}
          </div>
        </div>
      )}
    </section>
  );
}

export function ExecutiveDashboardPage() {
  const [period, setPeriod] = useState<ReportingPeriodKind>('Quarter');
  const [departmentId, setDepartmentId] = useState('');
  const [recruiterId, setRecruiterId] = useState('');
  const [data, setData] = useState<ExecutiveDashboardDto | null>(null);
  // The filters' choices outlive a failed load, so an error never takes away the filter that caused it.
  const [options, setOptions] = useState<{ departments: FilterOptionDto[]; recruiters: FilterOptionDto[] } | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    // A slower answer to an earlier filter must not overwrite a newer one.
    let current = true;
    setLoading(true);
    setError(null);
    analyticsApi
      .getExecutive({ period, departmentId: departmentId || undefined, recruiterId: recruiterId || undefined })
      .then((dto) => {
        if (!current) return;
        setData(dto);
        // Recruiters arrive only for company-wide viewers (the API ignores the filter for anyone else).
        setOptions({ departments: dto.departments, recruiters: dto.scope === 'Company' ? dto.recruiters : [] });
      })
      .catch((e: unknown) => {
        if (!current) return;
        setData(null);
        setError(e instanceof ApiError && e.status === 404
          ? 'Nothing on this dashboard is within your reach.'
          : e instanceof Error ? e.message : 'Could not load the dashboard.');
      })
      .finally(() => { if (current) setLoading(false); });
    return () => { current = false; };
  }, [period, departmentId, recruiterId]);

  const currentYear = data ? yearOf(data.period.from) : undefined;
  const than = data ? periodName(data.previous, currentYear) : '';

  return (
    <>
      <div className="mb-4 flex flex-wrap items-center gap-x-4 gap-y-2">
        <h1 className="text-xl font-semibold tracking-tight">Executive dashboard</h1>
        {data && (
          <span className="inline-flex h-6 items-center whitespace-nowrap rounded-full bg-brand-50 px-2.5 text-xs font-medium text-brand-800">
            {SCOPE_LABEL[data.scope]}
          </span>
        )}
      </div>

      {/* Filters in one row above the tiles, detented like everywhere else. */}
      <div className="mb-5 flex flex-wrap items-center gap-3">
        <div className="flex items-center rounded-md border border-line bg-white p-0.5" role="group" aria-label="Period">
          {PERIODS.map((p) => (
            <button
              key={p}
              type="button"
              aria-pressed={period === p}
              onClick={() => setPeriod(p)}
              className={`h-7 rounded px-2.5 text-sm transition-colors ${
                period === p ? 'bg-ink-900 font-medium text-white' : 'text-ink-600 hover:text-ink-900'}`}
            >
              {p}
            </button>
          ))}
        </div>
        {options && options.departments.length > 0 && (
          <select
            aria-label="Department" value={departmentId} onChange={(e) => setDepartmentId(e.target.value)}
            className="h-8 rounded-md border border-line bg-white px-2.5 text-sm"
          >
            <option value="">All departments</option>
            {options.departments.map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
          </select>
        )}
        {/* Only company-wide viewers choose: a recruiter's rows are their own whatever they ask. */}
        {options && options.recruiters.length > 0 && (
          <select
            aria-label="Recruiter" value={recruiterId} onChange={(e) => setRecruiterId(e.target.value)}
            className="h-8 rounded-md border border-line bg-white px-2.5 text-sm"
          >
            <option value="">All recruiters</option>
            {options.recruiters.map((r) => <option key={r.id} value={r.id}>{r.name}</option>)}
          </select>
        )}
        {data && (
          <span className="tnum ml-auto text-sm text-ink-600">
            {periodName(data.period)} · {calendarDate(data.period.from, false)} – {calendarDate(data.period.to, false)}
          </span>
        )}
      </div>

      {error && <p role="alert" className="mb-4 text-md text-critical-700">{error}</p>}

      {data === null && loading && (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3" data-testid="executive-skeleton">
          {[0, 1, 2].map((i) => (
            <div key={i} className="rounded-lg border border-line bg-white p-5">
              <span className="skeleton block h-4 w-24" />
              <span className="skeleton mt-3 block h-8 w-32" />
              <span className="skeleton mt-3 block h-4 w-48" />
            </div>
          ))}
        </div>
      )}

      {data && (
        <div className={`space-y-5 transition-opacity ${loading ? 'opacity-60' : ''}`} aria-busy={loading}>
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {data.timeToHire && (
              <Tile
                label="Time-to-Hire"
                value={data.timeToHire.days === null ? null : days(data.timeToHire.days)}
                unit="days"
                empty="No offers accepted"
                caption="Application received → offer accepted"
                delta={durationDelta(data.timeToHire, than)}
              />
            )}
            <Tile
              label="Time-to-Fill"
              value={data.timeToFill.days === null ? null : days(data.timeToFill.days)}
              unit="days"
              empty="No offers accepted"
              caption="Requisition approved → offer accepted"
              delta={durationDelta(data.timeToFill, than)}
            />
            {data.acceptance && (
              <Tile
                label="Offer acceptance rate"
                value={data.acceptance.rate === null ? null : String(Math.round(data.acceptance.rate))}
                unit="%"
                empty="No offers decided"
                caption={data.acceptance.decided === 0
                  ? 'None accepted, declined or expired yet'
                  : `${data.acceptance.accepted} of ${data.acceptance.decided} ${data.acceptance.decided === 1 ? 'offer' : 'offers'} accepted`}
                delta={acceptanceDelta(data.acceptance, than)}
              />
            )}
          </div>

          <Vacancies rows={data.vacancies} open={data.openVacancies} />
        </div>
      )}
    </>
  );
}

export default ExecutiveDashboardPage;
