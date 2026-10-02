<p align="center">
  <img src="web/public/logo.svg" alt="Frongle logo" width="96" height="96" />
</p>

<h1 align="center">Frongle</h1>

Frongle tracks assets such as light-posts, street signs, telephone poles, and traffic lights. It also tracks the maintenance and replacement of those assets.

Maintenance managers see asset status, set maintenance schedules, and assign work to teams. Work teams install, repair, remove, and replace assets. Many organizations (tenants) share one installation, and each tenant sees only its own data.

## Folders

- `web/`: React and TypeScript front end. The interface is mobile-first.
- `api/`: C# .NET 10 API with PostgreSQL (EF Core).
- `deploy/`: Terraform for AWS (VPC, EKS, ECR, RDS), a Helm chart, and the files for the local Docker Compose stack.

## Get started locally

### Tools to install

| Tool                                      | Version | Used for                                  |
| ----------------------------------------- | ------- | ----------------------------------------- |
| [Docker Desktop](https://www.docker.com/) | recent  | Builds the images and runs Docker Compose |
| [Node.js](https://nodejs.org/)            | 24      | Web app, tests, and Prettier              |
| [.NET SDK](https://dotnet.microsoft.com/) | 10      | API and API tests                         |

Optional: [Helm](https://helm.sh/) and [Terraform](https://www.terraform.io/) (to check `deploy/`) and [Trivy](https://trivy.dev/) (to run security scans).

On macOS with Homebrew: `brew install node dotnet` and `brew install --cask docker`.

### Steps

1. Start Docker Desktop. In its settings, give Docker at least 4 GB of memory. With less, Keycloak cannot start.
2. Make sure that port 80 is free on your machine. The stack publishes its gateway on `http://localhost`.
3. Clone the repository and go to its folder.
4. Run `docker compose up --build`. It builds the API and web images, then starts Postgres, Keycloak, the API, the web app, and a gateway. The first run takes a few minutes because of image downloads.
5. Wait until Keycloak prints `Running the server`. Add `-d` to the command to run the stack in the background.
6. Open http://localhost. The app sends you to the sign-in page.
7. Sign in as one of the demo users below. The password for all of them is `password`.

| User                  | Role                | Tenant |
| --------------------- | ------------------- | ------ |
| `manager@acme.test`   | maintenance-manager | acme   |
| `team@acme.test`      | work-team           | acme   |
| `manager@globex.test` | maintenance-manager | globex |

After you sign in, the page shows the greeting from the API with your tenant and roles. The menu at the top has Home and Logout.

Useful extras:

- Keycloak runs at http://localhost/auth. The admin login is `admin` with password `admin`.
- To deploy code changes, run `docker compose up --build` again. It is safe to repeat.
- To stop the stack, run `docker compose down`. Add `-v` to also delete the database.
- The demo users come from `deploy/local/realm.json`. Keycloak imports it only when the realm does not exist, so run `docker compose down -v` after you change it.

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
