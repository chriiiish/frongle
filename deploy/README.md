# Deploy

Terraform builds the AWS infrastructure. Helm deploys the app to it. Local work uses Docker Compose, not this chart.

## Local

Run `docker compose up --build` in the top-level folder. `compose.yaml` starts Postgres, Keycloak, the API, the web app, and an nginx gateway that plays the part of the ingress. The files that the stack needs are in `deploy/local/`.

## Terraform

The configuration in `terraform/` creates a VPC, an EKS cluster, two ECR repositories (api and web), and an RDS PostgreSQL instance. The workflow `.github/workflows/terraform.yml` plans on pull requests and applies on a merge to `main`. The state lives in an S3 bucket.

The workflow reads these values from the `production` environment in GitHub:

| Name                    | Kind     | Use                                                             |
| ----------------------- | -------- | --------------------------------------------------------------- |
| `AWS_ACCESS_KEY_ID`     | secret   | AWS login                                                       |
| `AWS_SECRET_ACCESS_KEY` | secret   | AWS login                                                       |
| `TF_STATE_BUCKET`       | variable | S3 bucket for the Terraform state                               |
| `ADMIN_CIDRS`           | variable | JSON list of ranges that can reach the Kubernetes API           |
| `ACME_EMAIL`            | variable | Contact address that Let's Encrypt uses for certificate notices |

## Release to AWS

The workflow `.github/workflows/release.yml` runs on every push to `main`. It builds the images, pushes them to ECR with the commit SHA as the tag, and deploys the chart to EKS. The site is then at https://frongle.cjl.nz/ with the web app at `/`, the API at `/api`, and Keycloak at `/auth`.

The workflow does these steps in order:

1. Add the runner address to the Kubernetes API allow list, then remove it at the end.
2. Install ingress-nginx behind a Network Load Balancer.
3. Point `frongle.cjl.nz` at the load balancer with a Route 53 CNAME record in the `cjl.nz` hosted zone.
4. Install cert-manager. The chart creates a Let's Encrypt `ClusterIssuer` and the ingress asks for a certificate.
5. Install the Keycloak operator and copy the RDS login into the `frongle-db` secret. The `frongle-app-db` secret holds the login of the restricted database role that serves requests.
6. Run `helm upgrade --install` with `values-production.yaml`.
7. Make sure that the site answers over HTTPS.

The chart creates the `keycloak` database on RDS with a hook job. Keycloak runs with two replicas. The realm has no users in prod, so you create them in the Keycloak admin console. The operator stores the first admin login in the secret `frongle-keycloak-initial-admin` in the `frongle` namespace.

## Check without AWS

```
terraform init -backend=false && terraform validate
helm lint helm/frongle --set database.host=example --set database.existingSecret=example --set database.appExistingSecret=example --set storage.bucket=example
helm/frongle/tests/tls.sh
```
