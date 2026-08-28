# Project: RecruitOps — In-house Recruitment Cloud System

> This is the "constitution" Claude Code reads at the start of every session — keep it
> accurate as the project evolves.

## What this product is

A multi-tenant SaaS for a **company's own talent acquisition department** — connecting
In-house Recruiters with Department Hiring Managers across requisition → sourcing →
interview → offer → analytics. A tenant is a **company**.

⚠️ It is **not** a recruitment-agency product. It pivoted on 2026-07-27
(`docs/decisions/ADR-0001-pivot-to-inhouse.md`); the agency-era code (`Client`,
`Contract`, `ClientTier`, `ClientFeedback`) has been **removed**. If you see those names
anywhere, they are stale — see `docs/status/MIGRATION-PLAN.md`.

⚠️ **Delivery is not a shared SaaS**: one instance + one database **per company**,
on our infrastructure or the customer's (`ADR-0004`). A tenant is a company, and tenant
query filters are a dormant safety net — the security-critical filter is **department
scoping** (`ADR-0003`), which is applied explicitly and can therefore be forgotten.

## Knowledge base — read first

`docs/` is the single source of truth. **Start every task at `docs/README.md`.**

| Need | Read |
|---|---|
| What we're building | `docs/product/overview.md`, `docs/product/modules/` (7 modules) |
| Current state of the code | `docs/status/FEATURE-STATUS.md` |
| What changed recently | `docs/status/CHANGELOG.md` |
| Why something is the way it is | `docs/decisions/` (ADRs) |
| Target entity model | `docs/architecture/data-model.md` |

**Keeping it current is part of the task, not an afterthought:**

1. Ship code → update `FEATURE-STATUS.md` in the same change.
2. Any meaningful change → add a `CHANGELOG.md` entry.
3. Hard-to-reverse decision → write an ADR in `docs/decisions/`.
4. Spec change → update the module doc **before** the code.

## UI work starts from `design/` — not from a description

`design/` is the source of truth for what the product looks like, the way `docs/` is for what it
does. **Before building or changing any screen, open the matching file in `design/internal/` or
`design/public/` and build against it.** Do not invent a layout when one is already drawn; if the
screen you need is not there, say so rather than improvising a twenty-sixth style.

- `design/internal/index.html` indexes all 26 screens.
- `design/internal/components.html` is the component sheet — buttons, inputs, pills, tables, empty
  and loading states.
- `design/internal/ds.js` holds the **V1.0 tokens** (ADR-0025) and `ds.css` the few things
  utilities cannot express (the approval-chain rail, Burmese line-height, focus ring, skeletons).

⚠️ **The design kit and the running apps do not yet share a token vocabulary, and this is not
cosmetic drift — the class names differ.** The kit uses `brand-*`, `ink-*`, `canvas`, `line`,
`positive/warn/critical`; `packages/ui/tailwind-preset.js` still ships "Clear Pipeline" —
`primary-*`, `surface-*`, `line-200`, `success/warning/danger/accent`. A screen copied from the
kit renders **unstyled**, not merely off-brand. **ADR-0025 step 3 is the prerequisite** for this
rule to mean anything; until it lands, translating by hand is how the two systems drift for good.

## Stack

- **Backend**: **.NET 10 (LTS)** / ASP.NET Core Web API — modular monolith, Clean Architecture (Domain / Application / Infrastructure / Api).
- **Frontend**: **two apps** — Vite + React SPA (`frontend/internal`, authenticated dashboards) and Next.js SSR (`frontend/public`, job pages + Open Graph previews). Shared design system via `packages/ui`, shared API types via `packages/types`. npm workspaces at the repo root.
- **Database**: PostgreSQL on AWS RDS, EF Core migrations, JSONB for customer-defined fields
- **Object storage**: Cloudflare R2 behind an S3-compatible abstraction (MinIO for on-prem)
- **Auth & Dynamic RBAC**: Self-issued JWT bearer. Token carries `sub`, `tenant_id`, role claim (`Admin` / `HrDirector` / `Recruiter` / `HiringManager` / `Approver` / Custom Role), `is_super_admin` flag, and granular `permissions` array (`permission:module:feature:action`). Fine-grained authorization enforced via `[HasPermission("permission:...")]` policy attribute on API endpoints (`/api/roles`, `/api/permissions`, `/api/users`). `SuperAdmin` and `Admin` roles bypass permission requirements dynamically.

## Repository Layout

Layout:

```
backend/
  src/
    Domain/          # entities (Role, Permission, RolePermission, UserRoleAssignment, etc.) — no outward dependencies
    Application/      # use cases, CQRS handlers, interfaces implemented by Infrastructure
    Infrastructure/    # EF Core, RbacSeedData, external services, implementations of Application interfaces
    Api/              # controllers (RolesController, PermissionsController, UsersController), DI wiring, HasPermission
  tests/
frontend/
  internal/           # Vite + React SPA — authenticated dashboards (Users, Role Builder, permission-aware UX)
  public/             # Next.js SSR — public job pages, application forms, OG metadata
packages/
  ui/                 # shared design-system components + Tailwind preset
  types/              # shared API types (mirror backend DTOs)
```

## Build & Test Commands

| Task | Command |
|---|---|
| Backend build | `dotnet build backend/src/Api` |
| Backend test | `dotnet test backend/RecruitOps.sln` (228 tests passing: 51 Domain + 177 Api) |
| Backend format | `dotnet format` |
| Backend build + test in Docker | `docker build --target test -t recruitops-test ./backend` |
| Whole stack | `docker compose up --build` |
| Internal SPA dev | `npm run dev:internal` (repo root) |
| Public app dev | `npm run dev:public` (repo root) |
| Frontend build | `npm run build` (repo root, all workspaces) |
| Frontend typecheck | `npm run typecheck` (repo root) |
| Frontend test | `npm run test` in `frontend/internal` (189 tests passing across 22 test files) |

> No local .NET SDK? `docker build --target test ./backend` compiles and runs the whole
> suite inside the SDK image. New EF migrations: see `docs/architecture/local-development.md`.

## Conventions

### C# / .NET
- Respect Clean Architecture boundaries: Domain has no outward dependencies; Application defines interfaces; Infrastructure implements them. Don't reach into Infrastructure from Domain.
- Async all the way — no `.Result` or `.Wait()` on tasks.
- Nullable reference types are enabled; don't suppress with `!` without a comment explaining why it's safe.
- Match existing patterns (CQRS/MediatR, repository, etc.) already used in the codebase rather than introducing a new one without discussion.

### TypeScript / React / Next.js
- Functional components and hooks only — no class components.
- Server Components by default; add `"use client"` only when interactivity requires it.
- Co-locate a component, its styles, and its test file.
- No `any` — use `unknown` and narrow, or define a proper type.

### Git
- Conventional Commits: `feat|fix|chore|refactor|test|docs(scope): description`
- One logical change per commit. Don't mix backend and frontend changes in one commit unless the change genuinely spans both (e.g. a shared API contract update).

## Code Intelligence & LSP Guidelines

Navigate code by **symbol**, not by string match. When an LSP is available, prefer it over
text search for anything that has a definition.

- **TypeScript / JS / Next.js / Nest.js / Node.js** (`frontend/internal`, `frontend/public`,
  `packages/ui`, `packages/types`): use LSP `goToDefinition`, `findReferences`, and `hover`
  to trace types, DTOs, controllers, and React components across files. Especially important
  for shared types in `packages/types`, which mirror backend DTOs — `findReferences` shows
  every consumer of a contract before you change its shape.
- **C# / .NET** (`backend/src/**`): use LSP to inspect `.cs` interfaces, dependency-injection
  bindings, and class definitions. Use it to follow an Application interface to its
  Infrastructure implementation rather than guessing from file names, and to find every
  endpoint carrying a given `[HasPermission(...)]` policy.
- **Python:** use the Pyright LSP to check type hints and module definitions. (No Python in
  this repo today — this applies to scripts or tooling added later.)
- Reserve `grep` and `glob` strictly for text searches or non-code files (`.json`, `.csproj`,
  `.env`, `.md`, migrations SQL). Symbol lookups go through the LSP, on both sides, with no
  warm-up step — `lsp_find_references` is complete from a cold call (measured 2026-08-15; see
  below). `grep` remains the right tool for finding *strings* that are not symbols: permission
  codes, route templates, config keys.

### Setup

The `lsp` MCP server is declared in `.mcp.json`; the language servers it drives are
declared in `.lsp-mcp.json`. It exposes 29 `lsp_*` tools (`lsp_goto_definition`,
`lsp_find_references`, `lsp_hover`, `lsp_call_hierarchy`, `lsp_rename`, …).

| Piece | Package | Install |
|---|---|---|
| MCP bridge | `lsp-mcp-server` (root devDependency) | `npm install` |
| TS/JS server | `typescript-language-server` (root devDependency) | `npm install` |
| C# server | `csharp-ls` (global .NET tool) | `dotnet tool install --global csharp-ls` |

`npm install` covers the two Node pieces. **`csharp-ls` is the one manual step** — it is a
global tool, not a repo dependency, so each developer installs it once. Without it the
TypeScript side still works and C# requests fail with a "server not found" error.

Workspace roots resolve automatically: `frontend/internal` (and the other npm workspaces)
via `tsconfig.json`/`package.json`, and `backend/` via `RecruitOps.sln`.

### TypeScript references are complete cold — no warm-up needed

> **This section previously said the opposite.** It documented a `grep` → `lsp_index_files` →
> `lsp_find_references` warm-up as mandatory, on a measurement of "cold → 1 reference, 1 file".
> Re-measured **2026-08-15**, that is no longer true, and following it wasted work *and* inverted
> a diagnostic. Left here as a record: an undated measurement in this file outlived its truth,
> and nothing would have caught it if it had not been tested directly.

Measured **2026-08-15** on `hasPermission` in `frontend/internal/src/lib/auth.ts:169`, with **no**
preceding `lsp_index_files` and `lsp_server_status` returning `[]` beforehand:

- cold → **50 references across 15 files**, including `auth.test.ts`

So call `lsp_find_references` directly. **A one-file answer now means the symbol really is
unused** — treat it as a finding, not as a tooling artifact. (That distinction matters here: the
orphaned `frontend/internal/src/features/requisitions/*` tree has zero importers repo-wide while
carrying six test files, and the old heuristic would have told you to dismiss that as "not
indexed yet".)

`lsp_index_files` is still needed, for a different job: it is the prerequisite for
`lsp_workspace_diagnostics` (which only sees opened files) and for `lsp_related_files imported_by`.
It is not a prerequisite for reference search.

**C# is complete from the first call too** — `csharp-ls` loads the whole solution at startup.
Verified **2026-08-15**: `HasPermissionAttribute` returns **23 references across 7 files**, tests
included. The trade-off is a slow first request while Roslyn loads the solution; `requestTimeout`
in `.lsp-mcp.json` is raised to 120 s for that reason.

That same query is the clearest illustration of why symbol lookup beats text search here: it
resolved `[HasPermission("permission:users:users:read")]` attribute shorthand **and**
`new HasPermissionAttribute(...)` in the tests as one symbol. `grep` needs two patterns for that
and still cannot tell you they are the same thing.

### ⚠️ The one real LSP trap: query shared types from the *consumer* side

**This is now the only LSP caveat in this repo** — the TypeScript warm-up above was retired on
2026-08-15, so do not read this as one caution among several. It is the single case where the LSP
will hand you a confident wrong answer, and it was **re-verified on 2026-08-15**: `LoginResponse`
queried from `packages/types/src/index.ts:33` still returns **exactly 1**.

Each npm workspace gets its **own** `tsserver` process (servers start lazily, one per workspace
root touched — `csharp-ls` on `backend/`, plus one each for `frontend/internal`, `frontend/public`,
`packages/ui`, `packages/types`). A server only sees files under its own root, and that has
one sharp consequence for `packages/types`:

| Query from | Result for `LoginResponse` |
|---|---|
| `packages/types/src/index.ts` (where it's **defined**) | **1** — the declaration only. Every consumer is invisible. |
| `frontend/internal/src/lib/auth.ts` (a **consumer**) | **5** — all local usages *plus* the declaration in `packages/types`. |

References resolve *outward* through the `@recruitops/types` import, never inward. So the
CLAUDE.md rule "find every consumer of a contract before you change its shape" **must not be
run from `packages/types`** — that returns a confident, empty-looking answer.

`@recruitops/types` is consumed by three workspaces — `frontend/internal`, `frontend/public`
and `packages/ui`. A complete answer means repeating the query once per consuming workspace,
or falling back to `grep` for the initial sweep and using the LSP to confirm each hit.

Two smaller notes from the same check: `packages/ui` and `packages/types` have **no
`tsconfig.json`** (they resolve via `package.json` instead) and still answer correctly; and
Next.js App Router paths containing brackets — `app/jobs/[token]/page.tsx` — work fine.

## Guardrails for Claude

- Never edit `appsettings.Production.json`, `.env.production`, or anything under `infra/secrets/`. (Enforced by a PreToolUse hook — see `.claude/settings.json`.)
- Never apply an EF Core migration (`dotnet ef database update`) against anything but a local/dev connection string. Propose the migration and let a human apply it, unless explicitly told this is a disposable dev database.
- Never commit directly to `main`. Always work on a feature branch.
- Ask before adding a new NuGet or npm package — check whether an existing dependency already covers the need.
- Flag any change touching authentication, authorization, or payment logic for explicit human review before considering the task done — use the `security-reviewer` subagent on these.

## When Starting a Task

0. **Read `docs/status/NEXT-SESSION.md` first**, then `docs/status/FEATURE-STATUS.md`. The
   first says where the product is, what to pick up, and which traps have already bitten us;
   the second is the per-module state. Update both when you're done.
   > Sessions are deliberately **one feature each** — conversation history is re-sent on
   > every turn, so a session that outlives its feature costs a lot and adds nothing. These
   > two docs exist so a fresh session starts cheaply.
1. Read the relevant existing code before writing new code. Match existing patterns rather than inventing new ones.
2. For a change that touches both backend and frontend, agree on the API contract/shared types first so both sides build against the same shape.
3. Run tests and lint before declaring a task complete.
4. For non-trivial features or fixes, prefer the `/feature` or `/bugfix` commands in `.claude/commands/` over ad-hoc prompting — they encode the steps above.
