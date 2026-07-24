#!/usr/bin/env bash
# PreToolUse hook: blocks Write/Edit/MultiEdit on protected paths.
# Reads the PreToolUse JSON payload from stdin. Exit 2 blocks the tool call
# and sends stderr back to Claude as the reason.
set -euo pipefail

INPUT=$(cat)
FILE=$(echo "$INPUT" | jq -r '.tool_input.file_path // empty')
[ -z "$FILE" ] && exit 0

PROTECTED_PATTERNS=(
  "appsettings.Production.json"
  ".env.production"
  "infra/secrets/"
)

for pattern in "${PROTECTED_PATTERNS[@]}"; do
  if [[ "$FILE" == *"$pattern"* ]]; then
    echo "Blocked: '$FILE' matches protected path '$pattern'. Edit this manually if the change is intentional." >&2
    exit 2
  fi
done

exit 0
