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

**Required `.env` Production Settings**:
```env
POSTGRES_USER=recruitops_admin
POSTGRES_PASSWORD=Use_A_Very_Strong_Random_DB_Password_2026!
POSTGRES_DB=recruitops_prod

# Generated secret key >= 32 characters (e.g. openssl rand -base64 32)
JWT_KEY=SuperSecretProductionJwtSigningKey2026_AtLeast32CharsLong!

# Object storage (MinIO) credentials — the API uses these too
MINIO_USER=recruitops-storage
MINIO_PASSWORD=Use_Another_Strong_Random_Password_2026!

# First Administrator — used only while the database has no users; 12+ character password.
# Remove both once you have signed in.
SEED_ADMIN_EMAIL=hr.admin@company.com
SEED_ADMIN_PASSWORD=Use_A_Long_One_Time_Password_2026!

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
2. **Issue the certificate before the stack starts** (port 80 must be free), and copy it to
   `TLS_CERT_DIR` as `fullchain.pem` + `privkey.pem`:
   ```bash
   sudo apt install -y certbot
   sudo certbot certonly --standalone -d company.recruitops.com
   sudo install -d -m 700 /etc/recruitops/tls
   sudo cp -L /etc/letsencrypt/live/company.recruitops.com/{fullchain,privkey}.pem /etc/recruitops/tls/
   ```
   ⚠️ Not `certbot --nginx` and not `python3-certbot-nginx`: that installs a second nginx on the
   host, which fights the container for port 80. The container *is* the web server (ADR-0029).
3. **Renewals** run through the container once it is up — the exact `certbot renew --webroot`
   command with its deploy hook is in [deployment-runbook.md](deployment-runbook.md), section 0b.

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
