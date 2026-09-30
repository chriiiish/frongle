# Frongle

Web app that tracks assets (light-posts, street signs, telephone poles, traffic lights) and their maintenance and replacement.

Two kinds of user:

- Maintenance Manager: sees asset status, sets Maintenance Schedules, assigns Work Orders to Work Teams.
- Work Team: installs, repairs, removes, and replaces assets.

## Layout

- `web/`: React and TypeScript (Vite, Vitest, React Testing Library). Mobile-first.
- `api/`: C# .NET 10 minimal API with EF Core and PostgreSQL. `src/Frongle.Api`, `src/Frongle.Domain`, `tests/Frongle.Api.Tests`.
- `deploy/`: Terraform (AWS: VPC, EKS, ECR, RDS) in `terraform/`, Helm chart in `helm/frongle/`, local kind cluster scripts in `local/`.

## Commands

- Web: `cd web && npm test`, `npm run lint`, `npm run build`
- API: `cd api && dotnet test`
- Deploy: `cd deploy/terraform && terraform validate`, `helm lint deploy/helm/frongle -f deploy/helm/frongle/values-local.yaml`
- Local cluster: `deploy/local/up.sh` and `deploy/local/down.sh`
- Security scan: `trivy fs --scanners vuln,secret,misconfig --severity HIGH,CRITICAL .`

## Architecture

- Multi-tenant: one shared database. Every tenant-owned table has a `tenant_id`. Isolation uses Postgres row-level security and an EF Core global query filter. The API takes the tenant from the validated token claim `tenant_id`, never from request data.
- Auth: Keycloak (Operator, one `frongle` tenant space) runs in Kubernetes. Roles are `maintenance-manager` and `work-team`. The Helm chart builds that space in `keycloak-realm.yaml`.
- The web app signs in with keycloak-js (PKCE). It reads `/config.json` at runtime, so one image works in every environment.
- Local and prod only. Both use the same Helm chart. Local adds `values-local.yaml` (in-cluster Postgres, demo users).

## Working rules

- TDD: write a failing test first, make it pass with the least code, then refactor. Use the `/tdd` skill for features and bug fixes.
- YAGNI: add no code, config, dependency, or abstraction that a current test or requirement does not need.
- Clean code: small functions, names that reveal intent, no dead code, comments only to explain why.
- Mobile-first: design for a phone screen, then scale up with `min-width` media queries.
- API dependencies point inward: Api depends on Domain, and Domain depends on nothing. When a feature needs them, add Application and Infrastructure projects. Do not add them before.
- Domain words: Asset, Maintenance Manager, Work Team, Work Order, Maintenance Schedule. Use them in code and tests.
- Format before you finish: `npm run format` (web), `dotnet format` (api), `npx prettier --write .` (top level).
- The API build treats analyzer warnings as errors.
- Do not commit unless asked.
- Write commit messages in Conventional Commits style: `type(scope): summary`. Use the types `feat`, `fix`, `docs`, `test`, `refactor`, `chore`, `build`, and `ci`. Write the summary in the imperative mood, with no full stop.
