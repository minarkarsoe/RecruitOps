# ADR-0014 — Multi-channel sourcing (Viber / Telegram / Facebook) as Module 8, immediately post-MVP

- **Date:** 2026-07-27
- **Status:** Accepted
- **Related:** [ADR-0006](ADR-0006-mvp-scope.md), [ADR-0011](ADR-0011-commercial-model-v2.md)

## Context

The v2.0 strategy names multi-channel sourcing **"The Killer Advantage"** — Viber and
Telegram candidate bots, Facebook Messenger auto-reply from post comments, and one-click
publishing to all three. In this market these are where candidates actually are, and the
original spec's Module 2.1 (multi-channel job posting) only covers *publishing*, not
*conversational intake*.

It is also the feature most likely to close a deal, which creates pressure to pull it into
the MVP.

## Decision

Multi-channel sourcing becomes **Module 8**, built **immediately after the MVP**
(Modules 1, 2, 3, 5) — not inside it.

## Why not in the MVP

The bots are an **intake channel into a pipeline**. Without Module 2's pipeline, candidate
records, duplicate detection and stage model, a bot has nowhere to put a CV and nothing to
report a status change from. Building the channel before the thing it feeds inverts the
dependency and produces throwaway work.

Sequencing it *immediately after* keeps it as the first post-MVP deliverable — close
enough to demo on a roadmap during a sales conversation, without destabilising the
foundation it depends on.

## Scope (Module 8)

### 8.1 Viber & Telegram candidate bots
Accept CVs through the bot, ask short screening questions automatically, and **push status
notifications back** to the candidate when their interview stage changes.

### 8.2 Facebook integration
Auto-reply to post comments via Messenger and accept applications through that thread.

### 8.3 One-click social publisher
Publishing a job posts it simultaneously to a Telegram channel, Viber group and Facebook page.

### 8.4 Position routing
How an inbound message is attached to the right vacancy:
- **Dynamic deep links** carrying a job id — e.g. `t.me/<bot>?start=ACC-001` — and QR codes
- **Chatbot inline selectors** — if a candidate arrives with no context, show buttons for active positions
- **Context match** — derive the position from the Facebook post id the comment came from

## Consequences and constraints

- **Webhooks engine required.** Inbound events from three platforms need a shared,
  idempotent webhook layer with signature verification, retries and replay protection.
  Design it once rather than three times.
- **Depends on Module 2 being right.** Bot intake must reuse the *same* candidate creation,
  duplicate detection and stage model as manual upload — not a parallel path. Duplicate
  detection matters more here, since the stated benefit is filtering **spam/duplicate CVs
  arriving from social channels**.
- **Outbound notifications need candidate consent and an opt-out** — messaging candidates
  on Viber/Telegram is a different privacy posture from email, and the platforms' own
  policies apply.
- **Platform dependency risk.** Facebook/Meta API terms and rate limits change, and bot
  platform access can be revoked. This is a "killer advantage" built on someone else's
  platform — keep the channel adapters isolated behind an interface so one platform's
  breakage doesn't take the module down.
- **On-premise reachability.** Webhooks require a publicly reachable endpoint. An
  on-premise install behind a corporate firewall may be unable to receive them
  ([ADR-0004](ADR-0004-single-tenant-deployment.md)) — this may make Module 8 a
  hosted-tier-only feature, or require an outbound-polling fallback. **Resolve before build.**
- Ties into tier packaging: Mid-Tier explicitly includes "Facebook/Viber Integrations"
  ([ADR-0011](ADR-0011-commercial-model-v2.md)), so this is a **contracted** feature, not
  a bonus.

## Also noted for later

**Internal Mobility** (promotions/transfers, performance data and skill matching for
existing employees) is recorded as a future **Module 9**. No decision taken.
