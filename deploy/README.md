# Deploy

Terraform builds the AWS infrastructure. Helm deploys the app to it. The chart is the same for local and prod.

## Local

Run `deploy/local/up.sh`. The script creates a kind cluster, installs the ingress controller and the Keycloak operator, builds the images, and installs the chart with `values-local.yaml`.

## Terraform

The configuration in `terraform/` creates a VPC, an EKS cluster, two ECR repositories (api and web), and an RDS PostgreSQL instance. Before the first apply, choose a remote state backend and add it to `terraform/versions.tf`. You must also set `admin_cidrs`, the address ranges that can reach the Kubernetes API.

```
cd terraform
terraform init
terraform plan -var 'admin_cidrs=["203.0.113.4/32"]'
terraform apply -var 'admin_cidrs=["203.0.113.4/32"]'
```

## Helm on AWS

1. Connect to the cluster: `aws eks update-kubeconfig --name frongle`.
2. Install the Keycloak operator. Use the commands in `local/up.sh` under "Installing the Keycloak operator".
3. Install an ingress controller (for example ingress-nginx).
4. Copy the RDS password into a Kubernetes secret with the keys `username` and `password`. The Terraform output `database_secret_arn` names the secret in AWS Secrets Manager.
5. Push the api and web images to ECR.
6. Install the chart:

```
helm upgrade --install frongle helm/frongle --namespace frongle \
  --set publicUrl=https://frongle.example.com \
  --set database.host=<database_host output> \
  --set database.existingSecret=<secret name> \
  --set api.image.repository=<ecr-api-url> --set api.image.tag=<tag> \
  --set web.image.repository=<ecr-web-url> --set web.image.tag=<tag> \
  --set ingress.host=frongle.example.com
```

The chart creates the `keycloak` database on RDS with a hook job. Keycloak runs with two replicas. The realm has no users in prod, so you create them in the Keycloak admin console.

## Check without AWS

```
terraform init -backend=false && terraform validate
helm lint helm/frongle -f helm/frongle/values-local.yaml
```
