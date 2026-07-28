# Auth & Multi-Tenancy

**Status:** ✅ Built (backend). See [ADR-0002](../decisions/ADR-0002-jwt-auth.md).

## Model

Self-issued **JWT bearer** (HS256, 8-hour lifetime). Each token carries:

| Claim | Purpose |
|---|---|
| `sub` | User id |
| `tenant_id` | **Company** the user belongs to — drives data isolation |
| `role` (`ClaimTypes.Role`) | RBAC |
| `email`, `name` | Display |

Signing key comes from `Jwt:Key` (user-secrets / env). **Never committed.**
`JwtTokenService` throws if it's missing rather than falling back to a default.

## Tenant isolation

`CurrentTenant` (`Api/Auth/CurrentTenant.cs`) reads `tenant_id` from the authenticated
principal. `AppDbContext` applies a **global query filter** on every `ITenantScoped`
entity, so tenant-owned data is filtered automatically on every query — isolation
doesn't depend on developers remembering a `WHERE` clause.

Proven by `RecruitOps.Api.Tests/ClientsIsolationTests.cs`: tenant A sees only its own
rows, tenant B cannot see A's, unauthenticated is 401, wrong role is 403.

**Pre-auth exception:** `AuthService` uses `IgnoreQueryFilters()` for the login lookup,
because there is no tenant context before authentication.

## Authorization

Secure-by-default: `FallbackPolicy = RequireAuthenticatedUser` means **every endpoint
requires auth unless it explicitly opts out** with `[AllowAnonymous]`.

Anonymous by design: `POST /api/auth/login`, and the public job page (Module 2).

## ⚠️ Roles must be revised for in-house

The implemented role set is the **agency** one and is now wrong:

| Current (agency) | Verdict |
|---|---|
| `Admin` | ✅ Keep |
| `SeniorRecruiter` / `JuniorRecruiter` | 🔄 Collapse to `Recruiter` (seniority isn't a permission boundary here) |
| `Client` | ❌ Remove — no external clients |

**Proposed in-house roles:**

| Role | Who | Can |
|---|---|---|
| `Admin` | System administrator | Settings, RBAC, integrations (Module 7) |
| `HrDirector` | Management / HR Director | All reports, budget & plan approval (Modules 5–6) |
| `Recruiter` | In-house recruiter | Full pipeline: postings, candidates, interviews, offers |
| `HiringManager` | Department manager | Raise requisitions; see **their own department's** candidates; interview + score |
| `Approver` | Dept Head / Finance / HR in an approval chain | Approve/reject requisitions and plans |

Policy rename: `AgencyStaff` → **`RecruitmentStaff`** (`Admin`, `HrDirector`, `Recruiter`).

## Department scoping — decided

`HiringManager` sees only **their own department's** data. Implemented as an **explicit
authorization predicate** in the application layer, *not* a global query filter — see
[ADR-0003](../decisions/ADR-0003-department-scoping.md) for the reasoning and edge cases.

Because it isn't automatic, **every** endpoint returning `Requisition`, `JobPosting`,
`Application`, `Candidate`, `Interview` or `Scorecard` needs a test proving a manager
from department A cannot read department B's row. Model access as a **set**
(`UserDepartment` many-to-many) — managers can own more than one department.

## ⚠️ Tenant isolation is now a safety net, not the boundary

Per [ADR-0004](../decisions/ADR-0004-single-tenant-deployment.md) each company gets its
**own deployment and database**, so isolation is physical. The tenant plumbing stays as
defence against misconfiguration and to keep a shared-hosting tier possible — but the
security-critical filter is now **department scoping** above. Do not let the presence of
tenant filters create false confidence.

## Brute-force protection ([ADR-0016](../decisions/ADR-0016-login-brute-force-protection.md))

`POST /api/auth/login` is limited on two independent axes, because one source making many
guesses and many sources guessing one account look nothing alike from the server:

- **Per client IP** — built-in fixed-window limiter, 60/60s by default, IPv6 grouped by /64.
- **Per account** — `ILoginThrottle`, 5 failures → 15-minute lockout, counted for *every*
  email whether or not it exists (otherwise the 429 becomes an existence oracle).

Supporting the same no-enumeration goal, `AuthService` verifies the supplied password
against a dummy hash when the account doesn't exist, so response time doesn't reveal which
emails are registered.

## Known gaps

- `LoginThrottle` state is in-process: reset by a restart, not shared across replicas.
  Adequate for one instance per company (ADR-0004); move to Redis/DB before scaling out.
- `ReverseProxy:TrustForwardedHeaders` is off by default because the dev compose file
  publishes the API port. Until a deployment turns it on, the per-IP limiter treats all
  proxied traffic as a single caller.
- No refresh token; the 8-hour access token is the only credential.
- Login matches on **email alone**, so email must be unique across all companies.
  Same email at two companies needs a tenant selector (subdomain/slug).
