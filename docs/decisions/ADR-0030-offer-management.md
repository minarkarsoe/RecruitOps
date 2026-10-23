# ADR-0030 — Offer management (Module 4): approval, reach, signing and the candidate link

**Status:** Accepted · **Date:** 2026-10-21 · **Module:** 4 (Offer & Pre-boarding)

## Context

Module 4's spec (`docs/product/modules/04-offer-and-preboarding.md`) left five questions open. Four
were the product owner's and were answered on 2026-10-21; the rest follow from the designs
(`design/internal/offer-create.html`, `offer-dashboard.html`, `preboarding-review.html`,
`design/public/offer-portal.html`) and from what Module 1 already does.

Module 4 is built in slices: **A** — offers drafted from an application, the budget check, internal
approval, the dashboard; **B** — sending, the candidate portal, signing and declining, reminders,
expiry; **C** — pre-boarding documents, their review, the IT/Admin handoff. The HRMS export is an
add-on (ADR-0007). This ADR records the decisions all three rest on.

## Decision

### 1. Offer approval reuses Module 1's machinery — the rules, not a copy of them

The rules that decide a step — only the current round is live (ADR-0023), a later step may
approve forward but never reject forward (ADR-0024), and a step closed on someone's behalf records
who actually acted — move into one domain helper, `ApprovalRail`, used by **both** requisitions
and offers. Building a second, simpler mechanism beside the first is how the two drift apart
(`offer-create.html` draws the same rail for the same reason).

`ApprovalChain` gains a **purpose** — `Requisition` (every existing chain) or `Offer`. An offer is
routed through its department's offer chain, else the company-wide offer chain, exactly as a
requisition finds its chain. No offer chain → submitting an offer that needs approval is refused
with a message saying so; nothing is guessed. The threshold-inserted approver drawn in the designs
is still not in the data model (see `settings-org.html`); it is not built here either.

An approver's **rejection returns the offer to Draft**, its round kept beside the next one. It is
never the `Rejected` status, which belongs to the candidate (§3).

**A decision names the round it was made on.** An offer's decide request carries the round the
approver was shown, and is refused if the offer was revised and resubmitted since — an approver
never approves terms they did not see. An offer's status and each step's decision are concurrency
tokens, so a revise racing an approval, two submits, or a rejection racing an approve-forward cannot
both commit; the second is told to reload. (Requisitions do not yet bind the round or carry the
tokens; that gap is Module 1's and is recorded in FEATURE-STATUS.)

### 2. When an offer needs approval

**Over budget, or by company setting** (product owner, 2026-10-21):

- *Over budget*: the monthly package — base salary plus allowances — exceeds the requisition's
  **salary budget, which is MMK per month per hire** (as `requisition-new.html` labels it; the
  requisition form now says so too). A requisition with no budget is never over it.
- *By setting*: `Company.RequireOfferApproval` sends every offer through approval.

The check runs when the terms are entered, not after sending: the editor shows the warning and the
amount the base must come down to for the offer to go straight to the candidate.

### 3. Status vocabulary — one addition to the 2026-08-18 list

`Draft → PendingApproval → Approved → Sent → Viewed → Accepted / Rejected / Expired`

**`Approved` is added**: cleared to send, not yet sent. Approval and sending are separate acts by
different people (an approver clears it; the recruiter sends it, with the expiry date it then
carries), and without the state an approved offer would have to be either sent automatically or
pushed back to Draft. An offer that needs no approval goes from Draft straight to Sent.

`Rejected` means **the candidate declined**. It is a different enum from
`PipelineStatus.Rejected` with the same label; the two are never conflated in code or analytics,
and the dashboard only ever shows the offer's own pill in an Offer column.

Only a Draft's terms can be edited. An Approved offer that needs changing is **revised** back to
Draft, which voids the approval: the next submission opens a fresh round.

### 4. Who reaches an offer

| Who | Reach |
|---|---|
| Admin, HR Director, Recruiter (recruitment staff) | Every offer; create, edit, submit, revise, delete a draft |
| Hiring Manager | View-only, offers in their own departments. **Salary hidden** unless `Company.ShowOfferSalaryToHiringManagers` (product owner, 2026-10-21: hidden by default, a company setting shows it) |
| Anyone **named on a step of the offer's live (latest) approval round** | **That offer only**: candidate name, position, department and terms, and the decision. Never the candidate's application, CV, notes or scorecards |
| Approver role otherwise, Interviewer | Nothing |

The named-step row is a narrow exception to ADR-0018, the same shape as the panel exception
(ADR-0017 §4): being named is the grant, and it reaches the offer, not the candidate. It is the
**live round only** (security review, 2026-10-21, M2): once a chain changes and a new round names
someone else, an earlier approver stops reading the offer's salary — "any round, for good" would be
standing salary access through a chain nobody remembers editing. A person named on a company-wide
offer chain reaches every offer routed through it while they are on its live round; that is what
naming them means. An unreachable offer is a 404, never a 403 (ADR-0003).

**Approvers' comments follow the salary rule**: a viewer who may not see salaries sees each step's
decision but not its comment, because "come down to 800,000" is the package in other words.

### 5. Signing and the candidate link (decided 2026-10-21; built in slice B — see §5a)

- **Signature: typed full name + consent** (product owner, 2026-10-21). On acceptance the system
  records the typed name, the time, the client IP and user agent, and a **SHA-256 of the exact
  letter rendered to the candidate**. No third-party provider. ⚠️ Whether this satisfies Myanmar
  law for an employment contract must be confirmed by the customer's counsel — the product does
  not assert it.
- **Link: an unguessable token plus a 6-digit code emailed to the candidate's address on file**
  (product owner, 2026-10-21). The link alone shows nothing sensitive; the code unlocks the offer
  and, after acceptance, the document uploads. A forwarded link is useless without the mailbox.
  SMS is the obvious upgrade once an SMS provider exists. The link stops working at the offer's
  expiry date, which may be **at most 90 days** after the offer is submitted — the link reaches
  salary and, after acceptance, identity documents.
- **The candidate's name is rendered on one line** in the letter: it comes from an anonymous form,
  and a line break in it would add paragraphs to the text the candidate signs.

### 5a. How slice B builds §5 (amendment, 2026-10-23)

Decided while building, before code; the portal screens were drawn first
(`design/public/offer-portal.html`, states `#link` … `#invalid`) and the product owner chose a
**printable web page over a PDF** for the letter (no new package; the browser saves it as a PDF).

- **The link is an address, not a credential.** `/offer/{token}` on the public site, the token 32
  random bytes, stored as is so a reminder can repeat it. What protects the offer is the code: the
  link alone shows the company's name and the candidate's email address masked (`k•••••@gmail.com`)
  — no name, position or terms. The public site's address is `PublicSite:BaseUrl`; without it,
  sending is refused rather than mailing a link that goes nowhere.
- **The code.** Six digits from a cryptographic generator, emailed through the outbox to the
  address on the application and to nowhere else; stored only as a hash; valid **10 minutes**;
  **5 wrong tries** end it; a new code replaces the old; at most **5 codes per offer per 24 hours**.
  Worst case, someone holding a forwarded link guesses 25 times a day — over an offer's longest
  life (90 days) a 0.2% chance, while the candidate receives every code they trigger. The code is
  in the outbox row's payload until it is sent; the delivery log never shows payloads.
- **The session.** A correct code sets an `HttpOnly`, `SameSite=Strict` cookie scoped to that
  offer's API path, valid **2 hours**, stored only as a hash. Every read of terms, letter or
  answer requires it. A new code means a new session.
- **`Viewed` means the candidate unlocked it** — the first correct code. Opening the link is not
  viewing it: the link shows nothing to view.
- **Accepting** takes the typed full name (1–200 characters, one line) and a consent tick, and
  the SHA-256 of the letter the page showed. The server re-renders the letter; if the hash differs
  it refuses and the page reloads, so what is recorded is what was read. Recorded in
  `OfferSignature`: the name as typed, the time, the client IP (the real one — ADR-0029 L6), the
  user agent, **the letter text itself** and its hash. Declining takes an optional reason (up to
  1,000 characters). Either answer is final from the link, emails the candidate's copy or
  acknowledgement, and tells the recruiter who created the offer.
- **Expiry.** The link works to the end of the expiry date **in the company's time zone** (UTC if
  none is set). A Sent or Viewed offer past it becomes `Expired` when anything reads it — the
  dashboard, the offer, the portal. There is no sweep job; anything that counts offers (Module 5)
  must read them through `OfferService` or expire them the same way first.
- **Sending** is allowed from `Approved`, or from `Draft` when the offer needs no approval —
  re-checked at send, not trusted from submit. It re-checks the expiry window (not past, at most
  90 days ahead) and that the candidate has an email address.
- **Reminders** (`Remind candidate`): for `Sent` or `Viewed`, at most once per 24 hours, the same
  link again.
- **What candidates are told to contact** is the outbound mail's From address (`Smtp:FromAddress`)
  — set it to a monitored mailbox.
- **Who sees the evidence:** recruitment staff see the typed name, time, IP, user agent and hash;
  everyone else sees only the answer and its date. Signing is not an access change, so it is not in
  the audit log; `OfferSignature` is the record.
- **Not in slice B:** withdrawing a sent offer, moving the application to `Hired` on acceptance,
  and document collection (slice C).

### 6. Records and numbering

- An offer is displayed as `OFR-<year>-<nnnn>`, numbered per company per calendar year, from a
  unique index rather than a counter row.
- The **letter text is per offer**: copied from a template when the offer is created (merge fields
  such as `{{CandidateName}}`, `{{BaseSalary}}`), editable for that offer only, and rendered with
  the current terms at preview and at send. Templates live in `OfferTemplate`; a built-in default
  is used until a company adds its own. There is **no template management screen in `design/`** —
  slice A ships the API only and says so.
- Creating, submitting, deciding, revising and deleting an offer are not access changes, but an
  offer carries salary, so its decisions are on the approval rail itself (who, when, comment) as
  for requisitions. Audit-log entries for those are not part of slice A. **Changing the offer
  policy is audited** (`OfferSettingsChanged`): showing salaries to Hiring Managers is an access
  change, and dropping "approve every offer" removes a financial control.
- One open offer per application is enforced by a partial unique index, not only by the service.

## Consequences

- `RequisitionService.DecideAsync` now runs on `ApprovalRail`. Module 1's existing tests are the
  guard for that refactor; they must pass unchanged.
- Every existing chain is a requisition chain after the migration. A company must create an offer
  chain before an offer that needs approval can be submitted — the editor says so rather than
  failing silently.
- A Hiring Manager never receives salary fields unless the setting is on: the API omits them, the
  screen does not merely hide them.
- Slice B must build the link and code exactly as §5 says; the portal design shows only the link
  and needs a code step drawn before it is built.
