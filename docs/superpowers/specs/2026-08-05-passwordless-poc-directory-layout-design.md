# Passwordless POC Directory Layout Design

## Goal

Organize the solution so every passwordless authentication POC is an independently understandable and deployable unit while keeping one solution entry point for the repository.

## Chosen layout

```text
PasswordlessAuthn/
├── PasswordlessAuthn.slnx
├── pocs/
│   └── PasskeyAuthn/
│       ├── src/PasskeyAuthn/
│       ├── tests/PasskeyAuthn.Tests/
│       ├── Dockerfile
│       ├── .dockerignore
│       ├── render.yaml
│       └── docs/deployment.md
├── docs/superpowers/
└── .github/workflows/
```

`pocs/PasskeyAuthn` is the ownership boundary for the Passkey POC. Its source, tests, deployment configuration, container build context, and deployment instructions move together. A future POC can be added as another sibling under `pocs/` without sharing application files or deployment artifacts.

## Root responsibilities

- `PasswordlessAuthn.slnx` lists every POC source project and its test project.
- `.github/workflows/ci.yml` remains repository-level orchestration and invokes each POC using explicit paths.
- `docs/superpowers/` remains process/design history and is not part of a deployable POC.

## Compatibility rules

- Preserve the existing project assembly names and C# namespaces.
- Preserve all runtime behavior, package versions, migrations, and configuration keys.
- Change only filesystem paths, project references, Docker context paths, Render paths, CI paths, and documentation links required by the move.
- Keep the POC Docker context scoped to `pocs/PasskeyAuthn` so another POC cannot accidentally enter its image.

## Verification

The reorganization is complete only when the solution restores, builds, formats, and runs the existing tests; the browser contract test resolves the moved JavaScript file; and source search finds no stale root-level project/deployment paths.
