---
allowed-tools: Read, Grep, Glob, Bash(git diff:*), Bash(git log:*)
description: Review the current diff for quality, security, and convention adherence
---

## Changed files
!`git diff --name-only HEAD~1`

## Diff
!`git diff HEAD~1`

Review the changes above against CLAUDE.md conventions. Cover:

1. Code quality and readability
2. Security — for anything touching auth, data access, or external input, delegate to the `security-reviewer` subagent
3. Test coverage for the new/changed behavior
4. Architecture boundary violations (Clean Architecture on the backend, Server/Client Component misuse on the frontend)

Organize feedback as 🔴 Must fix / 🟡 Should fix / 🟢 Optional, with file:line references.
