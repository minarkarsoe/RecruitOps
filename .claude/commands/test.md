---
allowed-tools: Bash, Read, Edit
argument-hint: [optional scope, e.g. "backend", "frontend", "auth"]
description: Run tests and fix any failures
---

Run tests for: $ARGUMENTS (or the whole repo if no scope given).

1. Determine scope: if $ARGUMENTS mentions "backend" or a .NET project name, run `dotnet test backend/tests`; if it mentions "frontend", run `npm run test --prefix frontend`; if unclear, run both.
2. If tests fail, read the failure output, locate the root cause, and fix it. Don't adjust an assertion to match broken behavior unless the test itself was wrong.
3. Re-run to confirm the fix.
4. Report a summary: tests run, pass/fail counts, and what was changed if anything failed.
