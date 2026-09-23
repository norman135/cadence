#!/usr/bin/env bash
# Smoke-tests a running Cadence deployment end to end: proxy, API, SPA hosting and caching.
#
#   perf/smoke-test.sh https://localhost
#
# -k is used because local and CI stacks use Caddy's self-signed certificate for "localhost".
set -euo pipefail

base="${1:-https://localhost}"
http_base="${base/https:/http:}"
failures=0

check() {
  local description="$1"
  shift
  if "$@" > /dev/null 2>&1; then
    printf '  ok    %s\n' "$description"
  else
    printf '  FAIL  %s\n' "$description"
    failures=$((failures + 1))
  fi
}

body() { curl -sk "$@"; }
header() { curl -sk -o /dev/null -D - "${@:2}" | tr -d '\r' | grep -i "^$1:" | cut -d' ' -f2-; }

index_html="$(body "$base/")"
asset_path="$(grep -oE '/assets/index-[^"]+\.js' <<< "$index_html" | head -1)"

echo "Smoke testing $base"

check "liveness reports Healthy" \
  test "$(body "$base/health/live")" = "Healthy"
check "readiness (database) reports Healthy" \
  test "$(body "$base/health/ready")" = "Healthy"
check "GET /api/v1/system/info returns Cadence" \
  grep -q '"name":"Cadence"' <<< "$(body "$base/api/v1/system/info")"
check "unknown API routes return 404 problem details" \
  test "$(curl -sk -o /dev/null -w '%{http_code} %{content_type}' "$base/api/v1/does-not-exist")" = "404 application/problem+json"
check "/ serves the SPA" \
  grep -q 'id="root"' <<< "$index_html"
check "client-side routes fall back to the SPA" \
  grep -q 'id="root"' <<< "$(body "$base/projects/CAD/board")"
check "index.html is always revalidated" \
  grep -q 'no-cache' <<< "$(header cache-control "$base/")"
check "hashed assets are cached immutably" \
  grep -q 'immutable' <<< "$(header cache-control "$base$asset_path")"
check "assets are served Brotli-compressed" \
  test "$(header content-encoding "$base$asset_path" -H 'Accept-Encoding: br, gzip')" = "br"
check "HTTP redirects to HTTPS" \
  test "$(curl -s -o /dev/null -w '%{http_code}' "$http_base/")" = "308"
check "no Server header is exposed" \
  test -z "$(header server "$base/")"

if ((failures > 0)); then
  echo "$failures smoke check(s) failed."
  exit 1
fi
echo "All smoke checks passed."
