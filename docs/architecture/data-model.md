# Target Data Model — In-house

Target state for the in-house product. Legend: ✅ exists · 🚧 stub exists · ⬜ to build · ❌ to delete.

## Core graph

```
Company (tenant)
 ├─ Department ──────────────┐
 ├─ User (role)              │
 ├─ JdTemplate               │
 ├─ RecruitmentPlan          │
 │   ├─ HeadcountPlan ───────┤ (per department)
 │   ├─ BudgetLine
 │   └─ BudgetSpend
 └─ Requisition ─────────────┘  raised by Hiring Manager, owned by a Department
     ├─ RequisitionApproval (ordered steps)
     └─ JobPosting                 created once approved
         ├─ JobChannelPost         career page / shareable link / social
         ├─ ApplicationForm ─ ApplicationFormField
         └─ JobApplication ─────── Candidate
             ├─ ApplicationStageHistory   (append-only → analytics)
             ├─ CandidateMatch            (Smart Match score vs. this job)
             ├─ Interview ─ InterviewParticipant
             │   └─ Scorecard ─ ScorecardResponse
             ├─ Note ─ NoteMention
             └─ Offer
                 ├─ OfferSignature
                 └─ PreboardingRequest ─ PreboardingDocument

Candidate
 └─ CandidateDocument (CV file + OCR text + parse status)

Cross-cutting: ApprovalChain/Step · IntegrationConfig · RetentionPolicy · AuditLog · NotificationLog
```

## Entity inventory

### Keep / rename

| Entity | State | Notes |
|---|---|---|
| `Tenant` → **`Company`** | ✅→⬜ rename | Tenant is now a *company*, not an agency. |
| `User` | ✅ | Fields built (email, hash, role, active). **Role enum must change** — see auth doc. |
| `Candidate` | ✅ | Name, email, phone (both normalised for dedup), source, merge ref. Skills/experience arrive with OCR (2.3). |
| `Application` → **`JobApplication`** | ✅ renamed | Renamed to avoid colliding with the `RecruitOps.Application` namespace (caught by the first compile). Needs stage + **`ApplicationStageHistory`**. `ClientFeedback` dropped. |
| `Job` → **`JobPosting`** | ✅ | Derives from an approved `Requisition` — **required, unique**. Title/description/location/salary/`ShowSalary`/JSONB form schema. |
| `JobChannelPost` | 🚧 stub | Repurpose: career page + standalone shareable job page + social. Still unused — the shareable link lives on `PortalLink`. |
| `PortalLink` | ✅ | Public applicant job-page token (256-bit CSPRNG, globally unique), expiry, revoke, view/apply counters. *Not* client CV review. |
| `JobApplication` | ✅ | Stage, source, applied-at, cover note, JSONB custom-field answers. |
| `ApplicationStageHistory` | ✅ | Append-only. Written from the application's arrival, because Module 5 cannot reconstruct it later. |

### Delete (agency-only) ❌

| Entity / type | Why |
|---|---|
| `Client` | External hiring company — doesn't exist in-house. Replaced by `Department`. |
| `Contract` | Agency↔client commercial contract. Replaced by `RecruitmentPlan`/`BudgetLine`. |
| `ClientTier` (Gold/Silver/Bronze) | Commercial client rating — meaningless internally. |
| `ContractStatus` | Tied to `Contract`. |
| `ClientFeedback` (Accepted/NeedMoreInfo/Rejected) | Client CV-review verdict. Replaced by **scorecards**. |
| `ContractStatusCalculator` | Domain service for contract expiry. |

### New ⬜

`Department` · `Requisition` · `RequisitionApproval` · `ApprovalChain` · `ApprovalChainStep` ·
`JdTemplate` · `ApplicationForm` · `ApplicationFormField` · `CandidateDocument` ·
`CandidateMatch` · `ApplicationStageHistory` · `Interview` · `InterviewParticipant` ·
`Scorecard` · `ScorecardCriterion` · `ScorecardResponse` · `Note` · `NoteMention` ·
`OfferTemplate` · `Offer` · `OfferSignature` · `PreboardingRequest` · `PreboardingDocument` ·
`RecruitmentPlan` · `HeadcountPlan` · `BudgetLine` · `BudgetSpend` · `PlanApproval` ·
`IntegrationConfig` · `RetentionPolicy` · `AuditLog` · `NotificationLog`

## Revised pipeline vocabulary

The agency vocabulary had a client hand-off step that no longer exists.

| Old (agency) | New (in-house) |
|---|---|
| `Sourced` | `Sourced` — added by recruiter, hasn't applied |
| — | `Applied` — came in via the application form |
| — | `Screening` |
| `Shortlisted` | `Shortlisted` |
| `SentToClient` ❌ | *removed* |
| `Interview` | `Interview` |
| — | `Offer` |
| `Placed` | `Hired` (rename) |
| `Rejected` | `Rejected` |

⚠️ This changes `Domain/Enums/PipelineStatus.cs`, `frontend/lib/types.ts`,
`StatusPill`, and the design system's fixed vocabulary (§5.2). All four must move together.

## Design-system impact

`/RecruitOps_Design_System.md` is agency-flavoured and needs revision:

| Element | Verdict |
|---|---|
| Color tokens, typography, spacing, radius, elevation | ✅ Keep — product-neutral |
| Status Pill (§5.2) | 🚧 Keep component, **change vocabulary** (above) |
| Tier Badge (§5.3) | ❌ Remove — no client tiers |
| Client Feedback Bar (§6.2) | ❌ Remove — replaced by scorecards |
| Portal Candidate Card (§6.3) | ❌ Remove as client-review card; ⬜ new **public job page + application form** design needed |
| Expiry Attention Card (§6.4) | 🔄 Repurpose — from contract expiry to **requisition awaiting approval** / budget alerts |
| Pipeline Stage Rail (§6.1) | ✅ Keep — still the app's identity, with the new stage list |
| CRM Client List (§7) | ❌ Remove; ⬜ replaced by **Requisition list** and **Department view** |
