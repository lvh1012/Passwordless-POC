# Deployment-readiness audit

Date: 2026-08-04

## Scope and method

This audit inspected the approved design and implementation plan, the prior
handoff audit, deployment documentation, `render.yaml`, `Dockerfile`, the
GitHub Actions workflow, application sources, and test sources. It did not
call GitHub, Render, Supabase, or any other external service, and it did not
initialize or commit Git.

## Checklist

| Requirement | Local evidence | Status |
| --- | --- | --- |
| .NET 10 web app uses built-in Identity Passkey support | `PasskeyAuthn.csproj`, `Program.cs`, Identity schema v3 migration | Ready locally |
| Persistent Supabase PostgreSQL state and Data Protection keys | `ApplicationDbContext`, startup migration, PostgreSQL Testcontainers coverage | Ready in source; real Supabase pending |
| Render Docker service uses platform health check and Free plan | `render.yaml` declares Docker, `plan: free`, `/health`, and disabled auto-deploy | Ready in source |
| Container listens on Render `PORT` and `0.0.0.0` | `Program.cs` binds `http://0.0.0.0:<PORT-or-8080>` | Ready in source |
| Production image is multi-stage and starts the application assembly | `Dockerfile` publishes only `PasskeyAuthn` and uses `dotnet PasskeyAuthn.dll` | Static review ready; local Docker build blocked |
| Render WebAuthn host/origin is fail-closed | `ProductionConfigurationValidator` requires a non-local host/RP ID, matching `https` origin, safe limits, and TLS PostgreSQL | Covered by 31 non-Docker C# tests |
| Render proxy restores HTTPS before HSTS/cookies | `Program.cs` configures `X-Forwarded-Proto` and calls `UseForwardedHeaders` before HTTPS middleware | Ready in source |
| GitHub gate validates before deployment | `.github/workflows/ci.yml` restores, builds, tests, checks the browser contract, builds Docker, then invokes the secret-only Deploy Hook for `main` pushes | Ready in source; hosted run pending |
| Runtime secrets stay out of repository and image context | `render.yaml` uses `sync: false`; `.dockerignore` excludes local secret patterns; `appsettings.json` has no database value | Ready in source |
| Supabase connection string can be supplied directly to Npgsql | `docs/deployment.md` now requires Npgsql key/value format rather than an unchanged `postgresql://` URI | Fixed in this audit |
| HTTPS Passkey ceremony, health check, and persistence after restart | Manual checklist in `docs/deployment.md` | Requires external resources |

## Finding addressed

### Resolved: Supabase URI versus Npgsql connection-string format

The previous instructions asked for a Supabase connection string but did not
state that the application validates Npgsql key/value syntax. A local parser
probe accepted a non-secret sample in this form:

```text
Host=<pooler-host>;Port=5432;Database=postgres;Username=<user>;Password=<password>;SSL Mode=Require
```

The same parser rejected a sample `postgresql://...` URI. Pasting that URI
unchanged into `ConnectionStrings__Default` would fail production startup
before migrations. `docs/deployment.md` now gives the required syntax and
explains where the real password belongs.

This was a documentation-only correction: no application behavior changed, so
an automated Unit Test would only test prose rather than a production contract.

## Files changed

- `docs/deployment.md` — added the exact safe Npgsql key/value connection
  string shape and URI warning.
- `.superpowers/sdd/2026-08-04-passkey-authn-poc/deployment-readiness-report.md`
  — this audit record.

## Commands and results

| Command | Result |
| --- | --- |
| `dotnet restore` | Passed; packages already current. |
| `dotnet build --configuration Release --no-restore` | Passed; 0 warnings, 0 errors. |
| Focused non-Docker configuration/security tests | Passed; 31/31. |
| `node --check PasskeyAuthn/wwwroot/js/passkey.js` | Passed. |
| `node --test PasskeyAuthn.Tests/Browser/passkey-client-contract.test.mjs` | Passed; 2/2. |
| `dotnet format PasswordlessAuthn.slnx --verify-no-changes --no-restore` | Passed. |
| Full `dotnet test ... --configuration Release --no-build` | 33 passed, 34 failed during Testcontainers construction because `npipe://./pipe/docker_engine` is unavailable. No application assertion failure was reached. |
| `docker build --tag passkeyauthn:deployment-readiness .` | Blocked because Docker Desktop Linux engine is unavailable at `npipe:////./pipe/dockerDesktopLinuxEngine`. |
| Local Npgsql parser probe | Accepted key/value syntax and rejected the illustrative `postgresql://` URI, without connecting to a database. |
| Runtime-secret pattern scan | No runtime credential was found. The only literal password-shaped value is the deliberate `do-not-echo` invalid-input test fixture. |

## External inputs still required

1. A GitHub repository containing this workspace, with a `main` branch and the
   `RENDER_DEPLOY_HOOK_URL` repository secret.
2. A Supabase project and its Shared Pooler **session-mode** details. The user
   must enter the real Npgsql-format TLS connection string only in Render.
3. A Render Free Docker Web Service created from `render.yaml`. After Render
   assigns `<service>.onrender.com`, the user must set both
   `Passkey__ServerDomain=<service>.onrender.com` and
   `Passkey__ExpectedOrigin=https://<service>.onrender.com` exactly.
4. A Docker-enabled environment, such as GitHub-hosted Actions, to run the
   PostgreSQL Testcontainers suite and build the production image.
5. Browser acceptance on the generated HTTPS subdomain with a real compatible
   authenticator: registration, logout, login, anonymous dashboard redirect,
   public credential-row check, and the restart/redeploy persistence check.

## Verdict

No additional bounded code or configuration gap was found that would prevent
the checked-in project from being configured for the intended GitHub → Render
Free Docker → Supabase PostgreSQL path. The Npgsql-format documentation gap was
fixed. This is not deployment evidence: Docker-dependent verification and all
GitHub, Render, Supabase, HTTPS, and real-browser evidence remain unavailable
until the listed external inputs are provided.
