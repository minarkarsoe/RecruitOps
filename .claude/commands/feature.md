---
allowed-tools: Read, Write, Edit, Grep, Glob, Bash
argument-hint: [feature description]
description: Implement a new feature end-to-end across backend and frontend
---

Implement the following feature: $ARGUMENTS

1. Read CLAUDE.md and check existing patterns in `backend/src/` and `frontend/` before writing code.
2. Plan the change: which layers does it touch (Domain/Application/Infrastructure/Api, and/or frontend routes/components)? State the plan briefly before coding.
3. Implement backend changes first if an API contract is involved, keeping Clean Architecture boundaries intact.
4. Implement frontend changes against the (possibly updated) API contract.
5. Write or update tests for the new behavior on both sides as applicable.
6. Run `dotnet test backend/tests` and `npm run test --prefix frontend`; fix failures.
7. Run `dotnet format` and `npm run lint --prefix frontend`.
8. Summarize what changed and flag any follow-ups or open questions for the human reviewer.
