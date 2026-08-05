# Deployment wording fix report

## Scope

Fixed only the Minor documentation finding from
`deployment-readiness-review.md`. The wording now matches the workflow triggers
in `.github/workflows/ci.yml`: `pull_request` and `push` to `main`.

## Change

- `docs/deployment.md:67-68`: clarified that direct pushes to non-`main`
  branches do not trigger the workflow, so they run neither validation nor the
  Render Deploy Hook.
- `.github/workflows/ci.yml` was not modified; the existing Render deploy gate
  remains restricted to a successful push to `main`.

## Verification

- Compared the documented trigger wording with `.github/workflows/ci.yml:3-6`.
- Confirmed the deploy condition remains
  `.github/workflows/ci.yml:40`:
  `github.event_name == 'push' && github.ref == 'refs/heads/main'`.
- Confirmed the diff is limited to the requested documentation wording and this
  report; no code, workflow, Git, or external service changes were made.
