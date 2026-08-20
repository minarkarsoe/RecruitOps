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
2. Take a database dump using Section 2.1.
3. Pull new release images: `docker compose -f docker-compose.prod.yml pull`.
4. Restart services with zero-downtime container replacement:
   ```bash
   docker compose -f docker-compose.prod.yml up -d --build
   ```
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
