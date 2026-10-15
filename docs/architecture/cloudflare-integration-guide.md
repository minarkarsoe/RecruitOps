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
4. **Enable Web Application Firewall (WAF)**: Enable Bot Fight Mode & Automatic HTTPS Rewrites.

---

### Method B: Cloudflare Tunnel (Highest Security - No Open Inbound Ports)

Cloudflare Tunnel lets you close Port 80 and Port 443 on your VPS UFW Firewall completely.

1. **Create Tunnel in Cloudflare Dashboard**:
   - Go to **Zero Trust** → **Networks** → **Tunnels** → **Create a Tunnel**.
   - Copy your Tunnel Token (`TUNNEL_TOKEN`).

2. **Add `cloudflared` Service to `docker-compose.prod.yml`**:
   ```yaml
   services:
     tunnel:
       image: cloudflare/cloudflared:latest
       restart: always
       command: tunnel --no-autoupdate run
       environment:
         - TUNNEL_TOKEN=${CLOUDFLARE_TUNNEL_TOKEN}
   ```

3. **Public Hostname Route**:
   - Route `company.recruitops.com` → `http://nginx:80` inside Docker container network.

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
