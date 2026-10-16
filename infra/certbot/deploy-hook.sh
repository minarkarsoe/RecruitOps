#!/bin/bash
# Certbot deploy hook: hands a newly issued or renewed certificate to the proxy (ADR-0029).
#
# Registered once, with the certificate it serves (certbot saves it in that lineage's renewal
# config, so it never fires for any other certificate on the host):
#   sudo certbot certonly --webroot -w "$ACME_WEBROOT" -d company.example.com \
#     --deploy-hook /opt/recruitops/infra/certbot/deploy-hook.sh
# Full procedure: docs/architecture/deployment-runbook.md, section 0b.
#
# Bash, not sh: certbot runs hooks through /bin/sh, which is dash on Debian and Ubuntu. The
# runbook's first version was an inline one-liner using {a,b} brace expansion, which dash leaves
# literal — the copy failed and every renewal would have left the old certificate in place.
set -euo pipefail

# The install is two levels above this file (<install>/infra/certbot/), wherever it was cloned.
script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
RECRUITOPS_DIR="${RECRUITOPS_DIR:-$(cd "$script_dir/../.." && pwd)}"
: "${RENEWED_LINEAGE:?certbot sets this; run me as a --deploy-hook}"

compose=(docker compose -f "$RECRUITOPS_DIR/docker-compose.prod.yml")

# Where the certificate goes: whatever directory is mounted into the running proxy at
# /etc/nginx/certs — the only path that matters, asked of Docker rather than parsed out of .env
# (comments, quotes, `export` and CRLF all broke a .env parser; ADR-0029 re-review, N2).
proxy_id="$("${compose[@]}" ps -q proxy)"
if [[ -z "$proxy_id" ]]; then
  echo "deploy-hook: the proxy container is not running" >&2
  exit 1
fi
tls_dir="$(docker inspect -f '{{range .Mounts}}{{if eq .Destination "/etc/nginx/certs"}}{{.Source}}{{end}}{{end}}' "$proxy_id")"
if [[ -z "$tls_dir" || ! -d "$tls_dir" ]]; then
  echo "deploy-hook: no certificate directory is mounted at /etc/nginx/certs in the proxy" >&2
  exit 1
fi

# Copy under temporary names, keep the current pair, then rename into place: nginx is never
# offered half a file, and the key is never readable by anyone but root, even for a moment.
# `install` reads through the symlinks certbot keeps in live/ (they point into archive/, which
# the container cannot see).
install -m 644 -o root -g root "$RENEWED_LINEAGE/fullchain.pem" "$tls_dir/.fullchain.pem.new"
install -m 600 -o root -g root "$RENEWED_LINEAGE/privkey.pem" "$tls_dir/.privkey.pem.new"
for f in fullchain.pem privkey.pem; do
  if [[ -e "$tls_dir/$f" ]]; then cp -p "$tls_dir/$f" "$tls_dir/.$f.prev"; fi
done
mv -f "$tls_dir/.fullchain.pem.new" "$tls_dir/fullchain.pem"
mv -f "$tls_dir/.privkey.pem.new" "$tls_dir/privkey.pem"

# Test before reloading. If the new pair does not load, put the old one back: otherwise nginx keeps
# running on the old certificate in memory, but the next container restart would not start at all.
if ! "${compose[@]}" exec -T proxy nginx -t -q; then
  for f in fullchain.pem privkey.pem; do
    if [[ -e "$tls_dir/.$f.prev" ]]; then mv -f "$tls_dir/.$f.prev" "$tls_dir/$f"; fi
  done
  echo "deploy-hook: nginx rejected the new certificate; the previous one is back in place" >&2
  exit 1
fi
rm -f "$tls_dir/.fullchain.pem.prev" "$tls_dir/.privkey.pem.prev"
# The reload's own "[notice] signal process started" goes to stdout — on stderr, certbot logs every
# successful renewal as a WARNING.
"${compose[@]}" exec -T proxy nginx -s reload 2>&1
echo "deploy-hook: installed $(basename "$RENEWED_LINEAGE") into $tls_dir and reloaded the proxy"
