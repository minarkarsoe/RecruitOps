# Local Development

Everything runs in containers ([ADR-0015](../decisions/ADR-0015-containerisation.md)).
The only prerequisite is **Docker** with Compose.

## Quick start

```bash
cp .env.example .env          # then edit JWT_KEY (must be >= 32 characters)
docker compose up --build
```

| Service | URL | Notes |
|---|---|---|
| API | http://localhost:5080 | Swagger at `/swagger` in Development |
| Web | http://localhost:3000 | Next.js public app |
| Postgres | localhost:5432 | user/db from `.env` |
| MinIO | http://localhost:9000 (console :9001) | S3-compatible storage |

## Database migrations

Migrations are applied **automatically on startup** (ADR-0004 — installs run unattended on
customer servers). This is a no-op on the in-memory provider used by tests, and can be
disabled with `Database:AutoMigrateOnStartup=false`.

### Creating a migration (no local .NET SDK needed)

> ⚠️ **Clean `obj/` and `bin/` first if you have ever built on the host.**
> `docker run -v` *mounts* the folder, so unlike `docker build` the `.dockerignore` does
> not apply — the container sees host build artifacts. A Windows `obj/` records absolute
> paths such as `C:\Program Files (x86)\...\NuGetPackages`, which the Linux container
> cannot resolve, and the run fails with
> `MSB4018 ... Unable to find fallback package folder`.
>
> ```powershell
> Get-ChildItem -Path .\backend -Include obj,bin -Recurse -Directory | Remove-Item -Recurse -Force
> ```
> ```bash
> find backend -type d \( -name obj -o -name bin \) -exec rm -rf {} +
> ```
>
> Safe to delete — both are generated output and are git-ignored. The same applies to any
> `docker run -v` against this repo, not just migrations.

### If you already have the .NET 10 SDK installed

`dotnet ef` is **not** part of the SDK — it is a separate global tool, so a clean machine
answers `Could not execute because the specified command or file was not found`. Install it
once:

```powershell
dotnet tool install --global dotnet-ef
# already installed but older than the target framework:
dotnet tool update --global dotnet-ef
```

Then, **from `backend/`** — not `backend/src/`, the paths below are relative to the folder
holding `RecruitOps.sln`:

```powershell
dotnet ef migrations add <Name> --project src/Infrastructure --startup-project src/Api
```

If the shell still can't find `dotnet-ef` right after installing, open a new terminal —
the installer adds `%USERPROFILE%\.dotnet\tools` to `PATH`, and an existing session won't
have picked that up.

### Without a local SDK (Docker)

**Linux / macOS (bash):**

```bash
docker run --rm -v "$(pwd)/backend:/src" -w /src mcr.microsoft.com/dotnet/sdk:10.0 bash -lc \
  "dotnet tool install -g dotnet-ef >/dev/null 2>&1; \
   dotnet restore RecruitOps.sln && \
   /root/.dotnet/tools/dotnet-ef migrations add InitialCreate \
     --project src/Infrastructure --startup-project src/Api"
```

**Windows (PowerShell)** — note this is a *single line*:

```powershell
docker run --rm -v "${PWD}\backend:/src" -w /src mcr.microsoft.com/dotnet/sdk:10.0 bash -lc "dotnet tool install -g dotnet-ef >/dev/null 2>&1; dotnet restore RecruitOps.sln && /root/.dotnet/tools/dotnet-ef migrations add InitialCreate --project src/Infrastructure --startup-project src/Api"
```

⚠️ Do **not** write `\$PATH` in PowerShell — backslash is not an escape character there, and
PowerShell parses `$PATH:` as a drive-qualified variable and fails. Call the tool by its
full path (`/root/.dotnet/tools/dotnet-ef`) instead of extending `PATH`.

`dotnet restore` is required because cleaning `obj/` (above) removes `project.assets.json`,
and the EF tooling needs it before it can read project metadata.

`AppDbContextFactory` (design-time factory) means this does **not** boot the API or connect
to a database — it only scaffolds. The migration lands in
`backend/src/Infrastructure/Migrations/`.

### Inspecting before applying

```bash
# generate the SQL without touching a database
dotnet ef migrations script --project src/Infrastructure --startup-project src/Api
```

Per the guardrail in `CLAUDE.md`, migrations are **proposed, never auto-applied by an agent**
to anything but a disposable local database. Review the generated SQL — and run the
`db-schema-reviewer` subagent on it — before it reaches a shared environment.

## Compile and test without installing the .NET SDK

```bash
docker build --target test -t recruitops-test ./backend
```

This restores, builds and runs the whole test suite inside the SDK image. **The backend has
never been successfully compiled**, so this is currently the fastest way to get a first
real build — expect errors, and fix them here.

Backend build only (no tests):

```bash
docker build -t recruitops-api ./backend
```

## Working on one part at a time

Full-stack containers are convenient but slow to iterate on the frontend. A common split:

```bash
# infrastructure only
docker compose up db storage

# then run the apps on the host against it
dotnet run --project backend/src/Api
npm run dev --prefix frontend
```

When the API runs on the host, point it at `Host=localhost` rather than `Host=db`.

## Environment variables

| Variable | Required | Purpose |
|---|---|---|
| `JWT_KEY` | **yes** | Token signing key, ≥32 chars. Compose fails fast if unset. |
| `POSTGRES_*` | no (defaults) | Database name/credentials |
| `SEED_ADMIN_EMAIL` / `SEED_ADMIN_PASSWORD` | no | Set both to seed one company + admin on first start |
| `API_INTERNAL_URL` | set by compose | Server-side fetch target for Next.js (`http://api:8080/api`) |
| `MINIO_*` | no (defaults) | Local S3 credentials |

**Never commit `.env`** — it is git-ignored. Production uses a secret store, not this file.

### Why there are two API URLs

Next.js Server Components run in Node and have no page origin, so they need an **absolute**
URL (`API_INTERNAL_URL` → `http://api:8080/api` inside the compose network). The browser
uses the relative `/api` path via the rewrite in `next.config.mjs`. Getting this wrong is
what caused a blank page earlier — see the CHANGELOG entry for the `/clients` fix.

## Common tasks

```bash
docker compose logs -f api          # follow API logs
docker compose down                 # stop
docker compose down -v              # stop and wipe database + storage volumes
docker compose build --no-cache api # rebuild after dependency changes
```

## Team conventions

- Config changes go in `.env.example` too, so the next person gets them.
- Never add a per-customer image variant — configuration only
  ([ADR-0007](../decisions/ADR-0007-productization-and-addons.md)).
- Pin image versions in `docker-compose.yml`; don't use `latest` for Postgres.
- Read [`docs/README.md`](../README.md) and
  [`status/FEATURE-STATUS.md`](../status/FEATURE-STATUS.md) before starting a task.
