# ADR-0020 — URL Route Parameter Obfuscation using Sqids

**Status:** Accepted · **Date:** 2026-07-30  
**Relates to:** [ADR-0004](ADR-0004-single-tenant-deployment.md), [ADR-0012](ADR-0012-frontend-split.md)  
**Initiated by:** Architecture & Security Review of Route Identifiers

---

## 1. Context

Internal routes in `frontend/internal` (`/requisitions/:id`, `/jobpostings/:id`, `/interviews/:id`) pass raw 128-bit GUID strings in the URL (e.g. `/requisitions/e4d9b23a-5c1a-4d92-8f92-0b56540e6e6b`).

While raw GUIDs prevent sequential enumeration (IDOR attacks), they present two issues:
1. **Unfriendly UX & Cluttered URLs:** 36-character hyphenated GUIDs make URLs visually noisy and difficult to communicate orally or share internally.
2. **Internal Database Key Exposure:** Exposing raw primary keys directly in client-side URLs tightly couples API routes to internal storage identifiers.

Alternatives considered:
- **Business Reference Slugs (e.g. `/requisitions/REQ-2026-0042` or `/jobpostings/senior-backend-dev`):** Human-readable, but introduces severe operational complexity: requires DB unique indexes, custom slug generator logic, collision resolution on title renames, and complex routing logic.
- **Raw Sequential Integers (1, 2, 3...):** Disqualified due to severe IDOR enumeration risks and Data Lake / Data Warehouse merge collision issues.
- **Sqids (Official successor to Hashids):** Converts GUIDs / IDs to short, elegant, non-sequential, URL-safe alphanumeric strings (e.g. `Xk9zL8mP`).

---

## 2. Decision

We adopt **Sqids (C# `SqidsNet` / TypeScript `sqids`)** for encoding internal entity identifiers in client-facing API routes and UI URLs:

1. **Stateless & Deterministic:** Sqids encodes/decodes GUIDs / 128-bit byte arrays into compact, 8–10 character strings deterministically without requiring new DB columns, slug tables, or state management.
2. **Obfuscation & Security:** Completely hides raw database primary keys from end-user URLs and prevents URL tampering while maintaining high-performance decoding in ASP.NET Core API ModelBinders.
3. **Decoupled Business Naming:** Avoids the heavy database constraints, slug collision handling, and renaming complexities associated with business title slugs.

---

## 3. Implementation Rules

- **API Model Binding:** ASP.NET Core controllers accept `[FromRoute] Sqid<Guid> id` or custom `SqidBinder`, decoding to `Guid` in Application handlers seamlessly.
- **DTO Serialization:** Response DTOs sent to `frontend/internal` serialize `Id` as a Sqid-encoded string.
- **Public App Exception:** `frontend/public` continues using 256-bit CSPRNG tokens (`PortalLink.Token`) for unauthenticated public applicant job pages (`/jobs/[token]`), maintaining maximal cryptographic isolation.

---

## 4. Consequences

- **Cleaner, Professional URLs:** UI routes shrink from 36 characters to ~8 characters (e.g. `/requisitions/7bXk9zL`).
- **Zero Schema Overhead:** Database schema (`BaseEntity.Id` as `Guid`) remains untouched, preserving EF Core query efficiency and Data Lake compatibility.
- **Reversible & Fast:** In-memory encoding/decoding overhead is near zero (<1 microsecond).
