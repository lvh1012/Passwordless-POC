# Task 6: Add Docker and Render deployment configuration

## Files

- Create `Dockerfile`.
- Create `.dockerignore`.
- Create `render.yaml`.
- Create `docs/deployment.md`.
- Modify `PasskeyAuthn/Program.cs` only if needed to consume Render `PORT` as implemented in Task 5.

## Deployment constraints

- Runtime is `.NET 10` and the application starts with `dotnet PasskeyAuthn.dll`.
- Use a reproducible multi-stage Docker build from the repository root.
- Render service is a Docker Web Service on the `free` plan, with the platform-provided `onrender.com` HTTPS subdomain and health check `/health`.
- Render commit auto-deploy is disabled; GitHub Actions will trigger the Render Deploy Hook in Task 7.
- Runtime database/passkey values are supplied through Render environment variables; no credentials or real hostnames are checked in.
- Supabase is the only application database. Use the Supabase Shared Pooler session-mode PostgreSQL connection string for a persistent backend and require SSL.
- Do not introduce an image or deployment dependency on the test project.
- Add XML/docs comments where code/config requires non-obvious platform or security rationale.

## Required Dockerfile shape

Use the repository root as build context and this multi-stage structure (adjust only if required for a verified build issue):

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY PasskeyAuthn/PasskeyAuthn.csproj PasskeyAuthn/
RUN dotnet restore PasskeyAuthn/PasskeyAuthn.csproj

COPY PasskeyAuthn/ PasskeyAuthn/
RUN dotnet publish PasskeyAuthn/PasskeyAuthn.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "PasskeyAuthn.dll"]
```

`.dockerignore` must exclude `.git`, `.idea`, `bin`, `obj`, local environment/secrets, test artifacts, and SDD scratch artifacts where appropriate, without excluding application source, migrations, or deployment docs needed for build context.

## Required Render Blueprint

Create `render.yaml` with equivalent values:

```yaml
services:
  - type: web
    name: passkey-authn
    runtime: docker
    plan: free
    dockerfilePath: ./Dockerfile
    healthCheckPath: /health
    autoDeployTrigger: off
    envVars:
      - key: ConnectionStrings__Default
        sync: false
      - key: Passkey__ServerDomain
        sync: false
      - key: Passkey__ExpectedOrigin
        sync: false
```

Do not put a Supabase URL, password, deploy hook, or service-specific hostname in `render.yaml`. Render supplies the free `https://<service>.onrender.com` hostname after provisioning.

## Required deployment documentation

In `docs/deployment.md`, document:

1. Create a Supabase project and obtain its Shared Pooler session-mode PostgreSQL connection string.
2. Require SSL in the connection string/configuration (for example `SSL Mode=Require`; do not paste a real secret into the docs).
3. Create the Render Web Service from the repository/Blueprint using Docker and the Free plan.
4. Set `ConnectionStrings__Default` in Render's Environment settings.
5. Set `Passkey__ServerDomain` to `<service>.onrender.com` (host/RP ID only).
6. Set `Passkey__ExpectedOrigin` to `https://<service>.onrender.com` (full origin).
7. Explain that Render's TLS and HTTP-to-HTTPS behavior provide the deployed HTTPS origin; do not configure a custom domain for this POC.
8. Explain where to create the Render Deploy Hook and that Task 7 stores its URL only as the GitHub `RENDER_DEPLOY_HOOK_URL` secret.
9. Confirm `/health` is healthy before browser Passkey testing.
10. Record Render Free sleep/wake behavior, Supabase project limitations, no account recovery/email verification, and POC-only scope.

Never include a real connection string, secret, private key, cookie, credential JSON, or deploy hook URL in docs.

## Verification

Run:

```powershell
dotnet build PasswordlessAuthn.slnx --configuration Release
docker build --tag passkeyauthn:poc .
docker image inspect passkeyauthn:poc
dotnet format PasswordlessAuthn.slnx --verify-no-changes --no-restore
```

If Docker is unavailable, record the exact daemon failure and still verify Dockerfile source shape, project publish/build, Blueprint values, and secret hygiene. Do not claim image validation passed when it did not.

With Docker available, also run:

```powershell
docker run --rm --env PORT=10000 --publish 10000:10000 passkeyauthn:poc
```

With a valid local database configuration, `GET http://localhost:10000/health` must succeed; stop the container after verification.

## Scope boundary

Do not create GitHub Actions workflow in this task; that is Task 7. Do not deploy externally or create Render/Supabase resources from this task. Do not initialize Git or create commits.

