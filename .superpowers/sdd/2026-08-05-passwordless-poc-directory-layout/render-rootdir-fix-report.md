# Render root directory fix report

Date: 2026-08-05

## Scope

Updated only:

- `pocs/PasskeyAuthn/render.yaml`
- `pocs/PasskeyAuthn/docs/deployment.md`

Added the Render service `rootDir: pocs/PasskeyAuthn` and
`dockerContext: .`, while retaining `dockerfilePath: ./Dockerfile`. The
service remains on the Free plan with `/health`, Auto-Deploy off, and all three
environment variables declared with `sync: false` and no secret values.

The deployment guide now instructs users to set the custom Blueprint Path to
`pocs/PasskeyAuthn/render.yaml` and explains that the service root scopes the
Dockerfile and build context.

## Static checks

- Required Render keys: PASS — `rootDir`, `dockerContext`, `dockerfilePath`,
  `plan`, `healthCheckPath`, `autoDeployTrigger`, and `envVars` are present with
  the required values.
- Secret scan: PASS — no secret values were added; all environment variables
  remain `sync: false`.
- Documentation text: PASS — the custom Blueprint Path, service root scope,
  and root-relative Docker paths/context are documented.
- Path existence: PASS — `pocs/PasskeyAuthn/render.yaml` and
  `pocs/PasskeyAuthn/Dockerfile` exist.

Docker and the full integration suite were not run, per scope.
