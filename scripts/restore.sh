#!/usr/bin/env bash
# Restores a backup taken by scripts/backup.sh: the database AND the CVs.
#
#   cd /path/to/recruitops
#   scripts/restore.sh /var/backups/recruitops/20261021_093000Z
#
# ⚠️ Replaces the current database with the backup's. The API is stopped while it runs.
set -euo pipefail

src="${1:?usage: scripts/restore.sh <backup directory>}"
[ -f "$src/db.dump" ] && [ -d "$src/cvs" ] || { echo "$src is not a backup made by backup.sh (needs db.dump and cvs/)" >&2; exit 1; }
compose=(docker compose -f docker-compose.prod.yml)
[ -f docker-compose.prod.yml ] && [ -f .env ] || { echo "run this from the install directory (docker-compose.prod.yml and .env)" >&2; exit 1; }
# Credentials are read inside the containers, from the environment Compose gave them — never by
# sourcing .env here, whose syntax is Compose's, not the shell's (a `$` or a space in a password
# would be mangled, or run).
src="$(cd "$src" && pwd)"

echo "Stopping the API so nothing writes while the database is replaced"
"${compose[@]}" stop backend

echo "1/2 database <- $src/db.dump"
"${compose[@]}" exec -T db sh -c '
  psql -U "$POSTGRES_USER" -d postgres -qc "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '"'"'$POSTGRES_DB'"'"' AND pid <> pg_backend_pid();" > /dev/null
  pg_restore -U "$POSTGRES_USER" -d "$POSTGRES_DB" --clean --if-exists --no-owner --no-acl' < "$src/db.dump"

echo "2/2 CVs <- $src/cvs/"
# create-buckets makes the bucket private and the API's account valid on every start; run it first
# so a restore onto a fresh install has somewhere to put the files.
"${compose[@]}" up -d --no-build create-buckets > /dev/null
"${compose[@]}" wait create-buckets > /dev/null
"${compose[@]}" run --rm --no-deps --user 0:0 -v "$src:/backup:ro" --entrypoint /bin/sh create-buckets -c '
  set -eu
  mc alias set dst http://storage:9000 "$MINIO_USER" "$MINIO_PASSWORD" > /dev/null
  mc mirror --quiet --overwrite /backup/cvs dst/recruitops-cvs'

echo "Starting the API"
"${compose[@]}" up -d --no-build backend > /dev/null
echo "Restored from $src. Sign in and open a candidate's CV to check."
