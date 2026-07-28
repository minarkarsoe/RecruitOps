# ADR-0005 — Commercial model: one-time licence + server + maintenance

- **Date:** 2026-07-27
- **Status:** ⛔ **Superseded by [ADR-0011](ADR-0011-commercial-model-v2.md)** (2026-07-27)
- **Kept for the record.** The model below (one-time licence + 10/20% maintenance) was
  replaced by an annual MMK subscription. The *engineering* commitments it identified —
  containerisation, automated migrations, version registry, sizing guide, runbooks — all
  still stand, and matter more under a recurring-revenue model.
- **Related:** [ADR-0004](ADR-0004-single-tenant-deployment.md)

## The model

| Component | Basis |
|---|---|
| **One-Time Fee** | Perpetual licence, charged once |
| **Server cost** | Sized per company — CPU / RAM / SSD differ by customer |
| **Maintenance Fee** | **~20%** of the one-time fee if on the customer's **own server**; **~10%** otherwise |

## Assessment

This is recorded here because it drives engineering priorities, not just pricing.

### What's sound

- **The 10% / 20% inversion is correct and well-reasoned.** Supporting software on
  someone else's hardware genuinely costs more: environments can't be reproduced, OS
  and Postgres versions drift, access may require VPN or a site visit, upgrades must be
  scheduled around the customer's IT, and their backup failures still become our support
  ticket. Charging more for that is right.
- **20% sits at the market norm** for perpetual-licence maintenance (commonly ~15–22%).
- **Unbundling server cost is smart.** It keeps hardware from eating the licence margin
  and lets price scale with customer size without renegotiating the licence.
- **One-time licence fits the buying behaviour** in this market — capex budgets and
  one-off approvals are often easier to clear than a recurring subscription, and it
  pairs naturally with the on-premise preference for HR data.

### The risks worth planning for

1. **The year-2 revenue cliff.** After the initial sale, recurring revenue is only
   10–20% of the licence per customer. Seven modules of ongoing development, plus
   support, will likely cost more than that per customer per year. Perpetual-licence
   vendors usually close this gap in one of three ways — **paid major-version upgrades**,
   **selling modules as add-ons** (Modules 4/6/7 are natural candidates), or **raising
   the maintenance percentage**.
   → **Decided:** the add-on route. See [ADR-0007](ADR-0007-productization-and-addons.md) —
   a generic core product with customization, deferred modules and integrations sold
   separately. Note this only works if add-ons never become per-customer code forks.

2. **Maintenance cost scales with the number of deployments, not revenue.** This is the
   biggest threat to the 10–20% figure. Ten customers on hand-built servers is
   unsustainable; ten customers on identical containers with automated migrations is
   routine. **The automation listed in ADR-0004 is what makes this pricing viable** —
   it is a commercial requirement, not an engineering nicety.

3. **"Maintenance" must be defined in the contract.** If it isn't, customers will
   reasonably expect new features under it. Suggested split: maintenance covers bug
   fixes, security patches, minor upgrades and a defined support response time;
   **new modules and major versions are charged separately**.

4. **Version fragmentation raises support cost per customer.** A published support
   policy (latest and latest-1 only) protects the margin.

5. **Sizing errors are absorbed by us.** If a server is under-specced, the complaints
   and the remedial work land on support. A sizing guide tied to headcount / expected
   applications per month should exist before the first sale.

> This is an engineering-side reading of the model's implications, offered to inform
> planning — not financial advice. Pricing decisions belong with the business.

## Engineering commitments this creates

- Containerised, reproducible deployment; automated migrations on startup (ADR-0004)
- `/api/version` endpoint + customer/version registry
- Documented server sizing guide
- Backup/restore runbook — including for customers who host themselves
- Feature flags for tier-gated modules, if the add-on route is taken
- Upgrade path that is safe to run unattended on a customer's server
