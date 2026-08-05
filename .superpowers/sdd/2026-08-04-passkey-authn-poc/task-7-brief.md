# Task 7: Add GitHub Actions CI and Render deployment trigger

## Files

- Create `.github/workflows/ci.yml`.
- Modify `docs/deployment.md` only to document the exact GitHub/Render secret setup and workflow behavior.

## Interfaces and constraints

- Workflow name is `CI`.
- Trigger on every `pull_request` and on pushes to `main` only.
- Required secret is `RENDER_DEPLOY_HOOK_URL`; the URL must never be checked in or printed.
- Render deploy condition is only a successful push event where `github.ref == 'refs/heads/main'`.
- Use GitHub-hosted Linux runners so Docker and Testcontainers PostgreSQL are available.
- Do not add a second CI provider, third-party WebAuthn dependency, or deployment credential to the repository.

## Required workflow shape

Create `.github/workflows/ci.yml` with equivalent behavior:

```yaml
name: CI

on:
  pull_request:
  push:
    branches: [main]

permissions:
  contents: read

jobs:
  validate:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 10.0.x

      - name: Restore
        run: dotnet restore

      - name: Build
        run: dotnet build --configuration Release --no-restore

      - name: Test
        run: dotnet test --configuration Release --no-build

      - name: Build Docker image
        run: docker build --tag passkeyauthn:ci .

      - name: Trigger Render deploy
        if: github.event_name == 'push' && github.ref == 'refs/heads/main'
        env:
          RENDER_DEPLOY_HOOK_URL: ${{ secrets.RENDER_DEPLOY_HOOK_URL }}
        run: |
          test -n "$RENDER_DEPLOY_HOOK_URL"
          curl --fail-with-body --request POST "$RENDER_DEPLOY_HOOK_URL"
```

The workflow must validate restore/build/test/Docker before the deploy step. A PR or non-main push must never invoke the hook. Do not expose the secret via `echo`, command arguments outside the environment reference, artifacts, or logs.

## Documentation requirements

Update `docs/deployment.md` with:

1. Render service Settings → Deploy Hook creation location.
2. GitHub repository Settings → Secrets and variables → Actions → New repository secret location.
3. Exact secret name `RENDER_DEPLOY_HOOK_URL`.
4. Auto-Deploy must remain off in Render because GitHub Actions triggers the hook.
5. PR and non-main pushes validate only; a successful push to `main` validates and then calls the hook.
6. The Deploy Hook URL is never stored in the repository or Render environment variables.

## Verification

Run the local equivalent:

```powershell
dotnet restore
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build
docker build --tag passkeyauthn:ci .
```

Inspect the workflow source for:

- correct triggers and branch condition;
- secret-only Render URL;
- no Supabase connection string or other runtime secret;
- required actions/commands in order.

If local Docker/Testcontainers is unavailable, report the exact blocked commands, still run restore/build/non-container tests, and do not claim local Docker validation passed. GitHub-hosted CI remains the authoritative Docker integration environment.

## Scope boundary

Do not push to GitHub, create repository secrets, create Render resources, or call an external Deploy Hook from this task. Do not initialize Git or create commits.

