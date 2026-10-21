# RecruitOps — Cloudflare Integration & Security Setup Guide

> **Scope**: Integrating Cloudflare Free Tier services (CDN, DDoS Protection, SSL, Cloudflare Tunnels, and R2 Object Storage) with RecruitOps.

---

## 1. Why Use Cloudflare with RecruitOps?

Integrating Cloudflare delivers enterprise-grade security and cost savings for **$0 / month**:

1. **Free Global CDN & SSL**: Automatic SSL/TLS certificates and edge caching for Next.js public portal & Vite React SPA.
2. **DDoS & WAF Protection**: Shields public job application forms (`/jobs/[token]/apply`) and auth endpoints (`/api/auth/login`) from brute-force & bot attacks.
3. **Cloudflare Tunnel (Zero Trust Security)**: Route traffic to your VPS **without opening ANY inbound firewall ports** (no open Port 80/443), hiding your real VPS IP from scanners.
4. **Cloudflare R2 Object Storage ([ADR-0013](file:///c:/Users/Min%20Arkar%20Soe/Desktop/Freelance_Project/RecruitOps/docs/decisions/ADR-0013-infrastructure-and-storage.md))**: S3-compatible resume storage with **$0 egress fees** and 10 GB free monthly storage.

---

## 2. Integration Architecture Options

```mermaid
graph TD
    Client[Browser / Candidate] --> Cloudflare[Cloudflare Edge / WAF / CDN]
    
    subgraph Option A: Standard Proxy
        Cloudflare -->|SSL Port 443| VPSNginx[VPS Nginx Port 443]
    end

    subgraph Option B: Cloudflare Tunnel (Recommended)
        Cloudflare -->|Encrypted Outbound Tunnel| Tunnel[cloudflared daemon in Docker]
        Tunnel --> Container[RecruitOps Nginx Container]
    end

    subgraph Object Storage
        API[Backend .NET 10 API] -->|S3 Protocol| R2[Cloudflare R2 (0$ Egress Fees)]
    end
```

---

## 3. Step-by-Step Setup Guide

### Method A: Standard Cloudflare Proxy (Easiest - 5 Minutes)

1. **Add Domain to Cloudflare**: Change your domain nameservers to Cloudflare (e.g. `ns1.cloudflare.com`).
2. **DNS Record**: Point `A` record for `company.recruitops.com` to your VPS IP address and ensure **Proxy status = Orange Cloud (Proxied)**.
3. **SSL/TLS Setting**: Set SSL/TLS Encryption Mode to **Full (Strict)**.
   - Strict needs a certificate on the server that Cloudflare trusts: create a **Cloudflare
     Origin Certificate** (SSL/TLS → Origin Server), save it and its key as `fullchain.pem` and
     `privkey.pem` in `TLS_CERT_DIR` (see `deployment-runbook.md`, section 0b). Never use
     "Flexible" — it sends sign-ins from Cloudflare to the server unencrypted.
4. **Enable Web Application Firewall (WAF)**: Enable Bot Fight Mode & Automatic HTTPS Rewrites.
5. **Set `EDGE_PROXY=cloudflare` in `.env`** and restart the proxy — see "The visitor's real IP" below.

---

### Method B: Cloudflare Tunnel (Highest Security - No Open Inbound Ports)

Cloudflare Tunnel lets you close Port 80 and Port 443 on your VPS UFW Firewall completely.

1. **Create Tunnel in Cloudflare Dashboard**:
   - Go to **Zero Trust** → **Networks** → **Tunnels** → **Create a Tunnel**.
   - Copy your Tunnel Token (`TUNNEL_TOKEN`).

2. **Turn on the bundled tunnel** — `docker-compose.prod.yml` already has it, as the
   `cloudflare-tunnel` profile (`cloudflared`, pinned by digest). In `.env`:
   ```env
   EDGE_PROXY=cloudflare-tunnel
   CLOUDFLARE_TUNNEL_TOKEN=<the token from step 1>
   ```
   then `docker compose -f docker-compose.prod.yml --profile cloudflare-tunnel up -d`. Keep the
   `--profile` on every later `up`: the tunnel shares the proxy's network namespace, and recreating
   the proxy without it leaves the tunnel attached to a container that no longer exists.

3. **Public Hostname Route**:
   - Route `company.recruitops.com` → `https://localhost:443`, with **Origin Server Name** set to
     `company.recruitops.com` and a Cloudflare Origin Certificate in `TLS_CERT_DIR` (the compose
     stack requires one either way). `localhost`, because cloudflared runs inside the proxy's
     network namespace — which is also what lets nginx trust it and nothing else (below).
   - ⚠️ Not port 80: it answers only certificate challenges and redirects everything else to
     HTTPS (ADR-0029), so a tunnel pointed there loops.
4. **Close the ports.** With the tunnel up nothing needs 80/443 published; remove them with a
   `docker-compose.override.yml` (`services: proxy: ports: !reset []`) or in the cloud provider's
   firewall. **Docker-published ports bypass UFW** — `ufw deny 443` does not close them.
   - cloudflared verifies the origin's certificate. A Cloudflare Origin certificate is accepted
     as is (its `caPool` setting exists for certificates Cloudflare did not sign); keep **Origin
     Server Name** equal to the hostname the certificate covers. Do not reach for `noTLSVerify`.

---

### Required for both methods: the visitor's real IP — `EDGE_PROXY`

Behind Cloudflare, the TCP peer nginx sees is a Cloudflare edge server (Method A) or `cloudflared`
(Method B), never the visitor. Left alone, the API puts **every visitor in one login rate-limit
bucket** — one attacker can lock everybody out of sign-in (ADR-0016) — and the audit log records
Cloudflare's address for every sign-in (ADR-0027). Security review of ADR-0029, finding L6.

**Built (2026-10-21).** `EDGE_PROXY` in `.env` picks what nginx includes from `infra/nginx/realip/`:

| `EDGE_PROXY` | nginx takes the visitor from `CF-Connecting-IP` when the peer is… |
|---|---|
| `none` (default) | never — the peer is the visitor |
| `cloudflare` (Method A) | one of Cloudflare's published ranges (`cloudflare.conf`) |
| `cloudflare-tunnel` (Method B) | `127.0.0.1` — the bundled tunnel, inside the proxy's namespace |

**A forged header gets nobody anywhere.** nginx honours `CF-Connecting-IP` only from the trusted
peers above; a client connecting to the server directly is not one, so its own address is used
whatever it sends. (This guide used to say the opposite — that the origin *had* to be firewalled
for the address to be safe. Firewalling Method A's origin to Cloudflare's ranges, or closing
Method B's ports, is still worth doing: it stops anyone **bypassing Cloudflare's WAF**. It is not
what keeps the address honest.)

**Before going live on Method A**, refresh the ranges — they were written from Cloudflare's list
as known on 2026-10-21, not fetched live: run `infra/nginx/realip/update-cloudflare.sh` on a
machine with internet access (it rewrites `cloudflare.conf` from
`https://www.cloudflare.com/ips-v4` and `/ips-v6`), then `docker compose -f
docker-compose.prod.yml restart proxy`. Again yearly, or when Cloudflare announces a change: an
edge address missing from the file puts that edge's visitors back in one bucket.

**Measured 2026-10-21** on the production stack, reading the address the audit log records for a
failed sign-in sent with a forged `CF-Connecting-IP`: from outside, all three modes record the real
peer; from loopback, only `cloudflare-tunnel` takes the header; from a peer inside a Cloudflare
range (a test network numbered `104.16.5.0/24`), `cloudflare` records the visitor's address. Not
measured against Cloudflare itself — after go-live, sign in and check the IP the audit log shows.

---

### Method C: Cloudflare R2 Object Storage Configuration ([ADR-0013](file:///c:/Users/Min%20Arkar%20Soe/Desktop/Freelance_Project/RecruitOps/docs/decisions/ADR-0013-infrastructure-and-storage.md))

Configure `appsettings.json` or `.env` to store uploaded CV resumes in Cloudflare R2:

```env
Storage__Provider=R2
Storage__ServiceUrl=https://<ACCOUNT_ID>.r2.cloudflarestorage.com
Storage__AccessKey=<R2_ACCESS_KEY>
Storage__SecretKey=<R2_SECRET_KEY>
Storage__BucketName=recruitops-cvs
```
