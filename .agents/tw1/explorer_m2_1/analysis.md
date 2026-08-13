# Blueprint — Milestone 2: remove the orphaned duplicate migration

**Explorer:** `explorer_m2_1` · **Filed by:** Orchestrator (subagents cannot write files here)

> ⚠️ Do **not** read `.agents/explorer_m2_1/analysis.md` (no `tw1/` in the path). That is a
> different, stale file from an Antigravity run on 2026-08-11 about frontend search DTOs. The id
> collision is why this run namespaces everything under `.agents/tw1/`.

## Verdict: safe cleanup, not a live bug

The stray file compiles into the assembly but EF never discovers it. Verified by reading, not
inferred:

- `backend/src/Infrastructure/Persistence/Migrations/20260811000000_AddPgTrgmAndSearchIndexes.cs`
  declares `namespace RecruitOps.Infrastructure.Persistence.Migrations` and carries **no attribute
  of any kind** on the class. There is no `.Designer.cs` beside it.
- The only `[Migration("20260811000000_AddPgTrgmAndSearchIndexes")]` in the repo is at
  `backend/src/Infrastructure/Migrations/20260811000000_AddPgTrgmAndSearchIndexes.Designer.cs:14-16`,
  in namespace `RecruitOps.Infrastructure.Migrations`.

C# merges `partial class` declarations only when **namespace and class name both match**. The
namespaces differ, so these are two distinct CLR types, and the stray one has no attribute. EF
derives migration ids from the attribute only — never from a file or class name — so the stray type
cannot be assigned an id, cannot be applied, and cannot collide with anything.

## The two bodies are identical

Both files are 55 lines. Every `migrationBuilder.Sql(...)` call, every numbered comment, and the
`Down()` drop order match line for line. The only differences are the namespace and the missing
`.Designer.cs`. **No union is needed** — the surviving file is already a superset.

## Canonical location

`backend/src/Infrastructure/Migrations/` is canonical, confirmed three ways:

1. It holds all six real migration pairs plus `AppDbContextModelSnapshot.cs`. The stray folder holds
   one `.cs`, no Designer, no snapshot.
2. `docs/architecture/local-development.md:93-95` says so explicitly.
3. No `MigrationsAssembly` or `MigrationsHistoryTable` override exists anywhere in `backend/`, so EF
   uses the default convention. Nothing in the documented workflow would ever produce a
   `Persistence/Migrations/` folder — this was hand- or agent-authored.

## The change

Delete exactly one file:

```
backend/src/Infrastructure/Persistence/Migrations/20260811000000_AddPgTrgmAndSearchIndexes.cs
```

Then remove the directory it leaves behind. The file is **untracked** (`git status` shows `??`), so
`git rm` does not apply — delete it from the filesystem.

- **No csproj change.** `RecruitOps.Infrastructure.csproj` is bare SDK-style with implicit globs and
  no `<Compile Remove/Include>`; removing the file is picked up automatically.
- **No `dotnet ef` invocation of any kind.** Verification here is static — file, attribute and
  namespace inspection. Agents do not run migrations against a database.
- **Do not touch `AppDbContextModelSnapshot.cs`.** Raw `migrationBuilder.Sql()` calls are not
  tracked in the model snapshot; there is nothing to regenerate.

## Nothing references it

- `backend/tests`: no match for `Migrations`, `GetPendingMigrations` or `MigrateAsync`. Tests run on
  the EF in-memory provider, which skips migrations entirely.
- `DatabaseStartup.cs` calls `MigrateAsync` against whatever the assembly scan reports — and the
  scan cannot see the stray type either way.
- `scripts/init-db.sql` runs `CREATE EXTENSION IF NOT EXISTS pg_trgm;` independently and
  idempotently; it does not reference either C# file and cannot collide.
- `docker-compose.yml` and `backend/Dockerfile`: no reference to the migration class or the stray
  namespace. *(The explorer flagged the root compose file as unverified; the Orchestrator grepped it
  directly — confirmed clean.)*

## Expected effect on the suite

**None.** No test touches migrations. If the Worker sees the test count move after this deletion,
that is a signal something else broke, not a side effect of the cleanup.

## Open Questions — resolved by the Orchestrator

1. **Safe cleanup or live bug?** Safe cleanup. Confirmed by attribute and namespace inspection.
2. **Remove the empty directory?** Yes — the file is untracked, so nothing in git handles it.
3. **Add a guard so a future agent cannot recreate a stray migration copy?** **Out of scope for M2.**
   Recorded as a follow-up. This milestone deletes a file; it does not build prevention tooling.
