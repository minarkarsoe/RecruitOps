# Product

<!-- impeccable:product-schema 1 -->

## Platform

web

## Stack

The application is an existing codebase and answers its own stack: .NET 10 / ASP.NET Core
Web API (modular monolith, Clean Architecture), PostgreSQL + EF Core, and two frontends —
Vite + React SPA (`frontend/internal`) and Next.js SSR (`frontend/public`), with npm
workspaces sharing `packages/ui` and `packages/types`. See `CLAUDE.md`.

**The marketing surface is a deliberate exception, pinned by the user:** a single-file
semantic HTML page with the Tailwind CSS CDN and Lucide/Phosphor icons, opening directly in
a browser with no build step. It is written to `marketing/landing.html` and is not a route
in either app. If it is later promoted into `frontend/public` as a real Next.js route, that
is a separate decision.

## Users

Two primary users, in tension with each other — resolving that tension *is* the product:

- **In-house Recruiters** — run high candidate volume for their own employer. Their pain is
  manual data entry and chasing hiring managers for decisions.
- **Department Hiring Managers** — not HR staff, and not daily users. They raise headcount
  requests, sit on interview panels, and score candidates. Today they do this over email,
  chat and spreadsheets. They are **first-class citizens of the system**, not observers.

Secondary, and the economic buyer: **HR Directors, CHROs and Management** — they control
recruitment budget and approve headcount, and they need the audit trail and the reporting.

Buying context: domestic Myanmar enterprises above ~1,000 staff — banks, FMCG/retail
chains, telecoms, factories, MFIs. Sold **partner-led** through local sales partners and HR
consultants (ADR-0011).

## Product Purpose

Make the whole hiring lifecycle — raising a vacancy through to onboarding — fast, traceable
and secure **inside one company**. The central problem is not sourcing; it is the
back-and-forth between recruiters and hiring managers, which currently has no system of
record.

Success means a requisition's entire life is answerable from the system: who asked, who
approved, on what version, who interviewed, what they scored, and when.

## Positioning

Explicitly **not** an external recruitment-agency product. It pivoted away from that on
2026-07-27 (ADR-0001); the agency-era concepts (`Client`, `Contract`, `ClientTier`,
`ClientFeedback`) were deleted. A tenant is a **company**, not a client.

Four mechanisms a neighbouring product could not truthfully copy:

1. **Physical single-tenant isolation.** One application instance + one database per
   company, on our infrastructure or the customer's, reached by subdomain (ADR-0004).
   Every tier gets a dedicated database — Mid-Tier included. Isolation is physical, not a
   `tenant_id` column. The tenant query filters still exist but are a **dormant safety
   net**; the security-critical filter is department scoping (ADR-0003).
2. **The approval chain is a truthful record, not a workflow toy.** The chain is
   snapshotted onto the requisition at submit, so later edits to the template cannot
   rewrite decisions already made. A rejected requisition can be revised and resubmitted,
   opening a **new round beside the old one** rather than over it (ADR-0023). A senior
   approver can close every step at or below their own position, and the record names who
   actually acted, not who the template expected (ADR-0024). Cancelling leaves the chain
   half-decided on purpose.
3. **Blind panel evaluation, enforced server-side.** A panel member sees only their own
   scorecard until theirs is submitted; submitting is irreversible; criteria are
   snapshotted onto each response so template edits cannot retroactively change what
   someone was asked (ADR-0017 §§2–3).
4. **Myanmar-language handling as infrastructure, not a locale file.** Zawgyi and Unicode
   occupy the same code block, so Zawgyi text looks valid and silently fails to match. Text
   is normalized to Unicode **at ingest**, storing both the normalized text and the
   original plus `detected_encoding` (ADR-0009). Burmese has no reliable word boundaries,
   so CV search cannot use PostgreSQL's default full-text tokenisation and must use trigram
   indexing or segmentation.

## Operating Context

The core loop:

```
Hiring Manager raises Requisition
    → approval chain (e.g. Dept Head → Finance → HR), sequential
Approved → Job Posting → public job page (shareable token)
    → candidates apply, or recruiter bulk-uploads CVs
    → pipeline: screening → shortlist
    → interview scheduled → blind scorecards → threaded debrief notes with @mentions
    → offer → hire → analytics
```

Access control is dynamic and permission-driven: JWT carries `sub`, `tenant_id`, a role
claim, an `is_super_admin` flag and a granular `permissions` array. Endpoints are gated by
a `[HasPermission("permission:module:feature:action")]` policy attribute. **37 distinct
permission codes across 10 modules** are seeded (`RbacSeedData.cs`), and custom roles are
composed from them in a Role Builder UI. `SuperAdmin` and `Admin` bypass dynamically.

Department scoping is applied as an **explicit predicate**, not an EF query filter — which
is precisely why it can be forgotten, and why ADR-0003 calls it the security-critical one.
Out-of-scope rows return **404, not 403**, so existence is not leaked.

## Capabilities and Constraints

Built and tested (507/507 backend, 318/318 frontend as of 2026-08-13):

- Module 1 Requisition & Approval — complete, drivable end to end from the browser
- Module 2 ATS & Sourcing — partial: posting, public job page, custom application forms,
  pipeline, CV ingestion, full-text search. OCR and Smart Match not started.
- Module 3 Interview & Assessment — scorecards, blind panel view, notes and @mentions
  built. Calendar sync and automated email invitations **do not exist** — no email sender
  or calendar client is in the codebase.
- Module 7 — Dynamic RBAC, Role Builder, User Directory, permission-aware UI

Not started: Module 4 (Offer & Pre-boarding), Module 5 (Reporting & Analytics), Module 6
(Planning & Budgeting), Module 8 (multi-channel Viber/Telegram/Facebook sourcing).

Known open constraints that marketing copy must not paper over:

- Zawgyi→Unicode normalization is **specified but not implemented**, and there is no
  official .NET client for Google's `myanmar-tools`.
- Burmese OCR accuracy is **unverified**; ADR-0009 defines a test and explicitly says not
  to advertise accuracy until measured.
- Feature gating by tier does not exist yet, and is required before the first Enterprise
  deal.
- No `/api/version`, server sizing guide, or upgrade/backup runbook yet.

Customization is sold as add-ons and must land in one of four buckets — configuration,
feature flag, extension point, or core change. **No per-customer code forks** (ADR-0007).

## Brand Commitments

- **Name:** RecruitOps. Confirmed by `CLAUDE.md` and the repository
  (`github.com/minarkarsoe/RecruitOps`). Docs also refer to it descriptively as the
  "In-house Recruitment Cloud System"; that is a description, not a second name.
- **No brand assets exist.** No logo, wordmark, SVG, or favicon anywhere in the repository
  — verified 2026-08-17. The visual identity is being originated with this surface; an
  inline SVG wordmark and glyph are authored here at the user's direction, and are a
  starting point for a designer, not an approved identity.
- **Audience and language:** the page is written in **English for Myanmar enterprise
  buyers**. Bilingual EN / Myanmar Unicode capability is a differentiator to demonstrate,
  not the language of the marketing page itself.

## Evidence on Hand

Real, and usable:

- The full `docs/` knowledge base — 7 module specs, 24 ADRs, feature status, changelog.
- Real permission codes, entity names, status vocabularies, and API routes.
- Real architectural decisions with dated rationale.

**Absences that future work must not fabricate** — confirmed with the user 2026-08-17:

- **No customers, pilots, references, testimonials, case studies or press.** The target
  segments (banking, FMCG/retail, telecoms, conglomerates, tech) are *targets*, and must be
  framed as "built for", never as existing clients or logos.
- **No compliance certification.** PDPA, GDPR and SOC are **not** certified or audited.
  Right-to-be-forgotten and retention are described as **capabilities**, never as badges
  implying certification.
- **No pricing shown.** ADR-0011's MMK figures exist but the user has chosen to keep them
  off the page; commercial tiers are compared on features and quoted through a partner.
- **No benchmark or performance numbers** — no time-to-hire improvement, no adoption stats.

One claim the user has confirmed as a real commitment and authorised for the page:

- **99.9% uptime SLA** on the Enterprise tier. This is not currently recorded in any ADR;
  it is a commitment the user is making. It should be written into the commercial terms and
  an ADR, because today the page will assert something the docs do not yet support.

## Product Principles

1. **The record is the product.** Nothing may fabricate a decision nobody made — not a
   rewritten approval chain, not a carried-forward approval, not a cancelled step
   backfilled. Truthfulness of the audit trail outranks tidiness of the UI.
2. **Hiring managers are users, not spectators.** Any capability that assumes an HR
   specialist at the keyboard has failed the person the product exists to reach.
3. **Isolation is physical, and department scoping is the sharp edge.** The safety net is
   dormant by design; the filter that actually protects data is the one applied by hand.
4. **Burmese is infrastructure, not a translation layer.** Encoding, search tokenisation
   and rendering are engineering problems that must be solved at the boundary.
5. **One generic product; customization is configuration.** A promise that fits none of
   ADR-0007's four buckets is repriced or refused, never forked.

## Accessibility & Inclusion

No formal standard has been established by the user. Two product-specific needs are
nonetheless real and confirmed by the docs:

- **Myanmar Unicode rendering** requires the `Noto Sans Myanmar` fallback and a ~1.7
  line-height; the design system already mandates this. Zawgyi-encoded text renders as
  garbage and must be normalized rather than displayed.
- **Hiring managers are occasional, non-expert users** on unknown hardware, which makes
  legible contrast, real target sizes and unambiguous labelling a product requirement
  rather than a nicety.
