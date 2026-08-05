# Task 8: Perform end-to-end verification and finalize handoff documentation

## Files

- Modify `docs/deployment.md`.
- Modify `docs/superpowers/specs/2026-08-04-passkey-authn-design.md` only if implementation decisions materially differ from the approved design; otherwise leave it unchanged.

## Required verification

Run the complete local equivalent:

```powershell
dotnet restore
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build
docker build --tag passkeyauthn:verification .
```

Also run/inspect:

```powershell
dotnet format PasswordlessAuthn.slnx --verify-no-changes --no-restore
dotnet test PasskeyAuthn.Tests/PasskeyAuthn.Tests.csproj --configuration Release --no-build --filter FullyQualifiedName~RegistrationStateProtectorTests
node --check PasskeyAuthn/wwwroot/js/passkey.js
```

Run a secret hygiene scan over checked-in source/docs/workflows (excluding `bin/obj` and SDD reports) for real Supabase connection strings, passwords, private keys, credential JSON, cookies, and Deploy Hook URLs. Do not print any secret values.

If Docker is unavailable, preserve and report the exact failure; do not replace real PostgreSQL/Testcontainers tests. If no GitHub repository, Render service, Supabase project, credentials, or deployed URL exists in the workspace, do not fabricate deployment evidence; document the precise handoff steps still required.

## Required deployment/acceptance documentation

Extend `docs/deployment.md` with a manual HTTPS acceptance checklist:

1. Open `/register` on the deployed `https://<service>.onrender.com` URL and create a platform Passkey.
2. Confirm redirect to `/dashboard` and the expected email display.
3. Logout.
4. Open `/` and sign in with the same email and Passkey.
5. Confirm anonymous `/dashboard` redirects to `/`.
6. Test cancelled authenticator, unsupported WebAuthn, duplicate registration, unknown email, malformed credential, and expired challenge behavior.
7. Verify `AspNetUserPasskeys` exists in Supabase PostgreSQL without exposing private key material; only public credential metadata is expected.
8. Confirm `/health` is healthy before and after Render Free wake-up.

Document that successful cryptographic ceremony and deployment require a real browser/authenticator, valid Supabase credentials, a Render service, a GitHub repository, the repository secret `RENDER_DEPLOY_HOOK_URL`, and a push to `main`. Record Render Free sleep/wake, Supabase Free limitations, and POC limitations.

## Scope and completion rules

- Do not create external accounts/resources, push to GitHub, create secrets, call a Render hook, or pretend manual HTTPS evidence exists without those external resources.
- Do not initialize Git or create commits.
- The task is complete only when all available automated checks have evidence and deployment/manual limitations are explicitly handed off.

