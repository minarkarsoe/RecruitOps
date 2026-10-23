# RecruitOps — Cloudflare Integration & Security Setup Guide

> **Scope**: Integrating Cloudflare Free Tier services (CDN, DDoS Protection, SSL, Cloudflare Tunnels, and R2 Object Storage) with RecruitOps.

> **Optional — nothing in RecruitOps needs Cloudflare, and nothing in this repo uses it unless an
> install is configured to.** Nobody has to buy or sign up for anything. Cloudflare entered the project as a plan, not a purchase: ADR-0013 (2026-07-27) chose
> R2 for *hosted* installs' CV storage because it charges nothing for downloads, and this guide
> (2026-08-15) describes putting a server behind Cloudflare's free proxy or tunnel. Out of the box
> every stack in this repo stores CVs in its own MinIO, `EDGE_PROXY` is `none`, and the tunnel
> container does not start — so an install that never touches Cloudflare is the default, and
> complete. Everything below applies only to an install whose owner chooses Cloudflare.

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
   - Route `company.recruitops.com` → `https://127.0.0.1:443`, with **Origin Server Name** set to
     `company.recruitops.com` and a Cloudflare Origin Certificate in `TLS_CERT_DIR` (the compose
     stack requires one either way). Loopback, because cloudflared runs inside the proxy's network
     namespace — which is also what lets nginx trust it and nothing else (below). `127.0.0.1`, not
     `localhost`, which may resolve to `::1`.
   - The token is an environment variable, like the stack's other secrets — anyone who can run
     `docker inspect` can read it, and whoever holds it can run a connector for your hostname.
     Treat it like `JWT_KEY`. After the proxy is restarted by Docker itself (a crash), run
     `docker compose -f docker-compose.prod.yml --profile cloudflare-tunnel restart tunnel`;
     Compose restarts it on its own for a `docker compose restart proxy` (measured).
   - ⚠️ Not port 80: it answers only certificate challenges and redirects everything else to
     HTTPS (ADR-0029), so a tunnel pointed there loops.
4. **Close the ports.** With the tunnel up nothing needs 80/443 published. **Close them in the
   cloud provider's firewall** — it holds whatever command is typed later.
   **Docker-published ports bypass UFW** — `ufw deny 443` does not close them.
   - ⚠️ Not with `docker-compose.override.yml` alone. Compose reads that file only when no `-f` is
     given, and every command here passes `-f docker-compose.prod.yml` — so the ports stayed open
     (measured 2026-10-23, Compose v5.6: `config` still listed 80 and 443). This step used to say
     an override file was enough. If you do use a file, name it and pass it every time:
     `services: proxy: ports: !reset []` in `docker-compose.no-ports.yml`, then
     `docker compose -f docker-compose.prod.yml -f docker-compose.no-ports.yml --profile
     cloudflare-tunnel up -d`. One later `up` without the second `-f` publishes them again.
     Check: `docker compose -f docker-compose.prod.yml -f docker-compose.no-ports.yml config
     --format json | grep -o '"published": *"[0-9]*"'` prints nothing (without the second file it
     prints 80 and 443), and `curl -k https://<server's public IP>` from outside fails.
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

**A forged header from outside Cloudflare gets nobody anywhere.** nginx honours `CF-Connecting-IP`
only from the trusted peers above; a client connecting to the server directly is not one, so its
own address is used whatever it sends. Two things this rests on, and one it does not cover:
- **Cloudflare sets the header.** Method A trusts every Cloudflare address, which serves every
  Cloudflare customer; the address is honest because Cloudflare overwrites `CF-Connecting-IP` on
  what it proxies. **Keep Pseudo IPv4 off** (Network settings) — "Overwrite headers" replaces an
  IPv6 visitor's address with a made-up IPv4 one.
- **Docker passes the peer on.** Method A needs the connection to reach nginx with Cloudflare's
  address. nginx listens on IPv4 only, and an IPv6 connection may be relayed by Docker from the
  bridge gateway — which is not trusted, so those visitors fall back to one shared bucket (safe,
  but silent). **Give the hostname an A record only, no AAAA.**
- **Not covered: someone else's Cloudflare zone.** Any Cloudflare account can point its own
  proxied hostname at your origin's IP, and that traffic arrives from Cloudflare's ranges like
  yours — past *your* WAF rules. Firewalling to Cloudflare's ranges does not stop that; **turn on
  Authenticated Origin Pulls** with a per-zone certificate, or use Method B with the ports closed.
  (This guide used to say firewalling was what kept the address honest. It is not — but it is
  part of keeping the WAF in the path.)

**Before going live on Method A**, refresh the ranges — they were written from Cloudflare's list
as known on 2026-10-21, not fetched live: run `infra/nginx/realip/update-cloudflare.sh` on a
machine with internet access (it rewrites `cloudflare.conf` from
`https://www.cloudflare.com/ips-v4` and `/ips-v6`, and refuses the answer unless every line is a
range no wider than an IPv4 /12 or IPv6 /28 and there are at least 10 and 5 of them — one
`0.0.0.0/0` would let anyone choose their address). Review the diff, then `docker compose -f
docker-compose.prod.yml restart proxy`. Again yearly, or when Cloudflare announces a change: an
edge address missing from the file puts that edge's visitors back in one bucket.

**Measured 2026-10-21** on the production stack, reading the address the audit log records for a
failed sign-in sent with a forged `CF-Connecting-IP`: from outside, all three modes record the real
peer; from loopback, only `cloudflare-tunnel` takes the header; from a peer inside a Cloudflare
range (a test network numbered `104.16.5.0/24`), `cloudflare` records the visitor's address. Not
measured against Cloudflare itself. **Re-measured 2026-10-23** with nginx and the repo's three
`realip` files: a direct client forging `CF-Connecting-IP` and `X-Forwarded-For` kept its own
address in every mode; a peer at `173.245.48.7` (inside a published Cloudflare range) was replaced
by the visitor only under `cloudflare`; loopback only under `cloudflare-tunnel`. **Go-live check:** from a machine whose public address you
know, sign in once with a wrong password, and confirm Users & Roles → Audit log shows that address.
nginx's access log records each request's TCP peer (`peer=`) and the header it sent (`cf=`), so a
forged header from an untrusted peer is visible there too.

---

### Method C: Cloudflare R2 Object Storage Configuration ([ADR-0013](../decisions/ADR-0013-infrastructure-and-storage.md))

⚠️ **Never tried end to end** — no R2 account has been used with RecruitOps. The settings below are
the ones the code reads (`FileStorageOptions`, section `FileStorage`); this section used to list
`Storage__*` names, including a `Storage__Provider` that does not exist, which configured nothing.

They cannot go in `.env`: `docker-compose.prod.yml` sets the API's `FileStorage__*` itself (to the
bundled MinIO), and `.env` only fills `${…}` placeholders. Put them in a second compose file and
pass it on every command, as in Method B step 4:

```yaml
# docker-compose.r2.yml — docker compose -f docker-compose.prod.yml -f docker-compose.r2.yml up -d
services:
  backend:
    environment:
      FileStorage__ServiceUrl: "https://<ACCOUNT_ID>.r2.cloudflarestorage.com"
      FileStorage__PublicServiceUrl: "https://<ACCOUNT_ID>.r2.cloudflarestorage.com"
      FileStorage__Region: "auto"
      FileStorage__AccessKey: "${R2_ACCESS_KEY}"
      FileStorage__SecretKey: "${R2_SECRET_KEY}"
      FileStorage__BucketName: "recruitops-cvs"
```

Create the bucket in the Cloudflare dashboard first (the API does not create it), with an API
token scoped to that bucket only. Backups: see `deployment-runbook.md` §2 — R2 is not covered by
`scripts/backup.sh`.
