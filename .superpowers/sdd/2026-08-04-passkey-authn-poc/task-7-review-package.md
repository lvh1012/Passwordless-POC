# Task 7 review package (Git unavailable)

Git is unavailable; inspect current workflow/docs as the Task 7 change set and use the report for command evidence only. Docker/Testcontainers failures are expected environment blockers locally.

## Files in scope

- `.github/workflows/ci.yml`
- `docs/deployment.md` Task 7 additions.

## Binding constraints

- Workflow name `CI`; triggers `pull_request` and pushes to `main` only.
- Linux hosted runner; restore → Release build → Release test → Docker build.
- Render hook runs only after successful push `refs/heads/main` and only from secret `RENDER_DEPLOY_HOOK_URL`.
- PRs/direct non-main pushes must never call the hook; no secrets/connection strings in workflow/docs.
- Render auto-deploy remains off; docs identify Render Deploy Hook and GitHub Actions secret locations.

## Review method

Inspect YAML trigger/condition/order/secret handling and docs consistency. Check that documentation does not claim non-main push runs when the workflow filter excludes them. Do not mutate files or call external services.

