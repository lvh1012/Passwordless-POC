# Passwordless POC Directory Layout Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move the existing Passkey POC into a self-contained `pocs/PasskeyAuthn` boundary and update all solution, build, deployment, CI, test, and documentation paths.

**Architecture:** The repository root owns the solution and shared CI/process documentation. Each POC owns its source under `src`, tests under `tests`, and deployment artifacts beside them. The Passkey POC's Docker build uses `pocs/PasskeyAuthn` as its context, preventing unrelated POCs from entering the image.

**Tech Stack:** .NET 10, ASP.NET Core, Razor Pages, xUnit, Node.js browser contract test, Docker, Render, GitHub Actions.

## Global Constraints

- Preserve the existing .NET target framework, assembly names, namespaces, package versions, migrations, and runtime behavior.
- Keep every POC independently buildable and deployable from its own directory.
- Keep `PasswordlessAuthn.slnx` as the single solution entry point.
- Keep deployment secrets outside the repository.
- Do not initialize Git, create commits, or alter unrelated process-history files.
- Every changed source/config comment must explain a non-obvious path or isolation decision rather than restating a command.

---

### Task 1: Relocate source and test projects into the Passkey POC boundary

**Files:**
- Move: `PasskeyAuthn/` → `pocs/PasskeyAuthn/src/PasskeyAuthn/`
- Move: `PasskeyAuthn.Tests/` → `pocs/PasskeyAuthn/tests/PasskeyAuthn.Tests/`
- Modify: `PasswordlessAuthn.slnx`
- Modify: `pocs/PasskeyAuthn/tests/PasskeyAuthn.Tests/PasskeyAuthn.Tests.csproj`
- Modify: `pocs/PasskeyAuthn/tests/PasskeyAuthn.Tests/Browser/passkey-client-contract.test.mjs`

**Interfaces:**
- The solution project paths become `pocs/PasskeyAuthn/src/PasskeyAuthn/PasskeyAuthn.csproj` and `pocs/PasskeyAuthn/tests/PasskeyAuthn.Tests/PasskeyAuthn.Tests.csproj`.
- The test project references the source project at `../../src/PasskeyAuthn/PasskeyAuthn.csproj`.
- The Node contract test resolves `../../../src/PasskeyAuthn/wwwroot/js/passkey.js` relative to its moved test file.

- [ ] **Step 1: Create the destination directories and move both project trees.**
- [ ] **Step 2: Update the solution and project/browser relative paths.**
- [ ] **Step 3: Search for stale source/test paths and verify the moved tree contains no duplicate project directories.**

### Task 2: Relocate and scope deployment artifacts to the Passkey POC

**Files:**
- Move: `Dockerfile` → `pocs/PasskeyAuthn/Dockerfile`
- Move: `.dockerignore` → `pocs/PasskeyAuthn/.dockerignore`
- Move: `render.yaml` → `pocs/PasskeyAuthn/render.yaml`
- Move: `docs/deployment.md` → `pocs/PasskeyAuthn/docs/deployment.md`
- Modify: `pocs/PasskeyAuthn/Dockerfile`
- Modify: `pocs/PasskeyAuthn/.dockerignore`
- Modify: `pocs/PasskeyAuthn/render.yaml`
- Modify: `pocs/PasskeyAuthn/docs/deployment.md`

**Interfaces:**
- Docker build context is `pocs/PasskeyAuthn`; the Dockerfile restores and publishes `src/PasskeyAuthn/PasskeyAuthn.csproj`.
- Render's Dockerfile path is `./Dockerfile` when the Blueprint is selected from the POC directory.
- Deployment instructions link to paths relative to the POC boundary and explicitly identify that directory as the Render Blueprint root.

- [ ] **Step 1: Move deployment files and the deployment documentation into the POC directory.**
- [ ] **Step 2: Update Docker COPY paths, ignore comments, Render guidance, and deployment links.**
- [ ] **Step 3: Validate the Dockerfile's source-shape and secret-exclusion rules without requiring a running Docker daemon.**

### Task 3: Update repository-level CI and documentation references

**Files:**
- Modify: `.github/workflows/ci.yml`
- Modify: root references that point to the moved deployment guide, if any

**Interfaces:**
- CI restores/builds/tests `PasswordlessAuthn.slnx` from the repository root.
- Node validation uses `pocs/PasskeyAuthn/src/PasskeyAuthn/wwwroot/js/passkey.js` and `pocs/PasskeyAuthn/tests/PasskeyAuthn.Tests/Browser/passkey-client-contract.test.mjs`.
- Docker validation uses `docker build --file pocs/PasskeyAuthn/Dockerfile --tag passkeyauthn:ci pocs/PasskeyAuthn`.

- [ ] **Step 1: Update browser contract paths and Docker context in the workflow.**
- [ ] **Step 2: Search all tracked operational files for old root-level paths and correct any remaining references.**

### Task 4: Verify the reorganized solution

**Files:**
- No source changes expected.

- [ ] **Step 1: Restore the solution.**
- [ ] **Step 2: Build the solution in Release configuration.**
- [ ] **Step 3: Run formatter verification and the Node browser contract test.**
- [ ] **Step 4: Run the full .NET test suite and record any environment-only Docker limitation precisely.**
- [ ] **Step 5: Confirm the final directory tree and absence of stale paths.**
