#!/usr/bin/env bash
# Backs up a production install: the database AND the CVs, into one dated directory.
#
#   cd /path/to/recruitops        # where docker-compose.prod.yml and .env are
#   scripts/backup.sh [/var/backups/recruitops]
#
# Database first, then CVs, on purpose: nothing in the product deletes a stored file (a CV is only
# ever added), so every file the dump refers to already exists when the copy runs. Taken the other
# way round, a CV uploaded in between would be referenced by the dump and missing from the copy.
# If a later feature deletes stored files, revisit this ordering.
#
# CVs are copied as objects (`mc mirror`), not as MinIO's volume: object copies restore into any
# MinIO version — or R2 — while a volume tarball needs the same server version and a stopped server.
set -euo pipefail

compose=(docker compose -f docker-compose.prod.yml)
[ -f docker-compose.prod.yml ] && [ -f .env ] || { echo "run this from the install directory (docker-compose.prod.yml and .env)" >&2; exit 1; }
# Credentials are read inside the containers, from the environment Compose gave them — never by
# sourcing .env here, whose syntax is Compose's, not the shell's (a `$` or a space in a password
# would be mangled, or run).

dest="${1:-/var/backups/recruitops}/$(date -u +%Y%m%d_%H%M%SZ)"
mkdir -p "$dest"
chmod 700 "$dest"   # CVs and every user's data: not for other accounts on this host

echo "1/2 database -> $dest/db.dump"
"${compose[@]}" exec -T db sh -c 'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" -F c' > "$dest/db.dump.partial"
mv "$dest/db.dump.partial" "$dest/db.dump"

echo "2/2 CVs -> $dest/cvs/"
# The root account reads the bucket; the API's account is for the API. Runs as root in a throwaway
# container so it can write into $dest, which belongs to whoever runs this script.
"${compose[@]}" run --rm --no-deps --user 0:0 -v "$dest:/backup" --entrypoint /bin/sh create-buckets -c '
  set -eu
  mc alias set src http://storage:9000 "$MINIO_USER" "$MINIO_PASSWORD" > /dev/null
  mc mirror --quiet src/recruitops-cvs /backup/cvs
  chown -R "'"$(id -u):$(id -g)"'" /backup/cvs'

files=$(find "$dest/cvs" -type f | wc -l)
echo "Done: $dest — database $(du -h "$dest/db.dump" | cut -f1), $files CV file(s)."
echo "Keep a copy off this server: a backup on the same disk as the install is not a backup."
