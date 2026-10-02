#!/usr/bin/env bash
# Checks where the API keeps Event photos: S3 through an IAM role on EKS.
set -euo pipefail

CHART="$(cd "$(dirname "$0")/.." && pwd)"
fail() { echo "FAIL: $1" >&2; exit 1; }
render() { helm template frongle "$CHART" "$@"; }

prod="$(render -f "$CHART/values-production.yaml" --set database.host=db.example --set ingress.tls.email=ops@example.com \
  --set storage.bucket=frongle-images-1 --set api.roleArn=arn:aws:iam::123456789012:role/frongle-api)"
grep -A1 'name: Storage__Bucket' <<<"$prod" | grep -q 'value: "frongle-images-1"' || fail "the API needs the bucket name"
grep -q 'eks.amazonaws.com/role-arn: "arn:aws:iam::123456789012:role/frongle-api"' <<<"$prod" || fail "the API service account needs the IAM role"
grep -q 'serviceAccountName: frongle-api' <<<"$prod" || fail "the API pod must use its service account"
! grep -q 'name: Storage__ServiceUrl' <<<"$prod" || fail "production must use Amazon S3, not a custom server"
! grep -q 'frongle-minio' <<<"$prod" || fail "production must not run MinIO"

! render -f "$CHART/values-production.yaml" --set database.host=db.example --set ingress.tls.email=ops@example.com --set storage.bucket= >/dev/null 2>&1 || fail "an empty bucket must be rejected"
echo "ok"
