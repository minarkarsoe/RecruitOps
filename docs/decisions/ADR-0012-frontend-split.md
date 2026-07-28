# ADR-0012 — Two frontends: internal SPA (Vite) + public SSR (Next.js)

- **Date:** 2026-07-27
- **Status:** Accepted
- **Amends:** the single Next.js App Router frontend created during scaffolding

## Context

The scaffold has one Next.js App Router app serving everything. The v2.0 architecture
splits the frontend by audience:

- **Internal** (HR & Admin dashboards) — **Vite + React SPA**
- **Public** (shareable job pages) — **Next.js SSR**, specifically so social shares render
  an **Open Graph thumbnail**. Job links are pushed to Facebook, Telegram and Viber
  ([ADR-0014](ADR-0014-multi-channel-sourcing.md)), where an unfurled preview card
  materially affects click-through.

## Decision

Split into two applications, as the v2.0 design specifies.

```
frontend/
  internal/    Vite + React SPA   — authenticated dashboards
  public/      Next.js (SSR)      — job pages, application forms, OG metadata
packages/
  ui/          shared design-system components + Tailwind preset
  types/       shared API types (mirrors backend DTOs)
```

> **Note:** a single Next.js app *could* serve both — App Router does SSR for public
> routes and client components for the dashboard. The split was chosen deliberately for a
> cleaner separation and a lighter internal bundle, accepting the duplication cost below.

## Consequences

### Must be planned for, or this split becomes expensive

1. **Shared UI must be a real shared package, not copy-paste.** The design system
   (`RecruitOps_Design_System.md`) defines one visual language; two independent copies of
   `StatusPill`, the Tailwind token config and the fonts will drift within weeks. Use a
   workspace (npm/pnpm workspaces) with `packages/ui` consumed by both apps.
2. **The Tailwind theme (design tokens) lives in the shared package** and is imported as a
   preset by both apps. Tokens must never be redefined per app.
3. **Two auth surfaces.** The internal SPA holds the JWT client-side; the public app is
   mostly anonymous but handles application-form submission. Do not share a token strategy
   between them by accident — the public app must never hold an agency-staff token.
4. **Two build and deploy artifacts**, and both must be containerised for the per-company
   install ([ADR-0004](ADR-0004-single-tenant-deployment.md)) — plus reverse-proxy routing
   that maps the public job-page paths to the Next.js app and everything else to the SPA.
5. **Shared API types** should also live in the workspace, so a backend DTO change breaks
   both apps at compile time rather than at runtime.

### Migration from the current scaffold

The existing `frontend/` (Next.js App Router with `app/dashboard`, `app/clients`,
`app/jobs`, `app/candidates`, `app/portal/[token]`) must be reorganised:

| Current | Goes to |
|---|---|
| `app/dashboard`, `app/jobs`, `app/candidates` | `frontend/internal` (Vite SPA routes) |
| `app/portal/[token]` | `frontend/public` (becomes the public job page) |
| `app/clients` | ❌ deleted ([MIGRATION-PLAN](../status/MIGRATION-PLAN.md)) |
| `components/ui/StatusPill`, Tailwind config | `packages/ui` |
| `lib/types.ts` | `packages/types` |
| `lib/api.ts` | split — SPA client (browser, bearer token) and public client |

The server-aware `lib/api.ts` fix (absolute URL server-side) still matters, but **only for
the Next.js public app** — the Vite SPA runs entirely in the browser and always uses a
relative or configured base URL.

### Open questions

- Does the internal SPA need any SSR at all (e.g. deep-link previews in Viber for internal
  users)? If yes, revisit — that's an argument for the single-app approach.
- Reverse-proxy path split: which paths belong to the public app? Suggest reserving a
  prefix (e.g. `/jobs/*`, `/apply/*`) rather than mixing at the root.
