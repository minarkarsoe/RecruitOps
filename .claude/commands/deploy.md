---
allowed-tools: Read, Bash(dotnet build:*), Bash(npm run build:*), Bash(git status:*), Bash(git log:*)
description: Run a pre-deployment readiness check (does NOT deploy anything)
---

Run a pre-deployment readiness check. This command never deploys anything — it only verifies readiness and reports findings.

1. Confirm the working tree is clean (`git status`) and the branch is up to date with `main`.
2. Run `dotnet build backend/src/Api` in Release configuration and `npm run build --prefix frontend`; report any errors or warnings.
3. Run the full test suite (backend + frontend).
4. Check for TODO/FIXME comments introduced in this branch's diff that look blocking.
5. Check for pending EF Core migrations that haven't gone through the `db-schema-reviewer` subagent.
6. Produce a go/no-go summary. If it's a no-go, explain exactly what needs to happen first. Do not run any actual deployment or infrastructure command.
