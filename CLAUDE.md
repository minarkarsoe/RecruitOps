# Project: [RecruitOps]

> Fill in the bracketed placeholders before committing this file. This is the
> "constitution" Claude Code reads at the start of every session — keep it
> accurate as the project evolves.

## Stack

- **Backend**: .NET 8 / ASP.NET Core Web API — Clean Architecture (Domain / Application / Infrastructure / Api)
- **Frontend**: Next.js (App Router) + React + TypeScript
- **Database**: PostgreSQL, EF Core migrations
- **Cache**: Redis [remove if unused]
- **Auth**: [JWT / ASP.NET Identity / external IdP — fill in]

## Repository Layout

```
backend/
  src/
    Domain/          # entities, value objects, domain logic — no outward dependencies
    Application/      # use cases, CQRS handlers, interfaces implemented by Infrastructure
    Infrastructure/    # EF Core, external services, implementations of Application interfaces
    Api/              # controllers/minimal API endpoints, DI wiring
  tests/
frontend/
  app/                # Next.js App Router routes
  components/
  lib/
  tests/
```

## Build & Test Commands

| Task | Command |
|---|---|
| Backend build | `dotnet build backend/src/Api` |
| Backend test | `dotnet test backend/tests` |
| Backend format | `dotnet format` |
| Frontend dev | `npm run dev --prefix frontend` |
| Frontend build | `npm run build --prefix frontend` |
| Frontend lint | `npm run lint --prefix frontend` |
| Frontend test | `npm run test --prefix frontend` |

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

## Guardrails for Claude

- Never edit `appsettings.Production.json`, `.env.production`, or anything under `infra/secrets/`. (Enforced by a PreToolUse hook — see `.claude/settings.json`.)
- Never apply an EF Core migration (`dotnet ef database update`) against anything but a local/dev connection string. Propose the migration and let a human apply it, unless explicitly told this is a disposable dev database.
- Never commit directly to `main`. Always work on a feature branch.
- Ask before adding a new NuGet or npm package — check whether an existing dependency already covers the need.
- Flag any change touching authentication, authorization, or payment logic for explicit human review before considering the task done — use the `security-reviewer` subagent on these.

## When Starting a Task

1. Read the relevant existing code before writing new code. Match existing patterns rather than inventing new ones.
2. For a change that touches both backend and frontend, agree on the API contract/shared types first so both sides build against the same shape.
3. Run tests and lint before declaring a task complete.
4. For non-trivial features or fixes, prefer the `/feature` or `/bugfix` commands in `.claude/commands/` over ad-hoc prompting — they encode the steps above.
