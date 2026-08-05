# POC CI contract/discovery report

Date: 2026-08-05

## Scope

Implemented only the requested files:

- `pocs/PasskeyAuthn/poc.json`
- `pocs/PasskeyAuthn/ci.sh`
- `scripts/discover-pocs.mjs`
- `scripts/discover-pocs.test.mjs`
- this report

No Git metadata was created. `.github/workflows/ci.yml` and `docs/README` were not changed.

## Contract implemented

`poc.json` contains only non-secret deployment metadata:

```json
{"id":"passkey-authn","deployHookSecret":"RENDER_DEPLOY_HOOK_URL_PASSKEY_AUTHN"}
```

The CI entry point is a fixed convention. Discovery always emits `ciScript: "ci.sh"`; it never reads a script name from the manifest. `deployHookSecret` is optional: missing or `null` metadata is emitted as `""`, while any non-empty value must be an uppercase environment name.

`discover-pocs.mjs`:

- uses Node built-ins only;
- treats the current working directory as repository root;
- scans only direct child directories of `pocs/`, preserving monorepo/POC isolation;
- requires `poc.json` and `ci.sh` for every discovered POC;
- validates lowercase kebab-case IDs and uppercase environment-name deploy secrets;
- canonicalizes POC paths and rejects a symlink/path escaping the `pocs/` root;
- fails with a `POC discovery failed:` message for a missing root, empty POC set, invalid manifest, or missing CI script;
- emits one JSON line suitable for a GitHub Actions matrix.

Current output:

```json
{"include":[{"id":"passkey-authn","path":"pocs/PasskeyAuthn","ciScript":"ci.sh","deployHookSecret":"RENDER_DEPLOY_HOOK_URL_PASSKEY_AUTHN"}]}
```

`ci.sh` calculates both the POC root and repository root from its own location, then runs the requested restore/build/test, JavaScript syntax check, browser contract test, and Docker build. The Docker context is the POC root so each POC owns its image inputs and cannot depend on a repository-root Dockerfile.

## Verification

Passed:

- `node --check scripts/discover-pocs.mjs`
- `node --check scripts/discover-pocs.test.mjs`
- `node --test scripts/discover-pocs.test.mjs` — 2 passed, including missing and `null` optional deploy hook metadata
- `node scripts/discover-pocs.mjs` — exact contract output above
- `bash -n pocs/PasskeyAuthn/ci.sh`
- `node --check pocs/PasskeyAuthn/src/PasskeyAuthn/wwwroot/js/passkey.js`
- `node --test pocs/PasskeyAuthn/tests/PasskeyAuthn.Tests/Browser/passkey-client-contract.test.mjs` — 2 passed
- `dotnet restore .../PasskeyAuthn.Tests.csproj` — up to date
- `dotnet build .../PasskeyAuthn.Tests.csproj --configuration Release --no-restore` — succeeded, 0 warnings, 0 errors

Blocked by local environment:

- Full `bash pocs/PasskeyAuthn/ci.sh` reached `dotnet test`, but the test suite could not create Testcontainers because Docker was unavailable. Result: 67 total, 33 passed, 34 failed; failures report `DockerUnavailableException` and endpoint `npipe://./pipe/docker_engine`.
- Direct Docker verification with the requested command failed before build: `failed to connect to the docker API at npipe:////./pipe/dockerDesktopLinuxEngine ... The system cannot find the file specified.`
- `shellcheck` was not installed, so the available static shell check was `bash -n`.

Because `ci.sh` uses strict mode, the unavailable Docker daemon correctly stops the workflow at the test phase; the Node and Docker phases were also run independently to verify their exact local outcomes.
