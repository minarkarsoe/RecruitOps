import { useCallback, useEffect, useState } from "react";
import type {
  AuditCategory,
  AuditLogEntry,
  PagedResult,
} from "@recruitops/types";
import { api } from "../lib/api";

// Built against the "Audit log" tab drawn in `design/internal/users-roles.html` (ADR-0027) — drawn
// for this screen from the kit's own pieces, because the index listed the viewer as not drawn.
//
// The screen's rules, from that drawing:
//   - Every row is a sentence a person can read; the before/after is one click away.
//   - Red means someone was turned away — a failed sign-in or a blocked access change — never an
//     ordinary change. The server decides which rows those are (`isRefusal`).
//   - There is no edit and no delete, here or anywhere.

const PAGE_SIZE = 25;

const FILTERS: { value: AuditCategory | "all"; label: string }[] = [
  { value: "all", label: "All" },
  { value: "SignIns", label: "Sign-ins" },
  { value: "Users", label: "Users" },
  { value: "Roles", label: "Roles" },
  { value: "Refused", label: "Refused" },
];

/** An audit log goes back months: every row carries its date, not just a time. */
function formatWhen(raw: string): string {
  const date = new Date(raw);
  if (Number.isNaN(date.getTime())) return "—";
  return date.toLocaleString(undefined, {
    day: "numeric",
    month: "short",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
}

/** "permissionsAdded" → "Permissions added". */
function humanise(key: string): string {
  const words = key.replace(/([a-z0-9])([A-Z])/g, "$1 $2").toLowerCase();
  return words.charAt(0).toUpperCase() + words.slice(1);
}

function isFromTo(value: unknown): value is { from: unknown; to: unknown } {
  return (
    typeof value === "object" &&
    value !== null &&
    "from" in value &&
    "to" in value
  );
}

function formatValue(value: unknown): string {
  if (value === null || value === undefined) return "—";
  if (isFromTo(value))
    return `${formatValue(value.from)} → ${formatValue(value.to)}`;
  if (Array.isArray(value)) {
    if (value.length === 0) return "—";
    return value
      .map((v) =>
        typeof v === "object" && v !== null ? JSON.stringify(v) : String(v),
      )
      .join(", ");
  }
  if (typeof value === "object") return JSON.stringify(value);
  return String(value);
}

/** Keys with nothing to say — a null, an empty list — are left out rather than shown as "—". */
function detailRows(details: Record<string, unknown>): [string, string][] {
  return Object.entries(details)
    .filter(
      ([, v]) =>
        v !== null && v !== undefined && !(Array.isArray(v) && v.length === 0),
    )
    .map(([k, v]) => [humanise(k), formatValue(v)]);
}

function WhoCell({ entry }: { entry: AuditLogEntry }) {
  // A null actor is someone who never got in (named by the email they tried), whoever held a
  // stolen session token (nobody to name), or the system itself (install, startup).
  const anonymousSignIn = entry.actorUserId === null && entry.action === 'SignInFailed';
  const stolenToken = entry.actorUserId === null && entry.action === 'RefreshTokenReused';
  const subline =
    entry.actorUserId !== null ? null : anonymousSignIn ? 'not signed in' : stolenToken ? 'held a stolen session token' : 'system';
  return (
    <>
      <span className={`block ${anonymousSignIn ? 'font-mono text-sm' : 'mm'}`}>
        {entry.actorName ?? (stolenToken ? 'Unknown' : '—')}
      </span>
      {subline && <span className="block text-xs text-ink-500">{subline}</span>}
    </>
  );
}

function WhatCell({ entry }: { entry: AuditLogEntry }) {
  const pill = entry.isRefusal
    ? "bg-critical-50 text-critical-700"
    : "border border-line bg-canvas text-ink-600";
  const rows = entry.details ? detailRows(entry.details) : [];

  return (
    <>
      <span
        className={`inline-flex h-6 items-center gap-1.5 rounded-full px-2.5 text-xs font-medium ${pill}`}
      >
        {entry.actionLabel}
      </span>
      <span className="mm mt-1 block text-ink-700">{entry.summary}</span>
      {rows.length > 0 && (
        <details className="mt-1.5 text-sm">
          <summary className="cursor-pointer text-brand-700">Details</summary>
          <dl className="mt-2 grid grid-cols-[max-content_1fr] gap-x-4 gap-y-1 text-ink-700">
            {rows.map(([label, value]) => (
              <div key={label} className="contents">
                <dt className="text-ink-500">{label}</dt>
                <dd className="break-words font-mono text-xs leading-5">
                  {value}
                </dd>
              </div>
            ))}
            {entry.traceId && (
              <div className="contents">
                <dt className="text-ink-500">Trace id</dt>
                <dd className="break-all font-mono text-xs leading-5">
                  {entry.traceId}
                </dd>
              </div>
            )}
          </dl>
        </details>
      )}
    </>
  );
}

export function AuditLogPage() {
  const [category, setCategory] = useState<AuditCategory | "all">("all");
  const [pageNumber, setPageNumber] = useState(1);
  const [result, setResult] = useState<PagedResult<AuditLogEntry> | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const filter = category === "all" ? "" : `&category=${category}`;
      setResult(
        await api<PagedResult<AuditLogEntry>>(
          `/audit-log?page=${pageNumber}&pageSize=${PAGE_SIZE}${filter}`,
        ),
      );
    } catch (e) {
      setError(
        e instanceof Error ? e.message : "Could not load the audit log.",
      );
    } finally {
      setLoading(false);
    }
  }, [category, pageNumber]);

  useEffect(() => {
    load();
  }, [load]);

  function choose(value: AuditCategory | "all") {
    setCategory(value);
    setPageNumber(1);
  }

  const entries = result?.items ?? [];
  const first = (pageNumber - 1) * PAGE_SIZE + 1;
  const last = first + entries.length - 1;

  return (
    <>
      <header className="mb-6 flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 className="text-xl font-semibold tracking-tight">Audit log</h1>
          <p className="mt-1 max-w-[74ch] text-sm text-ink-600">
            Who changed access, and who signed in. Kept forever and never edited
            — the product’s evidence that a change happened, and who made it.
            Newest first.
          </p>
        </div>
        <button
          type="button"
          onClick={load}
          disabled={loading}
          className="h-9 shrink-0 rounded-md border border-line bg-white px-3.5 text-base font-medium
            transition-colors hover:border-line-strong disabled:text-ink-400"
        >
          {loading ? "Refreshing…" : "Refresh"}
        </button>
      </header>

      <div className="mb-4 flex flex-wrap items-center gap-3">
        <div
          className="flex w-fit items-center rounded-md border border-line p-0.5"
          role="group"
          aria-label="Filter by kind"
        >
          {FILTERS.map((filter) => (
            <button
              key={filter.value}
              type="button"
              aria-pressed={category === filter.value}
              onClick={() => choose(filter.value)}
              className={`h-7 rounded px-2.5 text-sm transition-colors ${
                category === filter.value
                  ? "bg-ink-900 font-medium text-white"
                  : "text-ink-600 hover:text-ink-900"
              }`}
            >
              {filter.label}
            </button>
          ))}
        </div>
        <span className="text-sm text-ink-500">
          “Refused” is failed sign-ins and blocked access changes — the rows
          worth a second look.
        </span>
      </div>

      {error && (
        <div
          role="alert"
          className="mb-4 rounded-md border border-critical-100 bg-critical-50 p-3 text-sm text-critical-700"
        >
          {error}
        </div>
      )}

      {loading && !result && (
        <div className="space-y-2" data-testid="audit-log-skeleton">
          <span className="skeleton block h-10 w-full" />
          <span className="skeleton block h-10 w-full" />
          <span className="skeleton block h-10 w-full" />
        </div>
      )}

      {result && entries.length === 0 && !error && (
        <div className="rounded-lg border border-line bg-white p-8 text-center">
          <p className="text-base font-medium">
            {category === "all" ? "Nothing recorded yet" : "Nothing here"}
          </p>
          <p className="mx-auto mt-1 max-w-[52ch] text-sm text-ink-600">
            {category === "all"
              ? "Sign-ins and changes to users and roles appear here as they happen."
              : "No entry of that kind. Try “All”."}
          </p>
        </div>
      )}

      {entries.length > 0 && (
        <>
          <div className="overflow-x-auto rounded-lg border border-line">
            <table className="w-full min-w-[760px] bg-white text-left text-base">
              <thead>
                <tr className="border-b border-line bg-canvas text-sm">
                  <th className="px-5 py-2.5 font-medium text-ink-600">When</th>
                  <th className="px-5 py-2.5 font-medium text-ink-600">Who</th>
                  <th className="px-5 py-2.5 font-medium text-ink-600">
                    What happened
                  </th>
                  <th className="px-5 py-2.5 font-medium text-ink-600">From</th>
                </tr>
              </thead>
              <tbody>
                {entries.map((entry) => (
                  <tr
                    key={entry.id}
                    className="border-b border-line align-top last:border-b-0"
                  >
                    <td
                      className="tnum px-5 py-3 text-ink-600"
                      title={entry.occurredAt}
                    >
                      {formatWhen(entry.occurredAt)}
                    </td>
                    <td className="px-5 py-3">
                      <WhoCell entry={entry} />
                    </td>
                    <td className="px-5 py-3">
                      <WhatCell entry={entry} />
                    </td>
                    <td className="px-5 py-3 font-mono text-sm text-ink-600">
                      {entry.ipAddress ?? "—"}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <div className="mt-3 flex items-center justify-between text-sm text-ink-600">
            <span>
              Showing{" "}
              <span className="tnum">
                {first}–{last}
              </span>{" "}
              of <span className="tnum">{result?.totalCount ?? 0}</span>
            </span>
            <span className="flex gap-2">
              <button
                type="button"
                onClick={() => setPageNumber((p) => p - 1)}
                disabled={pageNumber <= 1 || loading}
                className="h-8 rounded-md border border-line px-3 hover:border-line-strong disabled:text-ink-400"
              >
                Newer
              </button>
              <button
                type="button"
                onClick={() => setPageNumber((p) => p + 1)}
                disabled={pageNumber >= (result?.totalPages ?? 1) || loading}
                className="h-8 rounded-md border border-line px-3 hover:border-line-strong disabled:text-ink-400"
              >
                Older
              </button>
            </span>
          </div>
        </>
      )}
    </>
  );
}
