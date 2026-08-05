# Task 2 Report — Relocate and Scope Deployment Artifacts

Date: 2026-08-05  
Status: Complete

## Changed files

- Moved `Dockerfile` to `pocs/PasskeyAuthn/Dockerfile`.
- Moved `.dockerignore` to `pocs/PasskeyAuthn/.dockerignore`.
- Moved `render.yaml` to `pocs/PasskeyAuthn/render.yaml`.
- Moved `docs/deployment.md` to `pocs/PasskeyAuthn/docs/deployment.md`.
- Updated the moved Dockerfile to restore and publish
  `src/PasskeyAuthn/PasskeyAuthn.csproj` and copy `src/PasskeyAuthn/` for a
  `pocs/PasskeyAuthn` Docker context. The entrypoint remains
  `dotnet PasskeyAuthn.dll`.
- Updated the moved `.dockerignore` with POC-relative `tests/`, .NET build
  artifact, SDD scratch-space, and credential/private-key exclusions. Comments
  document the context isolation and the defensive test-project exclusion.
- Kept Render `dockerfilePath: ./Dockerfile` and `healthCheckPath: /health`.
  The Blueprint comments and deployment guide identify
  `pocs/PasskeyAuthn/render.yaml` and `pocs/PasskeyAuthn` as the file/directory
  to select.
- Updated the deployment guide's CI reference to
  `../../.github/workflows/ci.yml` relative to the POC directory, with a
  Markdown link that resolves from `pocs/PasskeyAuthn/docs/deployment.md`.

## Commands and outcomes

1. Move command, run from `D:\Code\PasswordlessAuthn`:

   ```powershell
   Move-Item -LiteralPath Dockerfile -Destination pocs\PasskeyAuthn\Dockerfile
   Move-Item -LiteralPath .dockerignore -Destination pocs\PasskeyAuthn\.dockerignore
   Move-Item -LiteralPath render.yaml -Destination pocs\PasskeyAuthn\render.yaml
   Move-Item -LiteralPath docs\deployment.md -Destination pocs\PasskeyAuthn\docs\deployment.md
   ```

   Outcome: all four moves completed; destination collisions were checked
   before moving, and no old root artifact remains.

2. Static Dockerfile/ignore/Render/deployment validation, run with
   `powershell` from `D:\Code\PasswordlessAuthn`:

   ```powershell
   $dockerfile = Get-Content -Raw pocs\PasskeyAuthn\Dockerfile
   $ignore = Get-Content -Raw pocs\PasskeyAuthn\.dockerignore
   $render = Get-Content -Raw pocs\PasskeyAuthn\render.yaml
   $deployment = Get-Content -Raw pocs\PasskeyAuthn\docs\deployment.md
   ```

   Outcome: exit code `0`. All required Docker paths, publish target,
   entrypoint, `tests/` and build-artifact exclusions, secret/private-key
   exclusions, Render service shape, local Dockerfile path, `/health`, and
   `sync: false` environment declarations passed.

3. Link/path checks:

   ```powershell
   Test-Path pocs\PasskeyAuthn\src\PasskeyAuthn\PasskeyAuthn.csproj
   Test-Path pocs\PasskeyAuthn\tests
   Test-Path .github\workflows\ci.yml
   Test-Path .superpowers\sdd\2026-08-05-passwordless-poc-directory-layout
   ```

   Outcome: all expected paths returned `True`; the deployment guide's
   `../../../.github/workflows/ci.yml` Markdown target resolves successfully
   from its new location.

4. Stale-reference self-review:

   ```powershell
   Get-ChildItem -Recurse -File -Force . |
     Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } |
     Select-String -Pattern 'COPY PasskeyAuthn/|PasskeyAuthn/PasskeyAuthn\.csproj|docs/deployment\.md'
   ```

   Outcome: no stale operational deployment reference was found. Matches are
   limited to historical SDD reports/plans under `.superpowers` and
   `docs/superpowers`; those records were intentionally not rewritten.

## Scope and concerns

- No source project, test project, solution, or `.github/workflows/ci.yml` was
  modified.
- No Git repository was initialized and no commit was created.
- No real secret, Deploy Hook URL, database credential, or deployment hostname
  was added.
- Full Docker build and .NET/Node test suites were not run because this task
  requested static validation only after the artifact relocation; Dockerfile,
  Render, path, and secret-shape checks passed.
- Existing generated `bin/obj` output under the POC was left untouched; the
  scoped `.dockerignore` now excludes it from the POC Docker context.
