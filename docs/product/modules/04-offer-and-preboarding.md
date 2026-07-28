# Module 4 — Offer Management & Pre-boarding

**Status:** ⬜ Not started · **Priority:** Medium — after pipeline works end-to-end.

## Purpose

Produce, send, sign and track offers, then collect joining paperwork before day one.

## Features

### 4.1 Dynamic Offer Letter Generation
Generate appointment letters from **system templates**, auto-filling candidate/role data.

### 4.2 E-Signature & Status Tracking
Send the offer letter directly to the candidate and **watch the digital signing status**.

### 4.3 Pre-boarding Document Collection
Send a **secure link** for the candidate to upload required personal documents before joining.

### 4.4 Automated Notifications
When a candidate **accepts** the offer, notify the relevant departments — **IT, Admin** —
automatically (so laptop/access/desk are prepared).

## Entities

- `OfferTemplate` — letter template with merge fields
- `Offer` — job application, salary/terms, generated document, status, expiry
- `OfferSignature` — e-signature provider ref, signed-at, audit trail
- `PreboardingRequest`, `PreboardingDocument` — secure upload link, required doc checklist
- `DepartmentNotification` — which internal teams to alert on acceptance

## Status vocabulary (proposed)

`Draft` → `Sent` → `Viewed` → `Signed` / `Declined` / `Expired`

## Open questions

- **E-signature provider?** (DocuSign, Dropbox Sign, or a lightweight in-house click-to-accept.) This is a legally significant choice — needs to be valid in the target market (Myanmar).
- Does the offer need its own approval chain (e.g. salary above band → HR Director sign-off)?
- Secure link security model: expiring token, OTP to candidate's phone/email, or both?
- Document retention for candidates who **decline** — how long before purge? (Module 7.)
