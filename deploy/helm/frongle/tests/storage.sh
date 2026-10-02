#!/usr/bin/env bash
# Checks where the API keeps Event photos: S3 through an IAM role on EKS, and MinIO in the local cluster.
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

local="$(render -f "$CHART/values-local.yaml")"
grep -A1 'name: Storage__ServiceUrl' <<<"$local" | grep -q 'value: "http://storage.localhost"' || fail "local needs the MinIO address"
grep -q 'name: frongle-minio' <<<"$local" || fail "local needs MinIO"
grep -q 'host: storage.localhost' <<<"$local" || fail "the ingress must route storage.localhost to MinIO"
grep -q '"/data/frongle-images"' <<<"$local" || fail "MinIO needs the bucket folder"
# The photos must outlive the pod, as the local database does, and the non-root MinIO user must own the volume.
grep -q 'kind: PersistentVolumeClaim' <<<"$local" || fail "MinIO needs a persistent volume claim"
grep -q 'claimName: frongle-minio' <<<"$local" || fail "MinIO must keep its data on the claim"
! grep -A3 'name: data' <<<"$local" | grep -q 'emptyDir' || fail "MinIO must not keep photos in an emptyDir"
grep -q 'fsGroup: 65532' <<<"$local" || fail "the non-root MinIO user must own the data volume"

! render -f "$CHART/values-local.yaml" --set storage.bucket= >/dev/null 2>&1 || fail "an empty bucket must be rejected"
echo "ok"
