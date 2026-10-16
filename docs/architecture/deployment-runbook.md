# RecruitOps Deployment & Operational Runbook

> **Scope**: Operations guide for per-company single-tenant installations (ADR-0004 & ADR-0015).

---

## 0. Install prerequisite — SMTP (ADR-0026)

**An install without SMTP configured cannot tell any candidate anything.** Interview
invitations, offers and reminders queue, retry to their attempt cap, and land `Failed`. Nothing
is lost and nothing pretends to have been sent — but nobody is emailed, and the first person to
notice is a candidate who did not turn up. Configure this **before** handing the system over,
not after.

```yaml
environment:
  Smtp__Host: "mail.customer.internal"
  Smtp__Port: "587"                       # STARTTLS submission
  Smtp__UseStartTls: "true"
  Smtp__Username: "recruitops"            # omit for a relay that authorises by IP
  Smtp__Password: "<from the secret store, never in a committed file>"
  Smtp__FromAddress: "recruitment@customer.com"
  Smtp__FromDisplayName: "Customer Recruitment"
```

- **Port 465 (implicit TLS) is not supported.** `System.Net.Mail` speaks STARTTLS only. A relay
  offering nothing but 465 needs a code change, not a config change — raise it before the sale.
- **Microsoft 365 and Google Workspace are not supported as SMTP endpoints.** Both require
  XOAUTH2. An internal relay or a plain-SMTP submission host is what works today.
- **Set `Companies.TimeZoneId`** (IANA, e.g. `Asia/Yangon`) for the customer's company row.
  Candidate emails render interview times in it. Left null, they go out labelled UTC — correct
  but useless to a candidate in Yangon, where the offset is +06:30.

**Verifying it, without emailing a real candidate:** point `Smtp__Host` at the customer's relay
and schedule an interview against a test candidate whose address you control. Then check the
`OutboundMessages` table — `Status = 'Sent'` means the relay accepted it. A `Failed` row carries
the server's own reason in `LastError`.

---

## 0b. Install prerequisite — a TLS certificate (ADR-0029)

**`docker compose -f docker-compose.prod.yml up` refuses to start without one** — the proxy
serves the product only on 443, and port 80 only redirects. Put the certificate in a directory as
exactly two files, and name that directory in `.env`:

```env
TLS_CERT_DIR=/etc/recruitops/tls        # holds fullchain.pem (leaf + intermediates) and privkey.pem
ACME_WEBROOT=/var/www/recruitops-acme   # only for Let's Encrypt renewals (default ./infra/acme)
```

Which certificate:

- **Let's Encrypt (most VPS installs)** — issued and renewed *through the running proxy*, so
  nothing ever needs port 80 but the container. Rehearsed end to end on 2026-10-16 against
  Pebble (Let's Encrypt's test CA): issue, renewal from the saved config alone, dry run (the dry run pointed at Pebble with `--server`: `--dry-run` always switches to Let's Encrypt's staging CA, which the sandbox could not reach — in production that is what it should use).
  1. A throwaway placeholder, so the proxy can start before the real certificate exists:
     ```bash
     sudo install -d -m 700 /etc/recruitops/tls
     sudo install -d -m 755 /var/www/recruitops-acme
     sudo openssl req -x509 -newkey rsa:2048 -nodes -days 1 -subj "/CN=placeholder" \
       -keyout /etc/recruitops/tls/privkey.pem -out /etc/recruitops/tls/fullchain.pem
     sudo chmod 600 /etc/recruitops/tls/privkey.pem
     ```
     Browsers warn on it, and ignore the HSTS header sent over it (RFC 6797 §8.1) — it pins
     nothing. Do not hand the URL out until step 3 is done.
  2. `docker compose -f docker-compose.prod.yml up -d`
  3. Issue, registering the repo's deploy hook with this certificate:
     ```bash
     sudo certbot certonly --webroot -w /var/www/recruitops-acme -d company.recruitops.com \
       --deploy-hook /opt/recruitops/infra/certbot/deploy-hook.sh
     ```
     The hook (bash, so certbot's `/bin/sh` cannot mangle it) copies the certificate into
     `TLS_CERT_DIR` — key `600`, atomically — then runs `nginx -t` and reloads. It finds the
     install from its own path, so it works wherever the repo is cloned. Certbot saves
     `authenticator = webroot` and the hook in `/etc/letsencrypt/renewal/<name>.conf`, so the
     packaged timer's plain `certbot renew` uses both, and the hook fires for this certificate
     only.
  4. **Prove renewal works — mandatory:** `sudo certbot renew --dry-run`. Then check what is
     served: `echo | openssl s_client -connect localhost:443 -servername company.recruitops.com
     2>/dev/null | openssl x509 -noout -issuer -enddate`.

  ⚠️ Do **not** use `certbot --nginx` (it installs a host nginx that fights the container for
  port 80) or `--standalone` (it needs port 80 free, so every renewal would fail while the stack
  runs). An earlier version of this section did both, with an inline hook whose `{a,b}` braces
  `/bin/sh` (dash) leaves literal — renewals would have failed silently (security review,
  2026-10-16).
- **Behind Cloudflare** ("Full (strict)", or a Tunnel): a Cloudflare **Origin certificate**. See
  `cloudflare-integration-guide.md`. Install it as below.
- **The customer's own CA** (on premise): their certificate (with intermediates) and key:
  ```bash
  sudo install -m 644 -o root -g root chain.pem /etc/recruitops/tls/fullchain.pem
  sudo install -m 600 -o root -g root key.pem   /etc/recruitops/tls/privkey.pem
  docker compose -f docker-compose.prod.yml exec -T proxy sh -c 'nginx -t && nginx -s reload'
  ```
  Renewal is then the customer's process; diary the expiry date.

Keep the key root-only (`700` directory, `600` key): nginx's master process reads it as root at
startup, so it never needs to be world-readable (checked 2026-10-16). A missing file stops nginx
at startup with the path in the message — `docker compose -f docker-compose.prod.yml logs proxy`.

**HSTS is on** (one year): once a browser has visited, it will refuse plain HTTP *and* refuse to
let a user click past a certificate warning for that host. Do not deploy with a self-signed or
expired certificate.

> **Upgrading an install from before 2026-10-16:** add the certificate first. The first `up`
> also re-owns the storage volume for the new MinIO image (`storage-init`) and makes the CV
> bucket private (`create-buckets`) — `docker compose logs storage-init create-buckets` should
> end with `recruitops-cvs is private`. If that job fails, the API does not start, by design.
> The job removes every anonymous rule, including ones scoped to a folder, which overrides any
> public access someone set on purpose — there is none the product needs. It also refuses the
> published `minioadmin` credentials. It runs on `up`, not on `restart`: after changing storage
> policy by hand, run `up -d` again.

> **After the first sign-in,** remove `SEED_ADMIN_EMAIL` and `SEED_ADMIN_PASSWORD` from `.env` and
> run `docker compose -f docker-compose.prod.yml up -d` — the container keeps the values in its
> environment until it is recreated, and if the database were ever emptied while they were still
> set, a new Administrator with that password would be created on the next start.

> **Editing `infra/nginx/nginx.conf`:** apply it with `docker compose restart proxy`, not
> `nginx -s reload`. It is bind-mounted as a single file, and once an editor has replaced the
> file the container keeps reading the old one (measured 2026-10-16).

---

## 1. Automated Database Migrations

Automated, idempotent EF Core database migrations execute on backend application startup via `AppDbContext.Database.Migrate()`.

### On-Premise Startup Flow:
1. When the container boots, `Program.cs` checks PostgreSQL connection.
2. EF Core applies any outstanding schema migrations (`20260728023109_InitialCreate` through `20260812141548_AddCvIngestionAndAiProfileFields`).
3. Seed data (`DbInitializer`, `RbacSeedData`) initializes required default roles, 34 canonical permissions, and system tenant entries.

---

## 2. Database Backup & Restore Runbook

PostgreSQL database backup and restore operations must be executed directly against the `db` container.

### 2.1 Performing a Database Backup
```bash
# Export timestamped dump of the company database
docker compose exec -T db pg_dump -U postgres -d recruitops -F c -b -v -f /tmp/backup_$(date +%Y%m%d_%H%M%S).dump

# Copy backup file to host backup directory
docker cp $(docker compose ps -q db):/tmp/backup_*.dump /var/backups/recruitops/
```

### 2.2 Restoring a Database Backup
```bash
# Copy dump file into container
docker cp /var/backups/recruitops/backup_TARGET.dump $(docker compose ps -q db):/tmp/restore.dump

# Terminate existing connections and restore schema/data
docker compose exec db psql -U postgres -d recruitops -c "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = 'recruitops' AND pid <> pg_backend_pid();"
docker compose exec db pg_restore -U postgres -d recruitops --clean --if-exists --no-owner --no-acl /tmp/restore.dump
```

---

## 3. Customer Upgrade Runbook

### Support Policy
- Supported versions: **Latest (`v1.x`)** and **Latest-1 (`v1.x-1`)**.
- Deployments older than `Latest-1` must perform sequential upgrades.

### Upgrade Procedure
1. Inspect deployment version: `curl https://<customer>.recruitops.com/api/version`.
2. Take a database dump using Section 2.1. Upgrading from before 2026-10-16? Section 0b first —
   the new compose file needs a certificate.
3. Pull new release images: `docker compose -f docker-compose.prod.yml pull`.
4. Rebuild **with `--pull`**, then restart:
   ```bash
   docker compose -f docker-compose.prod.yml build --pull
   docker compose -f docker-compose.prod.yml up -d
   ```
   `--pull` is what refreshes the .NET and Node base images. `up -d --build` alone reuses the
   cached `aspnet:10.0` from the install's first build — and with it that day's runtime, whose
   shared-framework copies of libraries such as `System.Security.Cryptography.Xml` are what the
   API actually loads. Raising a package version in the repo does not patch those; a newer base
   image does (2026-10-16: the advisories fixed in 10.0.13 needed both).
5. Verify health: `curl https://<customer>.recruitops.com/health`.

---

## 4. Feature Flag Add-on Gating (ADR-0007)

Feature add-ons are toggled per customer via environment variables in `docker-compose.prod.yml`:

```yaml
environment:
  FeatureFlags__EnableAiProfiling: "true"
  FeatureFlags__EnableAnalytics: "true"
  FeatureFlags__EnableBulkCvUpload: "true"
  FeatureFlags__EnableSmartMatch: "true"
  FeatureFlags__EnableFullTextSearch: "true"
```
Disabling a feature flags returns **HTTP 403 Forbidden** with `FeatureDisabled` payload on gated endpoints and hides frontend UI elements automatically.
