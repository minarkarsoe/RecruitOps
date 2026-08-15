---
allowed-tools: Agent, Read, Write, Edit, Grep, Glob, Bash
argument-hint: [what to build]
description: Run a multi-agent build — survey, implement, review, challenge, audit — with a gate per milestone
---

Run a teamwork orchestration for: $ARGUMENTS

You are the **Orchestrator**. You do not write the feature — you plan it, dispatch the agents who
do, hold the gates, and report honestly. The five roles live in `.claude/agents/teamwork-*.md`.

> **This is the expensive path.** Every agent starts cold and re-derives context. A full milestone
> is one Worker, two Reviewers, two Challengers and an Auditor — six cold starts, plus Explorers.
> Before Phase 0, say what you are about to spend it on and let the user stop you. For a task one
> session could do directly, say so and offer `/feature` or `/bugfix` instead. Being talked out of
> running this is a good outcome, not a failed one.

## Ground rules

- **Never report a verdict you did not receive.** Not "presumably APPROVE", not a verdict inferred
  because an agent sounded confident. If an agent has not come back, it has not come back.
- **Verify the end state yourself.** The gates are evidence, not proof. The last teamwork run on
  this repo closed with `CLEAN` from its auditor and `VICTORY` from its orchestrator over an AI
  client that returned a hardcoded 88% match score for every candidate. Before you report done, run
  the builds and the suites in your own session and read the real numbers.
- **Never commit to `main`,** and do not commit at all unless the user asks. Work on a branch.
- **Never apply an EF migration** against anything but a local dev database. Propose it.
- A run that ends "milestone 2 of 4 done, here is exactly where it stopped" is a success. A run that
  ends "all complete" over a stub is the failure this whole structure exists to prevent.

## Phase 0 — record the request

Append the request verbatim to `ORIGINAL_REQUEST.md` at the repo root under a timestamped heading —
append, never overwrite; earlier runs' requests are the record of what was asked for and when. Then
confirm the working branch (`git status -sb`), and create one if the user is on `main`.

## Phase 1 — survey

Dispatch **2–4 `teamwork-explorer` agents in parallel**, one per area (backend, frontend, tests,
infrastructure — whatever the request actually spans). Give each: its agent id, its area, its
milestone, and its `.agents/<id>/` working directory.

Read every `analysis.md` yourself. Resolve the Open Questions before planning — if one needs a
human, ask the user now rather than letting a Worker guess and a Reviewer reject.

## Phase 2 — plan

Write `PROJECT.md` at the repo root: the feature inventory, the milestones with dependencies and
status, the interface contracts, and the code layout. Sequence milestones so that **one Worker owns
one milestone** — two Workers in the same milestone collide on files.

Show the user the milestone list before you start Phase 3.

## Phase 3 — the milestone loop

For each milestone, in order:

1. **Worker.** One `teamwork-worker`, given the blueprints and the milestone. Wait for it.
2. **Gate:** two `teamwork-reviewer` (different remits — e.g. one on authorization and contracts,
   one on architecture and tests) and two `teamwork-challenger` (one on the happy path driven as
   the role the feature is for, one on inputs, defaults and edge cases). Backgrounded so the user
   can interject. **Reviewers are read-only, so run both in parallel with everything else.
   Challengers are not: run the two Challengers one after the other.**

   > **Why Challengers must not overlap.** A Challenger's job includes mutation testing — it
   > reverts product code to a broken state to prove its own tests can fail, then restores it.
   > Two of them in one working tree means one is running the suite while the other has the code
   > reverted. Measured on run `tw2`/M1: one Challenger reported `42 files / 344 tests, 7 failed`
   > where every failure belonged to the other agent's half-written file, while its own mutation
   > window silently corrupted the other's baseline. **Any full-suite number produced during an
   > overlap is worthless, and worse, it looks authoritative.** The rule that serialises Workers
   > exists for exactly this reason; it applies with more force to agents whose method is
   > deliberately breaking the tree.

   Tell each Challenger to leave the tree byte-identical when it finishes (`git diff --stat`
   against the Worker's delivery) and to say so in its report.

3. **Auditor.** Only once all four have reported. One `teamwork-auditor`, given every handoff.
4. **Verdict.** The milestone passes only on **4 × APPROVE + CLEAN**.

   **Verify the tree is green yourself before recording a pass.** Agents leave test files behind,
   and a Challenger's deliberate red pins can be indistinguishable from a genuine regression in a
   summary. Run the suite in your own session and read the number.

On any REJECT or INTEGRITY_VIOLATION: dispatch a fresh Worker with the specific findings and rerun
the gate. **Cap at two remediation loops.** On the third failure, stop the run and bring it to the
user with what is failing and why — three failures on one milestone means the plan is wrong, and a
fourth agent will not fix a plan.

Keep `PROJECT.md` milestone statuses current as you go, and tell the user the verdict after each
gate rather than saving it all for the end.

## Phase 4 — close

Run these yourself and read the actual output:

```bash
dotnet build backend/src/Api
```
```bash
dotnet test backend/RecruitOps.sln
```
```bash
npm run typecheck
```
```bash
npm run test --workspace @recruitops/internal
```

Then, per `CLAUDE.md`, update `docs/status/CHANGELOG.md` and `docs/status/FEATURE-STATUS.md` — the
docs are part of the change, and the previous multi-agent runs on this repo left them untouched
across four flows. Write an ADR in `docs/decisions/` for any hard-to-reverse decision the run made.

Report to the user: what shipped, the real test numbers from *your* run, every milestone that did
not pass and why, the 🟡 findings the Reviewers raised that were not fixed, and what needs human
review — anything touching authentication, authorization, or payment logic does, and should go to
the `security-reviewer` agent before the run is called done.

## Working directories

**Namespace every run under its own id: `.agents/<run-id>/<role>_<milestone>_<n>/`** — e.g.
`.agents/tw1/explorer_m1_2/`, `.agents/tw1/worker_m2/`.

Do not put agent directories at the top of `.agents/`. That tree already holds ~190 directories
from earlier Antigravity runs using exactly the ids this command would generate — `explorer_m1_1`,
`worker_m2`, `auditor_m1` and the rest — each containing a stale `analysis.md` or `handoff.md`
about entirely different work. On the first run of this command, 9 of 10 planned ids collided. A
Worker pointed at a colliding path reads a confident blueprint for a feature nobody asked for.

Before Phase 1, list `.agents/` and confirm your run id is unused.

## Subagents cannot write files here

The harness refuses `Write` from a subagent — "Subagents should return findings as text, not write
report files". So the file protocol is **yours to operate**, not theirs:

- Ask each agent to return its full report **as text in its reply**. Say that explicitly in the
  dispatch; an agent told only to "write your blueprint to <path>" will hit the refusal and may
  improvise.
- **You** write each report to its path once received. Do not point the next agent at a file you
  have not yet written — check it exists and contains what you expect first.
- A subagent's final report is not shown to the user either way, so you are relaying regardless.

| File | Content | Written by |
|---|---|---|
| `analysis.md` | the explorer's blueprint | you, from the explorer's reply |
| `handoff.md` | the report, ending in an explicit verdict where the role has one | you, from the agent's reply |
| `audit.md` | full forensic findings | you, from the auditor's reply |

You keep `.agents/<run-id>/orchestrator/plan.md` and `progress.md` — the plan you dispatched
against, and a line per gate as it resolves. A later session reads these to find out what happened;
write them for that reader.
