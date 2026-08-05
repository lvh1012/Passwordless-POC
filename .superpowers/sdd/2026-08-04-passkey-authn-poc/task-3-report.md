# Task 3 Server Authentication Report

## Status

Implementation is complete in the current workspace. Focused unit tests, the schema-v3 composition test, Release build, and formatting verification pass. PostgreSQL-backed endpoint and persistence tests remain unexecuted because the local Docker daemon is unavailable; the real Testcontainers PostgreSQL fixture was preserved and no in-memory provider was introduced.

Commits: none. The workspace is not a Git repository, and no Git repository, branch, worktree, or commit was created.

## Implemented scope

- Added strongly typed `Passkey` settings with local, non-secret defaults and normal ASP.NET Core environment-variable override support:
  - `ServerDomain`: `localhost`
  - `ExpectedOrigin`: `https://localhost:5001`
  - `AuthenticatorTimeoutSeconds`: `300`
  - `MaxPasskeysPerUser`: `3`
  - `MaxDisplayNameLength`: `100`
- Added `RegistrationStateProtector` using ASP.NET Core Data Protection's time-limited protector. The protected envelope contains the user ID and absolute expiry; invalid, empty, malformed, modified, and expired tokens return `null` from `Unprotect`.
- Added the exact request/error contracts: `EmailRequest`, `CredentialRequest`, and `ApiError`.
- Added exactly four Passkey routes:
  - `POST /api/passkeys/register/options`
  - `POST /api/passkeys/register/complete`
  - `POST /api/passkeys/login/options`
  - `POST /api/passkeys/login/complete`
- Added `POST /api/auth/logout`, requiring authentication and antiforgery validation.
- Registration options normalize and validate email, enforce the configured display-name/passkey limits, create or reuse a pending user, set a five-minute expiry, call `MakePasskeyCreationOptionsAsync`, and issue the protected `passkey-registration` cookie with `HttpOnly`, `Secure`, and `SameSite=Lax`.
- Registration completion validates protected state and the pending database expiry, calls `PerformPasskeyAttestationAsync`, verifies the returned user entity, stores the built-in `UserPasskeyInfo` with `AddOrUpdatePasskeyAsync`, clears pending state, signs in, and returns only stable safe failures.
- Login options use normalized email lookup and return the same generic authentication error for a missing user or a user without a Passkey.
- Login completion passes only `Credential.GetRawText()` to `PasskeySignInAsync` and returns no framework failure detail.
- Added `AddAuthorization`, antiforgery header `X-CSRF-TOKEN`, a fixed-window rate-limit policy for all four Passkey routes, and middleware ordering `UseAuthentication` before `UseAuthorization`.
- Preserved Identity schema version 3, startup PostgreSQL migration, persisted Data Protection keys, and the production application-cookie `SecurePolicy.Always` configuration from Task 2.
- Added a test-only `SameAsRequest` application-cookie override because `TestServer` uses HTTP. Production configuration is unchanged.
- Added no third-party FIDO2/WebAuthn package. All ceremonies use the .NET 10 ASP.NET Core Identity Passkey APIs.

## Files

Created:

- `PasskeyAuthn/Configuration/PasskeySettings.cs`
- `PasskeyAuthn/Security/RegistrationStateProtector.cs`
- `PasskeyAuthn/Models/PasskeyRequests.cs`
- `PasskeyAuthn/Models/ApiError.cs`
- `PasskeyAuthn/Endpoints/PasskeyEndpointExtensions.cs`
- `PasskeyAuthn/Endpoints/AuthEndpointExtensions.cs`
- `PasskeyAuthn.Tests/Security/RegistrationStateProtectorTests.cs`
- `PasskeyAuthn.Tests/Endpoints/PasskeyEndpointTests.cs`
- `PasskeyAuthn.Tests/Endpoints/AuthEndpointTests.cs`
- `PasskeyAuthn.Tests/Infrastructure/AntiforgeryHttpClient.cs`

Modified:

- `PasskeyAuthn/Program.cs`
- `PasskeyAuthn/appsettings.json`
- `PasskeyAuthn.Tests/Infrastructure/PasskeyWebApplicationFactory.cs`

## TDD evidence

### Registration state RED

Command:

```powershell
dotnet test PasskeyAuthn.Tests/PasskeyAuthn.Tests.csproj --filter FullyQualifiedName~RegistrationStateProtectorTests
```

Observed before adding production code: exit code `1` with expected missing-production failures:

```text
CS0234: The type or namespace name 'Security' does not exist in the namespace 'PasskeyAuthn'
CS0246: The type or namespace name 'RegistrationStateProtector' could not be found
```

### Registration state GREEN

The same focused command passed after the minimal implementation:

```text
Passed! - Failed: 0, Passed: 5, Skipped: 0, Total: 5
```

The final Release-focused run also passed `5/5`.

### Endpoint RED

Tests and the real PostgreSQL host fixture were added before endpoint contracts or mappings. Command:

```powershell
dotnet test PasskeyAuthn.Tests/PasskeyAuthn.Tests.csproj --filter FullyQualifiedName~EndpointTests
```

Observed before adding endpoint production code: exit code `1` with the expected missing production contract:

```text
CS0234: The type or namespace name 'Models' does not exist in the namespace 'PasskeyAuthn'
```

### Endpoint post-implementation run

After implementation, the application and endpoint tests compile. The focused command discovered all nine endpoint tests, but all nine were stopped during `PostgresFixture` construction:

```text
DockerUnavailableException: Docker is either not running or misconfigured.
Failed to connect to Docker endpoint at 'npipe://./pipe/docker_engine'.
System.TimeoutException: The operation has timed out.
Failed: 9, Passed: 0, Total: 9
```

No endpoint assertion executed and failed; this is an environment failure before PostgreSQL startup.

## Endpoint test coverage preserved for Docker-enabled execution

- Invalid registration email returns safe `400 ApiError`.
- Unknown login email returns the generic authentication failure without echoing the email.
- A seeded existing Passkey returns `409` on duplicate public registration.
- Registration options create and reuse one pending user, return JSON, persist expiry, and set the secured state cookie.
- Registration completion without protected state returns safe `400`.
- Login completion with invalid credential JSON returns safe `400` or `401` without credential/parser detail.
- Missing antiforgery proof is rejected.
- Anonymous logout is rejected.
- Authenticated logout returns `204` and expires the application cookie.

The conflict seed uses `UserManager.AddOrUpdatePasskeyAsync` only to create an existing row. It does not simulate successful attestation or assertion.

## Verification summary

### Passed

```text
dotnet test PasskeyAuthn.Tests/PasskeyAuthn.Tests.csproj --configuration Release --filter FullyQualifiedName~RegistrationStateProtectorTests
Passed: 5, Failed: 0

dotnet test PasskeyAuthn.Tests/PasskeyAuthn.Tests.csproj --configuration Release --filter FullyQualifiedName~IdentitySchemaConfigurationTests
Passed: 1, Failed: 0

dotnet build PasswordlessAuthn.slnx --configuration Release
Build succeeded. 0 Warning(s), 0 Error(s)

dotnet format PasswordlessAuthn.slnx --verify-no-changes --no-restore
Exit code: 0
```

The schema composition test starts the application in EF design-time mode and confirms the built-in `IdentityUserPasskey<string>` entity remains registered.

### Docker-blocked

Exact required command:

```powershell
dotnet test --configuration Release
```

Result:

```text
Failed: 15, Passed: 6, Skipped: 0, Total: 21
```

All 15 failures share the same environment root cause at `PostgresFixture` construction: Testcontainers cannot connect to `npipe://./pipe/docker_engine` and times out. The affected tests are nine Task 3 endpoint tests, five existing PostgreSQL persistence tests, and one existing PostgreSQL-backed smoke test. The six non-container tests pass.

## API compatibility note

The installed runtime/reference pack is .NET `10.0.9`. Its `IdentityPasskeyOptions.ValidateOrigin` delegate returns `ValueTask<bool>`, so the required case-insensitive origin comparison is returned through `ValueTask.FromResult`. This preserves the required comparison and exact values while matching the installed .NET 10 API. The same reference pack exposes antiforgery requirement metadata through `RequireAntiforgeryTokenAttribute`; each POST route is marked with `required: true` and `UseAntiforgery` is enabled.

## Review and concerns

- No credential JSON, public key material, cookies, or connection strings are logged or included in API errors.
- Public endpoint extensions, contracts, settings, and state types have XML documentation. Security-sensitive and test-only decisions have concise rationale comments.
- Production `ServerDomain` and `ExpectedOrigin` must be overridden with `Passkey__ServerDomain` and `Passkey__ExpectedOrigin` on Render; checked-in values are local defaults only.
- Real endpoint behavior against PostgreSQL still needs one Docker-enabled run. Do not treat the current Docker failure as endpoint GREEN evidence.
- Successful attestation/assertion was intentionally not faked and remains a manual HTTPS ceremony for Task 8.
- No Razor UI, JavaScript, Docker, Render, GitHub Actions, or deployment work was added in this task.

## Fix round 1 (2026-08-04)

### Status

All three Important review findings were addressed in the current workspace. No Git repository or commit was created. The fixes remain within Task 3 server authentication scope; no UI or deployment work was changed.

### Changes

- Corrected both `PasskeySettings` code defaults and checked-in `appsettings.json` to the approved exact values `MaxPasskeysPerUser = 3` and `MaxDisplayNameLength = 100`.
- Added code-default/config binding tests plus endpoint boundaries for a valid 100-character email/display name and an invalid 101-character value.
- Replaced the two registration disclosure errors with one `409` contract: `ApiError("registration_conflict", "Registration cannot be started.")`. Existing-Passkey and limit-reached branches now return exactly the same body, which contains none of `account`, `passkey`, or `credential`.
- Preserved the existing unknown-login response: `401 ApiError("authentication_failed", "Authentication failed.")`.
- Removed typed `EmailRequest`/`CredentialRequest` parameters from the four Minimal API handlers. Each handler now uses built-in `HttpRequest.ReadFromJsonAsync<T>` inside the endpoint boundary and maps empty, malformed, unsupported, or JSON-null input to the existing stable `ApiError`; JSON parser details are not returned.
- Added no package and no custom FIDO2 implementation. The built-in .NET 10 ASP.NET Core Identity Passkey APIs and the real PostgreSQL Testcontainers fixture remain unchanged.

Changed or added for this review round:

- `PasskeyAuthn/Configuration/PasskeySettings.cs`
- `PasskeyAuthn/appsettings.json`
- `PasskeyAuthn/Endpoints/PasskeyEndpointExtensions.cs`
- `PasskeyAuthn.Tests/Configuration/PasskeySettingsTests.cs`
- `PasskeyAuthn.Tests/Endpoints/PasskeyEndpointTests.cs`

### Covering tests

- `Defaults_use_three_passkeys_and_one_hundred_character_display_names`
- `Appsettings_bind_approved_passkey_limits`
- `Registration_options_accept_email_at_display_name_limit`
- `Registration_options_reject_email_above_display_name_limit`
- `Registration_options_for_existing_passkey_returns_conflict`
- `Registration_options_at_passkey_limit_returns_generic_conflict`
- `Registration_options_with_malformed_json_returns_safe_invalid_email`
- `Login_options_with_empty_body_returns_safe_invalid_email`
- `Login_completion_with_malformed_json_returns_safe_authentication_failure`
- `Login_completion_with_empty_body_returns_safe_authentication_failure`
- `Login_completion_with_null_credential_returns_safe_authentication_failure`
- `Login_options_with_unknown_email_does_not_reveal_account_existence` continues covering the unchanged generic unknown-login behavior.

### TDD evidence

The settings tests were written before changing either production source.

```powershell
dotnet test PasskeyAuthn.Tests/PasskeyAuthn.Tests.csproj --filter FullyQualifiedName~PasskeySettingsTests
```

RED output with the old `5/64` values:

```text
Failed: 2, Passed: 0, Total: 2
Assert.Equal() Failure: Expected: 3; Actual: 5
```

The first assertion in each test stopped that run at `5`; the same tests also assert `100`, and the endpoint boundary test submits an exact 100-character email, so retaining `64` would also fail coverage.

GREEN output after the minimal settings/config change:

```text
Passed! - Failed: 0, Passed: 2, Skipped: 0, Total: 2
```

The endpoint assertions were added/changed before the endpoint implementation. Each required focused RED attempt compiled but was blocked before reaching the assertion because the unchanged real PostgreSQL fixture could not start:

```powershell
dotnet test PasskeyAuthn.Tests/PasskeyAuthn.Tests.csproj --filter FullyQualifiedName~Registration_options_accept_email_at_display_name_limit
dotnet test PasskeyAuthn.Tests/PasskeyAuthn.Tests.csproj --filter FullyQualifiedName~Registration_options_for_existing_passkey_returns_conflict
dotnet test PasskeyAuthn.Tests/PasskeyAuthn.Tests.csproj --filter FullyQualifiedName~Login_completion_with_malformed_json_returns_safe_authentication_failure
```

Output for each command:

```text
Failed: 1, Passed: 0, Total: 1
DockerUnavailableException: Docker is either not running or misconfigured.
Failed to connect to Docker endpoint at 'npipe://./pipe/docker_engine'.
System.TimeoutException: The operation has timed out.
```

This is test-first evidence but not assertion-level RED/GREEN evidence: `PostgresFixture` construction fails before the real host can execute the endpoint. No in-memory or fake persistence fallback was introduced.

### Final verification

Focused non-container unit tests:

```powershell
dotnet test PasskeyAuthn.Tests/PasskeyAuthn.Tests.csproj --filter FullyQualifiedName~PasskeySettingsTests
Passed! - Failed: 0, Passed: 2, Skipped: 0, Total: 2

dotnet test PasskeyAuthn.Tests/PasskeyAuthn.Tests.csproj --configuration Release --filter FullyQualifiedName~RegistrationStateProtectorTests
Passed! - Failed: 0, Passed: 5, Skipped: 0, Total: 5
```

All Task 3 endpoint tests compile and are discovered, but the real-host run is Docker-blocked before endpoint assertions:

```powershell
dotnet test PasskeyAuthn.Tests/PasskeyAuthn.Tests.csproj --configuration Release --filter FullyQualifiedName~EndpointTests
Failed: 17, Passed: 0, Skipped: 0, Total: 17
DockerUnavailableException: Docker is either not running or misconfigured.
Failed to connect to Docker endpoint at 'npipe://./pipe/docker_engine'.
```

Release build and formatting verification:

```powershell
dotnet build PasswordlessAuthn.slnx --configuration Release
Build succeeded. 0 Warning(s), 0 Error(s)

dotnet format PasswordlessAuthn.slnx --verify-no-changes --no-restore
Exit code: 0
```

Full required suite:

```powershell
dotnet test --configuration Release --logger "console;verbosity=quiet"
Failed: 23, Passed: 8, Skipped: 0, Total: 31
```

All 23 failures have the same Docker/Testcontainers root cause at `PostgresFixture` construction. The eight non-container tests pass. The PostgreSQL-backed tests remain pending a Docker-enabled run.

### Concerns

- Endpoint code compiles and the required real-host tests are present, but endpoint behavior is not assertion-GREEN until Docker can start the PostgreSQL fixture.
- The current Docker failure must not be interpreted as a product failure or a passing endpoint suite; it is an environment blocker before application test execution.
- Successful authenticator ceremonies remain outside these validation/error-path tests and were not faked.
