<p align="center">
  <img src="web/public/logo.svg" alt="Frongle logo" width="96" height="96" />
</p>

<h1 align="center">Frongle</h1>

Frongle tracks assets such as light-posts, street signs, telephone poles, and traffic lights. It also tracks the maintenance and replacement of those assets.

Maintenance managers see asset status, set maintenance schedules, and assign work to teams. Work teams install, repair, remove, and replace assets. Many organizations (tenants) share one installation, and each tenant sees only its own data.

## Folders

- `web/`: React and TypeScript front end. The interface is mobile-first.
- `api/`: C# .NET 10 API with PostgreSQL (EF Core).
- `deploy/`: Terraform for AWS (VPC, EKS, ECR, RDS), a Helm chart, and scripts for a local Kubernetes cluster.

## Get started locally

### Tools to install

| Tool                                               | Version    | Used for                                |
| -------------------------------------------------- | ---------- | --------------------------------------- |
| [Docker Desktop](https://www.docker.com/)          | recent     | Builds images and runs the kind cluster |
| [kind](https://kind.sigs.k8s.io/)                  | recent     | Local Kubernetes cluster                |
| [kubectl](https://kubernetes.io/docs/tasks/tools/) | recent     | Talks to the cluster                    |
| [Helm](https://helm.sh/)                           | 3 or later | Installs the Frongle chart              |
| [Node.js](https://nodejs.org/)                     | 24         | Web app, tests, and Prettier            |
| [.NET SDK](https://dotnet.microsoft.com/)          | 10         | API and API tests                       |

Optional: [Terraform](https://www.terraform.io/) (to check `deploy/terraform`) and [Trivy](https://trivy.dev/) (to run security scans).

On macOS with Homebrew: `brew install kind kubectl helm node dotnet` and `brew install --cask docker`.

### Steps

1. Start Docker Desktop. In its settings, give Docker at least 6 GB of memory. With less, Keycloak cannot start.
2. Make sure that port 80 is free on your machine. The cluster publishes the ingress on `http://localhost`.
3. Clone the repository and go to its folder.
4. Run `deploy/local/up.sh`. It creates the kind cluster, installs the ingress controller and the Keycloak operator, builds the API and web images, and installs the Helm chart. The first run takes 10 to 20 minutes because of image downloads.
5. Wait until all pods are ready: `kubectl -n frongle get pods`. Keycloak is the slowest, and the `frongle-realm` pod shows `Completed` when the realm import is done.
6. Open http://localhost and select Sign in.
7. Sign in as one of the demo users below. The password for all of them is `password`.

| User                  | Role                | Tenant |
| --------------------- | ------------------- | ------ |
| `manager@acme.test`   | maintenance-manager | acme   |
| `team@acme.test`      | work-team           | acme   |
| `manager@globex.test` | maintenance-manager | globex |

After you sign in, the page shows the greeting from the API with your tenant and roles.

Useful extras:

- Keycloak runs at http://localhost/auth. The admin password is in the `frongle-keycloak-initial-admin` secret in the `frongle` namespace.
- To deploy code changes, run `deploy/local/up.sh` again. It is safe to repeat.
- To remove everything, run `deploy/local/down.sh`.

### Run the tests

```
cd web && npm install && npm test
cd api && dotnet test
```

## Develop

CI runs tests, format checks, linters, `helm lint`, `terraform validate`, Trivy, CodeQL, and gitleaks. Run the format commands before you push: `npm run format` in `web/`, `dotnet format` in `api/`, and `npx prettier --write .` at the top level.

See `deploy/README.md` for deployment to AWS.

## Practices

We write the test first (TDD). We build only what we need now (YAGNI). We follow clean code principles. `CLAUDE.md` holds the full rules.
