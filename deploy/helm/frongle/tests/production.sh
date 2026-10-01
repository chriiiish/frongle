#!/usr/bin/env bash
# Checks the production settings that the two small EKS nodes and the in-cluster Keycloak address need.
set -euo pipefail

CHART="$(cd "$(dirname "$0")/.." && pwd)"
fail() { echo "FAIL: $1" >&2; exit 1; }

prod="$(helm template frongle "$CHART" -f "$CHART/values-production.yaml" --set database.host=db.example --set ingress.tls.email=ops@example.com)"

# The API reads metadata from the in-cluster http:// address, so it must not demand HTTPS there.
grep -A1 'name: Authentication__RequireHttpsMetadata' <<<"$prod" | grep -q 'value: "false"' \
  || fail "the API must not require HTTPS for the in-cluster metadata address"

# The default request of 1700Mi per Keycloak pod leaves no room for the realm import on t3.medium nodes.
grep -q 'memory: 900Mi' <<<"$prod" || fail "production needs a Keycloak memory request of 900Mi"
echo "ok"
