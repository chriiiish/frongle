#!/usr/bin/env bash
# Sets the CIDR ranges that can reach the EKS public API endpoint and waits until EKS applies them.
# "aws eks wait cluster-active" is not enough, because the cluster can still show ACTIVE for a moment after the update starts.
# Usage: set-api-access.sh <cluster> <comma-separated CIDRs>
set -euo pipefail

cluster="$1"
cidrs="$2"

update_id="$(aws eks update-cluster-config --name "$cluster" \
  --resources-vpc-config "publicAccessCidrs=$cidrs" --query update.id --output text)"

while true; do
  status="$(aws eks describe-update --name "$cluster" --update-id "$update_id" --query update.status --output text)"
  case "$status" in
    Successful) exit 0 ;;
    Failed | Cancelled) echo "EKS update $update_id ended with status $status" >&2; exit 1 ;;
  esac
  sleep 10
done
