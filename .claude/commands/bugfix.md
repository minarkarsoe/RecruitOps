---
allowed-tools: Read, Edit, Grep, Glob, Bash
argument-hint: [bug description or issue reference]
description: Investigate and fix a reported bug
---

Investigate and fix: $ARGUMENTS

1. Reproduce or trace the bug: search the relevant code paths (Grep/Glob), and run any existing repro steps or failing test.
2. Identify the root cause — don't just patch the symptom. State the root cause before fixing it.
3. Write a regression test that fails before the fix and passes after, if the codebase has test coverage for that area.
4. Apply the minimal fix that addresses the root cause.
5. Run the full relevant test suite (backend and/or frontend) to check for regressions elsewhere.
6. Summarize the root cause, the fix, and the regression test added.
