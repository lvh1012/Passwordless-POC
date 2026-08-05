# Passkey CI/CD Fix Wave Report

Date: 2026-08-05

## Changes completed

- `.github/workflows/ci.yml`
  - Changed `poc-validation.needs` from `[discover, solution]` to `[discover]`.
  - Kept deploy dependencies as `[discover, solution, poc-validation]`.
  - Added `test -n "$RENDER_DEPLOY_HOOK_URL"` before the deploy `curl` command.
- `pocs/PasskeyAuthn/README.md`
  - Documented discovery output: POC path, fixed `ci.sh` validation convention,
    and optional `deployHookSecret`.
  - Clarified that `ci.sh` is not user-configurable.
  - Documented unique non-empty deploy hook secret names and discovery rejection
    of duplicates.
  - Preserved the POC add instructions.
- `pocs/PasskeyAuthn/docs/deployment.md`
  - Documented the workflow path filters for pull request/push events:
    `.github/workflows/ci.yml`, `scripts/**`, `pocs/**`, and
    `PasswordlessAuthn.slnx`.
  - Clarified that the workflow does not run for every pull request.
  - Documented unique deployable-POC secret names and duplicate rejection.
  - Preserved `RENDER_DEPLOY_HOOK_URL_PASSKEY_AUTHN`, Passkey guidance, and the
    no-logging guidance for Deploy Hook URLs.

## Verification

- Workflow static assertion: **PASS**. Confirmed the requested `needs` values,
  and confirmed the non-empty guard precedes `curl`.
- `rg` content check: **PASS**. Confirmed the fixed `ci.sh` convention,
  non-configurable wording, duplicate-secret wording, exact workflow paths,
  current secret name, and no-logging guidance.
- `node scripts/discover-pocs.test.mjs`: **PASS** — 10 tests passed, 0 failed.
  The expected rejection diagnostics for invalid/missing manifests, invalid
  secrets, duplicate IDs/secrets, and escaping symlinks were emitted.
- An initial combined PowerShell check exited **1** because one assertion
  expected a phrase across a line break in the README; the source content was
  then confirmed directly with `rg`. No source change was required for that
  check issue.

## Scope

No discovery scripts or tests were modified. Source changes were limited to the
three requested files; this report is the requested additional artifact.
