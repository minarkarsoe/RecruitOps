# Module 8 — Multi-Channel Sourcing & Social Automation

**Status:** ⬜ Not started · **Priority:** First post-MVP deliverable
**Decided in:** [ADR-0014](../../decisions/ADR-0014-multi-channel-sourcing.md)

## Purpose

Meet candidates on the channels they actually use in this market — Viber, Telegram and
Facebook — and pull them into the same pipeline as every other applicant. Positioned as
the product's primary competitive differentiator.

## Features

### 8.1 Viber & Telegram candidate bots
Receive CVs via bot, ask short automated screening questions, and send **status
notifications back** to the candidate when their stage changes.

### 8.2 Facebook integration
Auto-reply via Messenger to comments on a job post, and accept applications in that thread.

### 8.3 One-click social publisher
One job posting publishes simultaneously to Telegram channel, Viber group and Facebook page.

### 8.4 Position routing
- **Dynamic deep links** with job id (`t.me/<bot>?start=ACC-001`) and QR codes
- **Chatbot inline selectors** — buttons listing active positions when context is missing
- **Context match** — infer the position from the originating Facebook post id

## Entities

- `ChannelAccount` — per-company credentials/config per platform (secrets in a secret store)
- `ChannelConversation` — thread ↔ candidate ↔ job binding
- `InboundMessage` / `WebhookEvent` — raw event log, idempotency keys
- `OutboundNotification` — status pushes, delivery state, opt-out flag
- Reuses `Candidate`, `JobApplication`, `JobPosting` from Module 2 — **no parallel intake path**

## Hard requirements

- **Shared webhook engine**: signature verification, idempotency, retry, replay protection
- **Reuse Module 2's duplicate detection** — filtering spam/duplicate CVs from social is a
  headline benefit
- **Consent + opt-out** for outbound messaging
- **Channel adapters behind an interface** — platform API/policy changes are expected
- ⚠️ **Webhooks need a publicly reachable endpoint** — on-premise installs behind a
  corporate firewall may not qualify; may be hosted-tier only, or need outbound polling

## Open questions

- Which platforms first? (Viber and Telegram are likely higher-signal locally than Facebook.)
- Bot per company or one shared bot with company routing? Affects credential management.
- How much screening logic in the bot vs. the application form — is the bot a form, or a conversation?
- Message retention: are bot conversations subject to the same retention policy as CVs (Module 7)?
