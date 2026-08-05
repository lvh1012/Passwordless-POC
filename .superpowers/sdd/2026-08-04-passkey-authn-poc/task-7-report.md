# Task 7 Report — GitHub Actions CI/CD

## Status

Implemented within the requested scope. The workflow and deployment documentation
are present and the workflow source matches the required CI/CD shape. Full local
test and Docker validation are blocked by the unavailable Docker daemon; GitHub
hosted `ubuntu-latest` remains the authoritative environment for Testcontainers
PostgreSQL and Docker image validation.

## Changed files

- Created `.github/workflows/ci.yml`.
  - Workflow name: `CI`.
  - Triggers: every `pull_request`; pushes to `main` only.
  - Permissions: `contents: read`.
  - Runner: `ubuntu-latest`.
  - Validation order: restore → Release build → Release test → Docker build.
  - Render hook condition: `github.event_name == 'push' && github.ref == 'refs/heads/main'`.
  - Hook URL is read only from `secrets.RENDER_DEPLOY_HOOK_URL` through the step environment and is not printed.
- Updated `docs/deployment.md`.
  - Documents Render **Settings → Deploy Hook**.
  - Documents GitHub **Settings → Secrets and variables → Actions → New repository secret**.
  - Uses the exact secret name `RENDER_DEPLOY_HOOK_URL`.
  - Requires Render **Auto-Deploy** to remain off.
  - Describes validation-only PR/non-main behavior and main push deployment behavior.
  - States that the Deploy Hook URL is not stored in the repository or Render environment variables.

## Validation summary

| Check | Result |
|---|---|
| `dotnet restore` | Passed; projects up to date |
| `dotnet build --configuration Release --no-restore` | Passed; 0 warnings, 0 errors |
| `dotnet test --configuration Release --no-build` | Blocked by Docker; 43 total, 9 passed, 34 failed during Testcontainers Docker endpoint initialization |
| Non-container test: `dotnet test --configuration Release --no-build --filter FullyQualifiedName~IdentitySchemaConfigurationTests` | Passed; 1/1 |
| `docker build --tag passkeyauthn:ci .` | Blocked; Docker Desktop Linux engine unavailable |
| Workflow/source secret inspection | Passed; no concrete Deploy Hook URL, Supabase connection string, or runtime secret found in the changed workflow/docs |

The exact Docker/Testcontainers blocker was:

```text
Docker is either not running or misconfigured.
Failed to connect to Docker endpoint at 'npipe://./pipe/docker_engine'.
```

The Docker CLI separately reported that `dockerDesktopLinuxEngine` was not
available. No attempt was made to start Docker, call a Render hook, create a
secret, or contact GitHub.

## Git status and external actions

- Commits: none.
- Pushes: none.
- Repository secret creation: none.
- Render resources/hooks: none created or called.
- Git initialization: not performed. The workspace is not currently a Git repository, so `git status` reports `fatal: not a git repository`.

## Concerns / handoff

1. Run the full `dotnet test --configuration Release --no-build` and
   `docker build --tag passkeyauthn:ci .` again with Docker Desktop running, or
   rely on the GitHub-hosted runner, before treating the CI run as fully green.
2. Because the required trigger is `push: branches: [main]`, pushes to other
   branches do not start this workflow. They cannot invoke the Render hook; pull
   requests are the validation-only workflow event.
3. Add the `RENDER_DEPLOY_HOOK_URL` repository secret manually only after the
   Render Deploy Hook exists. The secret value must not be copied into this
   repository, Render environment variables, or logs.
