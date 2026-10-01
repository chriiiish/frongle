#!/usr/bin/env bash
# Checks that production serves HTTPS on its own domain and that local stays plain HTTP.
set -euo pipefail

CHART="$(cd "$(dirname "$0")/.." && pwd)"
PROD=(-f "$CHART/values-production.yaml" --set database.host=db.example --set ingress.tls.email=ops@example.com)

fail() { echo "FAIL: $1" >&2; exit 1; }
render() { helm template frongle "$CHART" "$@"; }

prod="$(render "${PROD[@]}")"
grep -q 'host: frongle.cjl.nz' <<<"$prod" || fail "production ingress needs the host frongle.cjl.nz"
grep -q 'secretName: frongle-tls' <<<"$prod" || fail "production ingress needs a TLS secret"
grep -q 'cert-manager.io/cluster-issuer: letsencrypt' <<<"$prod" || fail "production ingress needs the cluster issuer annotation"
grep -q 'kind: ClusterIssuer' <<<"$prod" || fail "production needs a ClusterIssuer"
grep -q 'email: ops@example.com' <<<"$prod" || fail "the ClusterIssuer needs the ACME email"
grep -q 'https://frongle.cjl.nz/auth' <<<"$prod" || fail "Keycloak needs the public HTTPS URL"

local="$(render -f "$CHART/values-local.yaml")"
! grep -q 'kind: ClusterIssuer' <<<"$local" || fail "local must not create a ClusterIssuer"
! grep -q 'secretName: frongle-tls' <<<"$local" || fail "local must not use TLS"

! render "${PROD[@]}" --set ingress.tls.email= >/dev/null 2>&1 || fail "an empty ACME email must be rejected"
echo "ok"
