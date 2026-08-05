# Multi-POC CI/CD orchestration report

## Scope

Implemented root CI/CD orchestration and PasskeyAuthn deployment documentation
in the permitted files. No Git repository was initialized and no commit was
created. Existing `poc.json`, `ci.sh`, and discovery scripts were not modified.

## Workflow

- `discover` checks out the repository, sets up Node, runs
  `node scripts/discover-pocs.test.mjs`, runs `node scripts/discover-pocs.mjs`,
  and writes the JSON matrix to `GITHUB_OUTPUT`.
- `solution` restores and builds `PasswordlessAuthn.slnx`.
- `poc-validation` depends on discovery and solution validation, uses
  `fromJSON(needs.discover.outputs.matrix)`, disables fail-fast, sets up .NET
  and Node, and runs each entry's
  `bash "${{ matrix.path }}/${{ matrix.ciScript }}"`.
- `deploy` depends on all validation jobs and runs only after a successful push
  to `main`. Entries with `deployHookSecret: ""` are skipped by the deploy
  step, which supports POCs without Render deployment.
- Deploy hook lookup uses `secrets[matrix.deployHookSecret]` and passes the URL
  only through `RENDER_DEPLOY_HOOK_URL`; the URL is not echoed or logged.
- Triggers include pull requests and pushes to `main`, filtered to workflow,
  discovery scripts, POCs, and the aggregate solution. Permissions remain
  `contents: read`.

## Documentation

The PasskeyAuthn README documents discovery conventions, required `poc.json` and
`ci.sh`, optional `render.yaml`, one secret per deployable POC, and the rule
that adding a POC does not require root workflow edits.

The deployment guide uses
`RENDER_DEPLOY_HOOK_URL_PASSKEY_AUTHN`, explains per-POC secret isolation, and
describes root deploy-matrix behavior. Other security and deployment guidance
was preserved.

## Verification

Static checks covered workflow structure, job dependencies, matrix expressions,
trigger path filters, permission scope, dynamic secret lookup, documentation
references, and discovery output. Docker was not run as requested.

The discovery contract is the source of matrix entries; POCs without Render
must emit an empty `deployHookSecret`, while deployable POCs emit their own
secret name.
