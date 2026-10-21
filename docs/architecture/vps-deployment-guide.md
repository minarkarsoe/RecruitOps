# RecruitOps — VPS Production Deployment Guide

> **Target OS**: Ubuntu 24.04 LTS / 22.04 LTS  
> **Deployment Architecture**: Docker Compose + Nginx Reverse Proxy + Let's Encrypt SSL  
> **Supported VPS Providers**: Hetzner, Contabo, RackNerd, Netcup, DigitalOcean, AWS EC2

---

## 1. VPS Recommended Hardware Sizing

Select the appropriate VPS plan based on company headcount and expected resume upload volume ([server-sizing-guide.md](file:///c:/Users/Min%20Arkar%20Soe/Desktop/Freelance_Project/RecruitOps/docs/architecture/server-sizing-guide.md)):

| Company Size Tier | Employee Count | Recommended vCPU | Recommended RAM | NVMe SSD Storage | Est. Monthly VPS Cost |
|---|---|---|---|---|---|
| **Small Tier** | < 50 staff | 2 vCPU | 4 GB RAM | 50 GB NVMe | ~$4.50 – $6.00 / month |
| **Medium Tier** | 50 – 500 staff | 4 vCPU | 8 GB RAM | 150 GB NVMe | ~$5.50 – $10.00 / month |
| **Enterprise Tier** | > 500 staff | 8 vCPU | 16 GB RAM | 500 GB NVMe | ~$20.00 – $40.00 / month |

---

## 2. Step-by-Step VPS Deployment Instructions

### Step 1: Connect to VPS & Install Docker
```bash
# Update system packages
sudo apt update && sudo apt upgrade -y

# Install Docker & Docker Compose Plugin
curl -fsSL https://get.docker.com -o get-docker.sh
sudo sh get-docker.sh

# Verify Docker installation
docker --version
docker compose version
```

### Step 2: Clone Repository & Configure `.env`
```bash
# Clone the repository
git clone https://github.com/minarkarsoe/RecruitOps.git /opt/recruitops
cd /opt/recruitops

# Copy and configure environment variables
cp .env.example .env
nano .env
```

**Required `.env` Production Settings** — every secret is left **blank on purpose**: generate
each one on the server (`openssl rand -base64 36`) and paste it in. Copying a value from a document
means everyone who has read the document shares your password; until 2026-10-16 this guide
printed example values that the API accepted, and the API now refuses the signing key it printed.
A blank database, storage or signing secret stops `up` with a clear message, which is the point;
blank `SEED_ADMIN_*` simply creates no Administrator, so you would find out at the sign-in page.
```env
POSTGRES_USER=recruitops_admin
POSTGRES_PASSWORD=
POSTGRES_DB=recruitops_prod

# Signing key, 32+ bytes:  openssl rand -base64 48
JWT_KEY=

# Object storage (MinIO) root credentials — administration only. `minioadmin` is refused.
MINIO_USER=recruitops-storage
MINIO_PASSWORD=
# The API's own storage account, created on every start with access to the CV bucket only.
# A different user from the root one, 12+ characters:  openssl rand -hex 16
MINIO_APP_USER=recruitops-api
MINIO_APP_PASSWORD=

# Behind Cloudflare? none | cloudflare | cloudflare-tunnel (cloudflare-integration-guide.md)
# EDGE_PROXY=none

# First Administrator — used only while the database has no users; 12+ characters.
# Remove both and run `up -d` again once you have signed in (runbook, section 0b).
SEED_ADMIN_EMAIL=
SEED_ADMIN_PASSWORD=

# TLS certificate (Step 3) — required; the stack will not start without it
TLS_CERT_DIR=/etc/recruitops/tls
ACME_WEBROOT=/var/www/recruitops-acme

# Feature Flag Add-on Toggles
FeatureFlags__EnableAiProfiling=true
FeatureFlags__EnableAnalytics=true
FeatureFlags__EnableBulkCvUpload=true
FeatureFlags__EnableFullTextSearch=true
```

---

### Step 3: Domain DNS & Let's Encrypt Free SSL Setup

1. **DNS A Record**: Point your customer subdomain A Record to your VPS Public IP Address:
   - `company.recruitops.com` → `YOUR_VPS_IP`
2. **Certificate: follow [deployment-runbook.md](deployment-runbook.md), section 0b**, in order —
   a throwaway placeholder, `up -d`, then `certbot certonly --webroot` with the repo's deploy
   hook (`sudo apt install -y certbot` first), then `certbot renew --dry-run`. The certificate is
   issued and renewed through the running container, so nothing else may listen on port 80.
   ⚠️ Not `certbot --nginx` / `python3-certbot-nginx` (a second nginx on the host, fighting the
   container for port 80) and not `--standalone` (needs port 80 free, so renewals fail while the
   stack runs). This step said both until 2026-10-16.

---

### Step 4: Launch Production Docker Stack

Execute Docker Compose using the production profile ([docker-compose.prod.yml](file:///c:/Users/Min%20Arkar%20Soe/Desktop/Freelance_Project/RecruitOps/docker-compose.prod.yml)):

```bash
# Launch containers in background
docker compose -f docker-compose.prod.yml up -d --build

# Verify container status
docker compose -f docker-compose.prod.yml ps
```

---

### Step 5: Verify Health & Automated Database Migration

Database migrations execute automatically on container boot. Verify deployment health:

```bash
# Check version API endpoint
curl https://company.recruitops.com/api/version

# Check database & storage health
curl https://company.recruitops.com/health
```

Expected `/health` response:
```json
{
  "status": "Healthy",
  "totalDuration": "00:00:00.0123456",
  "entries": {
    "PostgreSQL": { "status": "Healthy" },
    "MinIO Storage": { "status": "Healthy" }
  }
}
```

---

## 3. Maintenance & Automated Backups

### Daily Automated Database Backup Cron Job
Add a cron job to backup the database every night at 2:00 AM:

```bash
# Open crontab editor
crontab -e

# Add daily backup line (saves to /var/backups/recruitops/)
0 2 * * * docker compose -f /opt/recruitops/docker-compose.prod.yml exec -T db pg_dump -U recruitops_admin -d recruitops_prod -F c -f /tmp/backup.dump && docker cp $(docker compose -f /opt/recruitops/docker-compose.prod.yml ps -q db):/tmp/backup.dump /var/backups/recruitops/backup_$(date +\%Y\%m\%d).dump
```
