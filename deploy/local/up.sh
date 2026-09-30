#!/usr/bin/env bash
# Creates a local kind cluster and deploys Frongle to it. Safe to run again.
set -euo pipefail

CLUSTER=frongle
NAMESPACE=frongle
KEYCLOAK_VERSION=26.7.5
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"

if ! kind get clusters | grep -qx "$CLUSTER"; then
  kind create cluster --name "$CLUSTER" --config "$ROOT/deploy/local/kind-config.yaml"
fi
kubectl config use-context "kind-$CLUSTER" >/dev/null

echo "Installing the ingress controller"
kubectl apply -f https://kind.sigs.k8s.io/examples/ingress/deploy-ingress-nginx.yaml
kubectl -n ingress-nginx rollout status deployment/ingress-nginx-controller --timeout=180s

echo "Installing the Keycloak operator $KEYCLOAK_VERSION"
kubectl create namespace "$NAMESPACE" --dry-run=client -o yaml | kubectl apply -f -
BASE="https://raw.githubusercontent.com/keycloak/keycloak-k8s-resources/$KEYCLOAK_VERSION/kubernetes"
for crd in keycloaks keycloakrealmimports keycloakoidcclients keycloaksamlclients; do
  kubectl apply -f "$BASE/$crd.k8s.keycloak.org-v1.yml"
done
kubectl -n "$NAMESPACE" apply -f "$BASE/kubernetes.yml"

echo "Building and loading images"
docker build -t frongle/api:local "$ROOT/api"
docker build -t frongle/web:local "$ROOT/web"
kind load docker-image frongle/api:local frongle/web:local --name "$CLUSTER"

echo "Deploying the Helm chart"
helm upgrade --install frongle "$ROOT/deploy/helm/frongle" \
  --namespace "$NAMESPACE" \
  -f "$ROOT/deploy/helm/frongle/values-local.yaml"

echo "Open http://localhost (sign in as manager@acme.test with password 'password')."
