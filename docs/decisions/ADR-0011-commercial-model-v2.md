# ADR-0011 — Commercial model v2: annual MMK subscription, partner-led sales

- **Date:** 2026-07-27
- **Status:** Accepted
- **Supersedes:** [ADR-0005](ADR-0005-commercial-model.md) (one-time licence + maintenance %)

## Context

ADR-0005 recorded a **perpetual one-time licence** plus server cost plus a 10–20%
maintenance fee. The v2.0 strategy replaces this with an **annual subscription priced in
MMK**, sold through local sales partners into large enterprises.

## Decision

| Tier | Target | Annual price (MMK) | Scope |
|---|---|---|---|
| **Mid-Tier** | 1,000–3,000 staff | 20–35 lakh / year | Full ATS, Facebook/Viber integrations, duplicate detection |
| **Enterprise** | 5,000+ staff, banks | 60–80 lakh / year | AWS Multi-AZ, payroll/HRMS API integration, dedicated DB |

**Target market:** domestic enterprises above ~1,000 staff — banks, FMCG/retail chains,
telecoms, factories, MFIs.

**Go-to-market:** *sales-first*, partnered with local sales partners / HR consultants who
know the market, building against real B2B enterprise demand rather than in isolation.
Partner commission: **20–30% of first-year contract value**.

**Financial risk controls:**
- **100% of the annual fee collected upfront**, no monthly billing — the cash is converted
  into cloud infrastructure cost early, hedging MMK depreciation.
- **Currency clause:** the annual fee may be renegotiated if the USD rate moves more than **20%**.
- **Cost optimisation:** Cloudflare R2 (zero egress fees) and AWS Savings Plans / reserved
  instances, targeting ≥50% off the cloud bill.

## Why this is better than ADR-0005

ADR-0005 flagged a **year-2 revenue cliff**: after a one-time fee, only 10–20% recurred,
which was unlikely to fund seven modules of ongoing development. **An annual subscription
removes that cliff** — revenue recurs at 100%, not 10%. This is a materially healthier
model for a product still growing its feature set.

## New risks this model introduces

1. **Churn is now fatal, and it wasn't before.** Under a perpetual licence a lost customer
   still paid. Under an annual subscription, a non-renewal removes the entire revenue line.
   **Retention becomes the key business metric** — which raises the priority of the things
   customers renew for: reliability, support responsiveness, and visible ongoing improvement.

2. **Year-1 margin may be thin.** Partner commission (20–30%) plus cloud infrastructure
   plus implementation effort all land in year 1. **The profit is in the renewal years** —
   which reinforces point 1. Worth modelling year-1 vs. year-2 margin per tier before
   committing to the commission band.

3. **100% upfront is a real sales objection.** It's the right call for FX exposure, but
   some enterprise procurement cannot pay a full year in advance. Expect to need a
   fallback — and note that quarterly billing would partly undo the FX hedge.

4. **The 20% FX clause needs a defined mechanism** — which reference rate, measured over
   what period, and what happens if the customer refuses the adjustment. A clause that
   isn't operationally precise won't be enforceable in practice.

5. **Tier boundaries need enforcement in code.** Mid-Tier vs. Enterprise differ by
   feature, not just by price — this is exactly the feature-flag mechanism required by
   [ADR-0007](ADR-0007-productization-and-addons.md), now with commercial teeth.

## What carries over from ADR-0005 unchanged

- **Add-on strategy** ([ADR-0007](ADR-0007-productization-and-addons.md)) still applies:
  generic core, customization and deferred modules sold separately. It is now
  *incremental* revenue rather than the fix for a structural gap.
- **No per-customer code forks.** With N dedicated installs
  ([ADR-0004](ADR-0004-single-tenant-deployment.md)) this remains the rule that keeps
  operating cost sane — and under a subscription, operating cost is a direct margin hit
  every single year, not a one-off.
- The operational prerequisites in ADR-0004 (containerisation, automated migrations,
  version registry, runbooks) are, if anything, **more** important: they are now an annual
  recurring cost, not a one-time one.

## Consequences

- Pricing is **per tier, per year**, in MMK — no per-seat metering to build.
- Need **renewal tracking** (contract dates, renewal alerts) as an internal concern.
  Note the irony: the agency-era `Contract` entity being deleted in
  [MIGRATION-PLAN](../status/MIGRATION-PLAN.md) was for *customer* contracts; this is a
  different, internal need and should not resurrect that model.
- Feature gating by tier must exist before the first Enterprise deal.
- Sales partners need a **demo environment** — another deployment to operate.
