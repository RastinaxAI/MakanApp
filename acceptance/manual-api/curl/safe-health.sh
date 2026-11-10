#!/usr/bin/env bash
set -euo pipefail
: "${MAKAN_BASE_URL:?Set MAKAN_BASE_URL to an authorized API origin}"
BASE="${MAKAN_BASE_URL%/}"
case "$BASE" in
  https://*) ;;
  http://localhost*|http://127.0.0.1*) ;;
  *) echo "Refusing unencrypted/non-local request by default" >&2; exit 2 ;;
esac
echo "GET /health/live"
curl --fail-with-body --silent --show-error --max-time 15 "$BASE/health/live"
echo
echo "GET /health/ready"
curl --silent --show-error --max-time 15 -i "$BASE/health/ready"
echo
echo "GET /api/v1/me without authorization (expected HTTP 401)"
curl --silent --show-error --max-time 15 -i -H 'Accept: application/json' "$BASE/api/v1/me"
echo
