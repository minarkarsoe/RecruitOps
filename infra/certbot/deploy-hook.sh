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

env_file="$RECRUITOPS_DIR/.env"
tls_dir="$(sed -n 's/^TLS_CERT_DIR=//p' "$env_file" | tail -n 1)"
tls_dir="${tls_dir%\"}"; tls_dir="${tls_dir#\"}"
if [[ -z "$tls_dir" || ! -d "$tls_dir" ]]; then
  echo "deploy-hook: TLS_CERT_DIR in $env_file is unset or not a directory" >&2
  exit 1
fi

# Copy under temporary names, then rename into place: nginx is never offered half a file, and the
# key is never readable by anyone but root, even for a moment. `install` reads through the
# symlinks certbot keeps in live/ (they point into archive/, which the container cannot see).
install -m 644 -o root -g root "$RENEWED_LINEAGE/fullchain.pem" "$tls_dir/.fullchain.pem.new"
install -m 600 -o root -g root "$RENEWED_LINEAGE/privkey.pem" "$tls_dir/.privkey.pem.new"
mv -f "$tls_dir/.fullchain.pem.new" "$tls_dir/fullchain.pem"
mv -f "$tls_dir/.privkey.pem.new" "$tls_dir/privkey.pem"

# Test before reloading: a reload with a broken file would leave nginx on the old certificate
# without saying so; `nginx -t` fails loudly, and certbot reports the hook's failure. The reload's
# own "[notice] signal process started" goes to stdout — on stderr, certbot logs every successful
# renewal as a WARNING.
docker compose -f "$RECRUITOPS_DIR/docker-compose.prod.yml" exec -T proxy \
  sh -c 'nginx -t -q && nginx -s reload 2>&1'
echo "deploy-hook: installed $(basename "$RENEWED_LINEAGE") into $tls_dir and reloaded the proxy"
