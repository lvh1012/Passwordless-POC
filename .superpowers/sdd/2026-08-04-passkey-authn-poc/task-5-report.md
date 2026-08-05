# Task 5 Runtime Hardening and Health Report

## Status

Implementation is in place and compiles cleanly. PostgreSQL/Testcontainers integration verification is blocked because the local Docker daemon is unavailable. No in-memory or SQLite replacement was introduced.

## Changes

- Replaced the temporary liveness route with `HealthEndpointExtensions.MapHealthEndpoints()`.
- Added an unauthenticated `/health` readiness route that calls `ApplicationDbContext.Database.CanConnectAsync` and returns `503 Service Unavailable` without a body when the database check fails. Exceptions are intentionally not returned or logged by the endpoint.
- Kept `DatabaseInitializer` as the startup gate; an empty `ConnectionStrings:Default` prevents the host from starting, so no false healthy endpoint is served.
- Configured `X-Forwarded-Proto` handling before HSTS and HTTPS redirection. The configuration clears the default proxy lists because Render proxy addresses are not static; the inline comment documents the trust boundary that the application must run behind Render's proxy.
- Enabled HSTS and HTTPS redirection outside `Development` and `Testing`.
- Bound the host to `http://0.0.0.0:${PORT:-8080}` using the required `UseUrls` call.
- Preserved existing Identity Passkey, Data Protection, cookie, antiforgery, rate-limit, UI, and endpoint registrations.

## TDD

- Added `HealthEndpointTests` and `RuntimeConfigurationTests` before changing `Program.cs` or adding the production health endpoint.
- The focused health run was blocked before endpoint execution by Testcontainers because Docker is unavailable. Tests remain real PostgreSQL tests and include healthy readiness, unavailable database readiness, no connection-detail disclosure, startup fail-closed behavior, production cookie settings, test-only cookie override isolation, and forwarded HTTPS handling.

## Verification

| Command | Result |
| --- | --- |
| `dotnet build PasswordlessAuthn.slnx --configuration Release --no-restore` | Passed: 0 warnings, 0 errors. |
| `dotnet test PasskeyAuthn.Tests/PasskeyAuthn.Tests.csproj --filter FullyQualifiedName~MissingDatabaseConfigurationTests` | Passed: 1/1. Confirms the host fails closed without `ConnectionStrings:Default`. |
| `dotnet test PasskeyAuthn.Tests/PasskeyAuthn.Tests.csproj --configuration Release --filter "FullyQualifiedName~PasskeySettingsTests|FullyQualifiedName~RegistrationStateProtectorTests"` | Passed: 7/7. |
| `dotnet format PasswordlessAuthn.slnx --verify-no-changes --no-restore` | Passed. |
| `dotnet test PasskeyAuthn.Tests/PasskeyAuthn.Tests.csproj --filter FullyQualifiedName~HealthEndpointTests` | Blocked: 4 tests could not create PostgreSQL containers. |
| `dotnet test --configuration Release` | Blocked: 34 Testcontainers/PostgreSQL tests failed before execution; 9 non-Docker tests passed (43 total). |
| `dotnet run --project PasskeyAuthn/PasskeyAuthn.csproj` | Expected fail-closed result: exit 1 with `InvalidOperationException: ConnectionStrings:Default must be configured.` No server process remained running. |

### Exact Docker blocker

The Testcontainers test output reports:

```text
DotNet.Testcontainers.Builders.DockerUnavailableException : Docker is either not running or misconfigured.
Failed to connect to Docker endpoint at 'npipe://./pipe/docker_engine'.
System.TimeoutException : The operation has timed out.
```

The direct Docker CLI check also reported that `npipe:////./pipe/dockerDesktopLinuxEngine` does not exist.

## Files changed

- `PasskeyAuthn/Program.cs`
- `PasskeyAuthn/Endpoints/HealthEndpointExtensions.cs`
- `PasskeyAuthn.Tests/Endpoints/HealthEndpointTests.cs`
- `PasskeyAuthn.Tests/Security/RuntimeConfigurationTests.cs`

## Commits

None. The workspace is not a Git repository, and Git was not initialized.

## Concerns and follow-up

1. Start Docker Desktop (or otherwise expose the configured Docker endpoint), then rerun the focused health/runtime tests and `dotnet test --configuration Release` to complete real PostgreSQL verification.
2. A normal local `dotnet run` cannot serve pages until a valid PostgreSQL connection string is supplied. This is intentional fail-closed startup behavior.
3. Clearing forwarded-header proxy allow-lists is appropriate only for the documented Render-only deployment topology. If the deployment topology expands, restrict the trusted proxy network explicitly.
