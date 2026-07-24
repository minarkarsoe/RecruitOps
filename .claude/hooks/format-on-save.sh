#!/usr/bin/env bash
# PostToolUse hook: auto-formats a file after Claude writes/edits it.
# Reads the PostToolUse JSON payload from stdin (Claude Code's hook protocol).
set -euo pipefail

INPUT=$(cat)
FILE=$(echo "$INPUT" | jq -r '.tool_input.file_path // empty')
[ -z "$FILE" ] && exit 0
[ -f "$FILE" ] || exit 0

case "$FILE" in
  *.cs)
    dotnet format --include "$FILE" >/dev/null 2>&1 || true
    ;;
  *.ts|*.tsx|*.js|*.jsx|*.json|*.css)
    npx --prefix frontend prettier --write "$FILE" >/dev/null 2>&1 || true
    ;;
esac

exit 0
