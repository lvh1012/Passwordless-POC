# Deployment-readiness bounded review

Date: 2026-08-04

## Scope and method

Read-only review of `deployment-readiness-report.md`, `docs/deployment.md`,
`ProductionConfigurationValidator`, `render.yaml`, relevant tests, and
`.github/workflows/ci.yml`. No source or deployment configuration was changed,
and no external service was called. The mandated review artifact is the only
file written.

## Verified

- `docs/deployment.md` explicitly requires `ConnectionStrings__Default` in
  Npgsql key/value form and warns not to paste Supabase's `postgresql://...`
  URI unchanged. A local Npgsql parser probe accepted the documented
  `Host=...;Database=...;Username=...;Password=...;SSL Mode=Require` shape and
  rejected the URI shape.
- `ProductionConfigurationValidator` parses the value with
  `NpgsqlConnectionStringBuilder`, requires host/database/username, and allows
  only `Require`, `VerifyCA`, or `VerifyFull` SSL modes. Validation occurs before
  migration in non-Development/non-Testing startup.
- `render.yaml` exposes `ConnectionStrings__Default` with `sync: false`; the
  workflow receives only `RENDER_DEPLOY_HOOK_URL` from GitHub Secrets and does
  not echo it. Repository docs contain placeholders only; test password
  literals are clearly non-runtime fixtures.
- Render Docker/Free, `/health`, `autoDeployTrigger: off`, and the documented
  `main`-only deploy condition agree with the checked-in workflow. Focused
  configuration tests passed `23/23`; browser contract checks passed `2/2`.

## Finding

- **Minor — documentation wording contradicts the workflow:**
  `docs/deployment.md:67-68` says non-`main` pushes are “validation-only”, but
  `.github/workflows/ci.yml:3-6` triggers only for pull requests and pushes to
  `main`; a non-`main` branch push does not start this workflow. This does not
  weaken the connection-string correction or the `main` deployment gate, but
  the wording should say that pull requests (or a separately configured local
  check) provide validation for non-`main` work.

## Verdict

**Pass for the bounded Supabase connection-string change, with one minor
documentation follow-up noted above.** The change prevents the incompatible
URI paste, documents the correct Npgsql/TLS shape, and introduces no observed
secret leakage or connection/deployment-step contradiction.
