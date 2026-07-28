# ADR-0010 — Target .NET 10 LTS

- **Date:** 2026-07-27
- **Status:** Accepted
- **Amends:** the `.NET 8` choice made during scaffolding, and the `.NET 8 / .NET 9`
  wording in the v2.0 knowledge-base draft

## Context

The scaffold targets `net8.0`. The v2.0 architecture draft says ".NET 8 / .NET 9".
Both are out of date as of July 2026:

| Version | Track | Support ends | Status today |
|---|---|---|---|
| .NET 8 | LTS | **10 Nov 2026** | ~4 months left |
| .NET 9 | STS | **~May 2026** | **already out of support** |
| **.NET 10** | **LTS** | **14 Nov 2028** | current LTS, released 11 Nov 2025 |

This product is sold to enterprises — including **banks** — on multi-year installs
([ADR-0011](ADR-0011-commercial-model-v2.md)). Shipping on a runtime whose support
expires within months is indefensible in a security review, and every customer install
would need a runtime upgrade almost immediately after go-live.

## Decision

Target **.NET 10 (LTS)** — `net10.0` — across all projects.

## Why now specifically

**Nothing in the backend has ever been compiled or run.** There is no working build to
regress, no migration to re-generate, no customer install to upgrade. The cost of moving
is a `TargetFramework` change and a package-version bump. Every week of real
implementation raises that cost. **This is the cheapest possible moment.**

## Scope of the change

- `TargetFramework` → `net10.0` in all 6 project files (4 src + 2 test)
- Bump package majors to the matching line: EF Core, Npgsql provider,
  `Microsoft.AspNetCore.Authentication.JwtBearer`, `Microsoft.Extensions.Identity.Core`,
  `System.IdentityModel.Tokens.Jwt`, test SDK
- Docker base images (when created, [ADR-0004](ADR-0004-single-tenant-deployment.md))
  must pin the .NET 10 runtime
- `CLAUDE.md` stack section and `docs/architecture/overview.md` updated

## Consequences

- Support runway to **Nov 2028** — comfortably covers the first sales cycle and renewals.
- Package upgrades must be verified together with the **first successful build**, which
  has not happened yet — expect to fix compile errors from both the framework bump *and*
  the never-compiled code at the same time. Treat these as one task, not two.
- Developer machines and CI need the .NET 10 SDK installed.

## Revisit

When .NET 12 (the next LTS, expected late 2027) ships, plan an upgrade before .NET 10
support lapses in Nov 2028 — and note that on-premise customers upgrade on **their**
schedule, so the runbook from ADR-0004 must cover runtime upgrades, not just app upgrades.
