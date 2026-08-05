# Task 6 review package (Git unavailable)

Git is unavailable, so review current Task 6 artifacts directly and use the implementer report for verification claims. Docker daemon verification is expected to be environment-blocked; do not treat that alone as a source defect.

## Files in scope

- `Dockerfile`
- `.dockerignore`
- `render.yaml`
- `docs/deployment.md`
- `PasskeyAuthn/Program.cs` only to verify existing `PORT` behavior.

## Binding constraints

- Multi-stage .NET 10 SDK/ASP.NET runtime image, root build context, app-only publish, entrypoint `dotnet PasskeyAuthn.dll`.
- Render Docker Web Service, `free` plan, `/health`, `autoDeployTrigger: off`, platform `onrender.com` HTTPS hostname.
- Render values `ConnectionStrings__Default`, `Passkey__ServerDomain`, and `Passkey__ExpectedOrigin` are externally supplied (`sync: false`) and no real secrets/hostnames/deploy hook URL are checked in.
- Supabase Shared Pooler session mode with SSL is documented; Supabase is the only application database.
- Docker context excludes secrets/build/test artifacts but retains app source and migrations.
- Task 7 workflow must not be created in Task 6.

## Review method

Inspect artifact contents, Docker build path/source shape, Blueprint values, docs accuracy/secret hygiene, and consistency with `PORT`/health behavior. Do not mutate files or claim Docker runtime success.

