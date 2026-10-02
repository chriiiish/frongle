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

## Tools to install

| Tool                                      | Version | Used for                                  |
| ----------------------------------------- | ------- | ----------------------------------------- |
| [Docker Desktop](https://www.docker.com/) | recent  | Builds the images and runs Docker Compose |
| [Node.js](https://nodejs.org/)            | 24      | Web app, tests, and Prettier              |
| [.NET SDK](https://dotnet.microsoft.com/) | 10      | API and API tests                         |

Optional: [Helm](https://helm.sh/) and [Terraform](https://www.terraform.io/) (to check `deploy/`), [Trivy](https://trivy.dev/) (to run security scans), and `psql` (to reach the database from your terminal).

On macOS with Homebrew: `brew install node dotnet libpq` (then `brew link --force libpq` to get `psql`) and `brew install --cask docker`.

## Run the whole project locally

Start Docker Desktop first. Give Docker at least 4 GB of memory, or Keycloak cannot start. Make sure that port 80 is free, because the stack publishes the app on `http://localhost`.

```
docker compose up --build      # builds the images and starts everything
docker compose ps
```

The first run takes a few minutes because of image downloads. Keycloak is the slowest to start. Add `-d` to run the stack in the background. When Keycloak prints `Running the server`, open http://localhost and sign in as one of these users. The password for all of them is `password`.

| User                  | Role                | Tenant |
| --------------------- | ------------------- | ------ |
| `manager@acme.test`   | maintenance-manager | acme   |
| `team@acme.test`      | work-team           | acme   |
| `manager@globex.test` | maintenance-manager | globex |

To deploy your code changes, run `docker compose up --build` again. It is safe to repeat. To stop the stack, run `docker compose down`. To also delete the database and the photos, run:

```
docker compose down -v
```

Keycloak runs at http://localhost/auth. The admin login is `admin` with password `admin`. The demo users come from `deploy/local/realm.json`. Keycloak imports it only when the realm does not exist, so run `docker compose down -v` after you change it. Event photos go to MinIO, which the stack serves at http://storage.localhost.

## Run the web with hot reload

The dev server shows your changes in the browser as you save them. It needs the local stack for sign-in, because Keycloak runs there. It sends calls to `/api` to an API on your machine, so start that too (see the next section).

```
cd web
npm install
npm run dev
```

Open http://localhost:5173 and sign in with a demo user. The sign-in page comes from the stack, and it sends you back to port 5173.

- The proxy to the API is in `web/vite.config.ts`, and the Keycloak address is in `web/public/config.json`.
- To run the tests again each time you save, use `npx vitest` in `web/`.

## Run the API locally with debugging

The API runs on your machine, so that you can set breakpoints. It uses the database, the sign-in service, and the other parts that run in the local stack. Start the stack first. The stack publishes the database on port 5432 of your machine. Then start the API in one of these ways:

- Visual Studio Code with the C# Dev Kit: open the `api/` folder, open `Frongle.sln`, and press F5. Choose the `Frongle.Api` project.
- JetBrains Rider or Visual Studio: open `api/Frongle.sln`, choose the `http` launch profile, and start the debugger.
- A terminal: run `cd api && dotnet watch --project src/Frongle.Api`. It restarts on every save. To debug, attach your editor to the `Frongle.Api` process.

The API listens on http://localhost:5162. Check it with `curl http://localhost:5162/health/ready`. It answers `200` when it reaches the database. The settings for this mode are in `api/src/Frongle.Api/appsettings.Development.json`. They point to the stack's database and to its sign-in service, and the credentials are for local work only.

With `npm run dev` running as well, a click in the browser at http://localhost:5173 reaches your breakpoints. The API at http://localhost, which runs inside the stack, is a separate copy that serves the built images.

## Connect to the database locally

The local stack runs PostgreSQL in the `postgres` service and publishes it on port 5432 of your machine.

Then connect with `psql`, or with any database tool, using these settings:

| Setting  | Value                 |
| -------- | --------------------- |
| Host     | `localhost`           |
| Port     | `5432`                |
| Database | `frongle`             |
| User     | `frongle`             |
| Password | `local-only-password` |

```
psql "postgresql://frongle:local-only-password@localhost:5432/frongle"
```

Without `psql` on your machine, open a shell in the container instead:

```
docker compose exec postgres psql -U frongle -d frongle
```

This login owns the database and has superuser rights, so it sees the data of every tenant. If another PostgreSQL already uses port 5432 on your machine, change the port mapping of the `postgres` service in `compose.yaml`, for example to `55432:5432`, and use that port. Keycloak keeps its own data in a second database called `keycloak` on the same server. `docker compose down -v` deletes the database.

## Run the tests

```
cd web && npm install && npm test
cd api && dotnet test
```

## Develop

CI runs tests, format checks, linters, `helm lint`, `terraform validate`, Trivy, CodeQL, and gitleaks. Run the format commands before you push: `npm run format` in `web/`, `dotnet format` in `api/`, and `npx prettier --write .` at the top level.

See `deploy/README.md` for deployment to AWS.

## Practices

We write the test first (TDD). We build only what we need now (YAGNI). We follow clean code principles. `CLAUDE.md` holds the full rules.
