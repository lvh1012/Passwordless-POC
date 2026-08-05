# Final whole-branch fix wave

The final review found one Critical and several Important findings. Implement all fixes in one wave, then run one scoped re-review. Work directly in the current workspace; no Git initialization, commits, external services, or resource creation.

## Critical — fix first

`PasskeyAuthn/wwwroot/js/passkey.js` manually serializes the browser credential but omits the root `clientExtensionResults` field required by the .NET 10 Identity WebAuthn JSON contract. Add:

```javascript
clientExtensionResults: credential.getClientExtensionResults(),
```

to the serialized `PublicKeyCredential` object. Include optional standard fields such as `authenticatorAttachment` and attestation transports when available, without breaking browsers that return null/undefined. Keep all binary fields unpadded base64url.

Add a runnable dependency-free Node contract test under `PasskeyAuthn.Tests/Browser/` that loads `passkey.js` in a small VM/browser stub and asserts the serialized object contains `id`, `rawId`, `type`, root `clientExtensionResults`, `response.clientDataJSON`, and attestation/assertion fields. Add a GitHub Actions step to run `node --test` for this contract test. `node --check` must still pass.

## Important — API auth challenge behavior

`Program.cs` currently redirects every cookie challenge to `/`, which makes anonymous `/api/auth/logout` return `302` instead of the required API `401`. Keep Razor Page anonymous dashboard redirects as exact `/`, but for `/api/**` set `401` without redirect. Also set API access denied to `403` without redirect while keeping page access behavior safe. Preserve production cookie `HttpOnly`, `SecurePolicy.Always`, and `SameSite.Lax`. Add/adjust a focused test if it can run without Docker; retain the existing integration test expectation.

## Important — production configuration fail-closed

Add a documented public configuration validator (or equivalent well-tested implementation) and invoke it during startup for non-Development/non-Testing environments before database migration. It must reject without exposing secret values:

- missing/invalid `Passkey:ServerDomain` (host/RP ID only);
- missing/invalid `Passkey:ExpectedOrigin` (full `https://` origin whose host exactly matches ServerDomain, no path/query/fragment/port);
- non-positive timeout;
- `MaxPasskeysPerUser` below 1 or above approved maximum 3;
- `MaxDisplayNameLength` below 1 or above approved maximum 100;
- missing/invalid PostgreSQL connection string or SSL mode that is not `Require`, `VerifyCA`, or `VerifyFull`.

Development/Testing may use local defaults and Testcontainers' non-TLS connection. Production must not become healthy with localhost fallback or a non-TLS connection. Add unit tests for valid production config and representative invalid cases; do not use in-memory database.

## Important — account-enumeration conflict resolution

The approved design and task plan intentionally require an email-first UX, a generic unknown-login error, and HTTP 409 for duplicate registration. Those requirements cannot provide strong uniform anti-enumeration because status/options/timing distinguish existing accounts. Do not silently remove the approved email-first or duplicate-conflict behavior. Instead:

- keep response messages generic and free of account/passkey details;
- add an explicit deployment-doc limitation that this POC's email-first flow can reveal account eligibility through status/options and is not suitable as a production anti-enumeration design;
- ensure the final report/ledger does not claim strong account-enumeration resistance.

If you can preserve all approved behavior and eliminate the oracle without weakening the POC flow, do so and test it; otherwise document the deliberate limitation as above.

## Minor fixes worth including

- Set the rate limiter rejection status to `429 Too Many Requests`.
- Extend the manual Supabase checklist to verify an `AspNetUserPasskeys` row is created for the registered user and remains usable after logout/restart, without exposing private key material.
- Add an explicit final ledger line such as `Final status: handoff pending` for Docker/GitHub/Render/Supabase/browser evidence; historical task completion lines may remain as task-level bookkeeping.

## Verification/report

Run and record:

```powershell
dotnet restore
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build
dotnet test PasskeyAuthn.Tests/PasskeyAuthn.Tests.csproj --configuration Release --no-build --filter FullyQualifiedName~RegistrationStateProtectorTests
dotnet format PasswordlessAuthn.slnx --verify-no-changes --no-restore
node --check PasskeyAuthn/wwwroot/js/passkey.js
node --test PasskeyAuthn.Tests/Browser/passkey-client-contract.test.mjs
docker build --tag passkeyauthn:final .
```

Docker/Testcontainers may be blocked locally; preserve real PostgreSQL tests and report exact failure rather than substituting a fake. Append a full fix report to `final-fix-report.md` in the SDD workspace with commands/output and concerns.

