# Orchestrator Plan — teamwork run `tw2`

## The request

> ငါ့ကို တစ်ခုလုပ်ပေး ငါ approval chain flow ကိုကြည့်တာခုထိမပြီးသေးဘူး အဲ့တာဘာလို့လဲစစ်ပေးပါ။

"I've been looking at the approval chain flow and it's still not finished — check why."

## How this run is shaped, and why

This is a **diagnosis**, not a build. The user asks *why* something is unfinished. The
milestone gate — Worker + 2 Reviewers + 2 Challengers + Auditor, six cold starts — answers
"is this change correct", not "why is this incomplete". So:

- **Phase 1 (survey) runs first and alone.** Three Explorers: backend lifecycle, frontend
  surface, tests/docs+history.
- **Phase 2/3 are conditional.** The gate is only spun up if the user, having seen the
  diagnosis, chooses to fix it in this run. Told the user this up front so they could stop
  the spend.

Run id `tw2` confirmed unused — `.agents/` holds 212 directories from earlier Antigravity
runs using exactly the ids this command generates. Namespaced under `.agents/tw2/`.

## Orchestrator's own trace (done in parallel with the Explorers, not delegated)

The ground rule is "verify the end state yourself". I traced the decisive link — whether the
chain template is ever instantiated — before any Explorer reported, because that single
question separates "never built" from "built but unreachable".

**The backend engine works.** Verified directly:

| Link | State | Evidence |
|---|---|---|
| `POST /requisitions/{id}/submit` stamps approval rows from the template | WORKS | `RequisitionService.cs:160-179` — steps ordered by `Sequence`, one `RequisitionApproval` per step, `Decision = Waiting` |
| Turn order enforced | WORKS | `RequisitionService.cs:203-205` — only the lowest-sequence `Waiting` row is actionable, and only by its own approver |
| Status transitions | WORKS | `RequisitionService.cs:216-223` — any reject → `Rejected`; all approved → `Approved` |
| Approver inbox | WORKS | `GET /requisitions/inbox`, filtered in SQL with an explicit turn check |
| `POST /requisitions/{id}/decision` | EXISTS | `RequisitionsController.cs:79-80` |
| Chain template **edit / delete** | **MISSING** | `ApprovalChainsController.cs` exposes GET, GET/{id}, POST only |

This matters for how the run is framed: the answer to "why isn't it finished" is **not**
"the approval engine was never written". It was written, in commit `b497faf`
(*feat(requisitions): Module 1 — requisition lifecycle and sequential approval*), and it is
not stubbed. Nothing approval-related is sitting uncommitted in the working tree.

So the gap is somewhere between the user and a working engine. Leading candidates before the
Explorers report: an uneditable chain template (confirmed), and the frontend surface
(delegated to `explorer_ac_2`, unknown at time of writing).

## Explorers dispatched

| id | area | question it owns |
|---|---|---|
| `explorer_ac_1` | backend lifecycle | where the chain of causation breaks; permissions seeded?; ADR-0003 scoping on approval endpoints |
| `explorer_ac_2` | frontend surface | what an Approver can actually *do* on screen; routes/nav reachability; frontend→backend endpoint mismatches |
| `explorer_ac_3` | tests, docs, history | what the spec says "finished" means; does any test catch a missing instantiation step; when did this stall |

Each was told to return its report as **text**, not to `Write` — the harness refuses file
writes from subagents, and an agent told only "write to <path>" improvises.
