# Competitive UX Research & Design Recommendations for RecruitOps
> **Provenance.** Rescued from `design-prototypes-7/` on 2026-08-21 when the seven exploratory
> `design-prototypes*` folders were deleted in favour of `design/`. The prototypes were
> superseded; this research was not, and it has no copy anywhere else in `docs/`. Its
> recommendations are **not** decided — treat them as input, not as spec.

> Sources reviewed 2026-08: greenhouse.com, ashbyhq.com, manatal.com (+ enterprise-suite approval patterns from Workday/SAP-class systems). Paired against RecruitOps BRD/FRD v1.0.0 and FEATURE-STATUS 2026-08-18. Companion to `design-prototypes-6/` (The Front Desk, System-Aligned Edition).

## 1. What the leaders actually do

### Greenhouse — "structured hiring" as the product thesis
| Pattern | What it looks like | RecruitOps mapping |
|---|---|---|
| Interview kits | Every job ships with question kits per interview stage; scorecards are generated FROM the kit, not authored ad hoc | M3 scorecard templates → evolve into **per-stage interview kits** (questions + rubric attached to the round, not just criteria list) |
| Debrief mode | After all submissions, a dedicated decision screen ranks candidates by aggregated scorecards | Blind-unblind gate exists; add a **debrief decision view** (side-by-side scorecard comparison) |
| Candidate portal (MyGreenhouse) | Real-time application status, job alerts, "where did my application go" | Public apply currently ends at reference number → add **status timeline for candidates** |
| Trust layer (Real Talent) | Fraud detection, identity verification flags on candidate cards | Differentiator opportunity later; note only |

### Ashby — "all-in-one, AI embedded, reduce clicks"
| Pattern | What it looks like | RecruitOps mapping |
|---|---|---|
| Insights everywhere | Metrics live inside pipeline/stage headers, not just an Analytics tab | Put days-in-stage + conversion % inline on pipeline columns (M5 data reused at point of work) |
| Reduce clicks doctrine | Bulk actions, keyboard-first lists, one-screen candidate review | Add **bulk stage moves** + candidate quick-review drawer (already prototyped in set-6 pipeline) |
| Custom reporting | Users compose reports from any entity | Matches FR-M5 scheduled reports direction; keep report builder simple: columns + filters + schedule |
| Automation with human touch | Triggers suggest actions; human confirms | Approval reminders via ADR-0026 outbox = natural first automation |

### Manatal — simplicity for non-expert recruiters
| Pattern | What it looks like | RecruitOps mapping |
|---|---|---|
| Kanban + list toggle | Same data, two views, drag-drop everywhere | Pipeline board exists; add **list view toggle** (recruiters differ; HMs prefer lists) |
| Quick-screen | Preview candidate card from within pipeline, advance/reject without leaving board | Adopt: quick-screen footer inside candidate slide-over |
| Search-as-you-type across everything | One global search box, instant contextual results | Ctrl+K command palette already prototyped — extend to candidates/CVs |
| AI recommendations w/ scores | Ranked suggestions with relevance % | Screening-rank concept (labelled concept) matches; keep explainability visible |
| Compliance center | GDPR/PDPA consent management surfaced as a feature | BRD §4 retention/right-to-be-forgotten → make a **visible Privacy page**, not buried settings |
| Mobile PWA + notifications | Full features on phone; push notifications | Hiring managers are occasional users on phones — mobile-first HM views matter more than recruiter mobile |

### Enterprise approval suites (Workday/SAP class) — governance patterns
- Approval inbox with **delegation** ("out of office → route to X") and batch approve.
- Every approval shows: amount/headcount context, history, and *who else is on the chain*.
- Threshold rules displayed as plain-language sentences (">$10k adds CFO").
- **RecruitOps gap:** threshold injection is FRD-specified but unmodelled (known gap); delegation doesn't exist. Both are table-stakes for CHRO buyers.

## 2. Design-theory principles to hold (with why)

1. **Recognition over recall (Nielsen #6):** never make users remember chain position or token URLs — always show current step, remaining steps, and copyable link.
2. **Progressive disclosure:** default screens show the 20% used daily (Greenhouse hides advanced fields behind "More"); RecruitOps detail pages should collapse JD text, custom answers, and audit logs behind expanders.
3. **One primary action per view (Hick's law):** every screen gets exactly one blue button; everything else is ghost/secondary. Set-6 already does this — keep it law.
4. **Immediate feedback (Nielsen #1):** every async act (submit, publish, bulk upload, invite) shows progress + completion state; silence reads as failure.
5. **Error prevention > recovery (Nielsen #5):** confirmations typed-with-reason for irreversible acts (already designed); disable invalid choices instead of validating after (e.g., posting creation only lists Approved requisitions).
6. **Peak-end rule:** the two emotional peaks are *application submitted* (candidate) and *offer accepted / req approved* (staff). Invest ceremony there — reference numbers, next-steps, shareable confirmation.
7. **Working memory ≤4 (Cowan):** approval chain shows max ~4 visible steps then "…+2 earlier (collapsed)".
8. **Fitts's law for frequent acts:** the daily loop is *open pipeline → advance candidate*. That control must be the largest target in its region (drag + explicit button both).
9. **Consistency creates trust in governance products:** same status chip vocabulary everywhere (one `StatusPill` source of truth — already in packages/ui).
10. **Bilingual as layout constraint, not translation:** Myanmar text needs ~1.75 line-height and no uppercase transforms; every string must survive lengthening ~130%.

## 3. Feature-by-feature recommendations (RecruitOps scope)

### M1 Requisition & approval — your differentiator, invest most here
- **Adopt:** plain-language threshold rules ("headcount > 5 adds CFO") shown ON the requisition before submit; delegation/out-of-office for approvers; batch approve in inbox; chain visualization with collapsed history.
- **Differentiate:** nobody markets "immutable snapshot audit trail" — render it as a **timeline receipt** (who/when/which version, printable). This is your "the record is the product" made visible.
- **Watch out:** don't copy consumer-app playfulness here; approvers are executives — calm density wins.

### M2 ATS/pipeline/search/AI
- **Adopt:** kanban/list toggle; quick-screen drawer; bulk stage moves with undo; global search-as-you-type (Ctrl+K); inline column metrics (days-in-stage); saved filters/views.
- **Adapt carefully:** AI screening ranks must show *why* (matched skills highlighted in CV text) — explainable or hidden. Manatal-style percentages without explanation erode trust and raise PDPA questions.
- **Avoid:** auto-reject automations (bias risk, PDPA exposure, contradicts "human judgment" positioning).

### M3 Interviews/scorecards
- **Adopt:** interview kits per stage (questions + rubric bundled); debrief decision view comparing all scorecards side-by-side after unblind; calendar availability placeholder until real sync lands.
- **Differentiate:** blind evaluation is enforced server-side — market it in-UI: a small shield badge "blind until all submit" educates HMs that this is fairness infrastructure, not friction.

### M5 Analytics
- **Adopt Ashby's doctrine:** put metrics where work happens (pipeline header counts, requisition age badges) rather than one gated dashboard; keep the gated dashboard for trends/funnels.
- Honest-clock caveat stays labelled until Module 4.

### M7 RBAC/admin
- **Adopt:** effective-permissions preview ("what CAN this role do today?" summary before save); deactivate-user flow listing stalled chain steps (drawn already — build it).
- Permission matrix: group by module with module-level tri-state checkboxes.

### Public candidate surface
- **Adopt:** application status timeline for candidates (submitted → viewed → shortlisted timestamps); job alerts (email me similar jobs); autofill-friendly inputs; LinkedIn profile paste-import (parse URL, not scraping).
- **Mobile-first apply:** >70% of traffic from job ads will be phones; single-column, autocomplete everywhere, CV upload from cloud drive.

## 4. Where RecruitOps should NOT follow the leaders
1. **Don't copy agency-CRM complexity** (Manatal's client management) — in-house TA needs less chrome, not more.
2. **Don't chase Voice-AI interviewing yet** — unverified Burmese accuracy makes it a credibility risk today (ADR-0009 discipline).
3. **Don't soften the audit trail** for tidiness — competitors hide decisions; showing them IS the brand.
4. **Don't gate clarity:** even Core-tier companies must see *their own* funnel basics; gate only comparative/trend analytics (matches EnableAnalytics intent without blinding small customers).

## 5. Top 10 prioritized UI upgrades (impact × effort, given set-6 prototypes exist)
1. Inline pipeline column metrics + bulk stage moves (Ashby doctrine, M5 reuse)
2. Debrief decision view post-unblind (Greenhouse pattern, M3 differentiator support)
3. Application status timeline for candidates (peak-end + trust)
4. Plain-language threshold-rule preview on requisition form [needs FR-M1-03 modelling]
5. Global Ctrl+K search spanning candidates/CVs/postings
6. Approver delegation + batch approve in inbox
7. Interview kits bundling questions into rounds
8. Privacy/compliance page surfaced (retention clock visible per candidate)
9. Kanban/list toggle + saved views on pipelines & requisitions
10. Effective-permissions preview in Role Builder

*Full evidence trail lives in this folder; prototypes demonstrating most items: `design-prototypes-6/`.*
