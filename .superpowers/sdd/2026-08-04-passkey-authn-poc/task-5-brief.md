# Task 5: Add health checks, forwarded HTTPS handling, and production-safe runtime behavior

## Files

- Modify `PasskeyAuthn/Program.cs`.
- Create `PasskeyAuthn/Endpoints/HealthEndpointExtensions.cs`.
- Create `PasskeyAuthn.Tests/Endpoints/HealthEndpointTests.cs`.
- Create `PasskeyAuthn.Tests/Security/RuntimeConfigurationTests.cs`.
- Modify existing test infrastructure only as needed for real PostgreSQL-backed health/runtime checks.

## Interfaces and constraints

- `HealthEndpointExtensions.MapHealthEndpoints(IEndpointRouteBuilder endpoints)` maps `GET /health`.
- `/health` returns `200` only when the application has a usable configured database and a lightweight database query succeeds; database/configuration failures return a non-success status without connection details, stack traces, or secrets.
- Production continues to fail startup when the required connection string is missing/unusable, through `DatabaseInitializer`; the health endpoint must not make an unconfigured app look healthy.
- Use Supabase PostgreSQL only; do not add an in-memory provider or second database.
- Preserve built-in Identity passkeys, Data Protection key persistence, secure cookies, antiforgery, and all current endpoint mappings.
- Every changed public class/API has XML docs; comments explain proxy/HTTPS or security decisions.

## TDD sequence

Write health/runtime tests first and run the focused health test to observe expected missing endpoint/runtime behavior. Then implement the minimum behavior, run focused tests and full build/test, and refactor only while green.

## Required tests

Add tests for:

- Healthy real PostgreSQL-backed `/health` response.
- Missing connection string/startup configuration failure without serving a false healthy response.
- Configured application cookie options remain `HttpOnly`, `SecurePolicy.Always`, and `SameSite.Lax` in production; test-only `SameAsRequest` override remains isolated to test factory.
- The health response body does not expose connection details.
- Forwarded HTTPS metadata is handled before HTTPS-sensitive middleware (static/config inspection or focused host test as appropriate).

If Docker is unavailable, preserve these as real Testcontainers PostgreSQL tests and report the exact environment-blocked result; do not replace them with SQLite/in-memory.

## Runtime implementation requirements

Implement a lightweight readiness handler that resolves `ApplicationDbContext` and calls a bounded/simple database check such as `CanConnectAsync` or a no-op query. Return a generic non-success result if it fails; do not include exception messages or connection strings.

Configure forwarded headers for Render's TLS-terminating proxy before HTTPS-sensitive middleware. The configuration must accept the forwarded proto needed for `Request.Scheme` to become `https` when Render terminates TLS. Document/comment the trust boundary because this app is intended to run behind Render's proxy.

Enable HTTPS redirection and HSTS outside local Development and test environments. Keep local/test host requests usable for automated tests while production deployed traffic is HTTPS-only.

Bind Kestrel/host to Render's port using:

```csharp
builder.WebHost.UseUrls($"http://0.0.0.0:{Environment.GetEnvironmentVariable("PORT") ?? "8080"}");
```

Use the Render `PORT` value when present and `8080` as the local/container fallback. Do not log the database connection string or secrets.

Replace the temporary `app.MapGet("/health", ...)` endpoint with `app.MapHealthEndpoints()` while preserving an unauthenticated health route suitable for Render's health check.

## Verification

Run:

```powershell
dotnet test PasskeyAuthn.Tests/PasskeyAuthn.Tests.csproj --filter FullyQualifiedName~HealthEndpointTests
dotnet test --configuration Release
dotnet run --project PasskeyAuthn/PasskeyAuthn.csproj
dotnet format PasswordlessAuthn.slnx --verify-no-changes --no-restore
```

Verify local pages and that an invalid/missing database configuration fails startup rather than serving the app. Stop the local process after verification. If Docker blocks integration tests, still require Release build, unit/security tests, and honest report of the Docker endpoint failure.

## Scope boundary

Do not implement Dockerfile, Render Blueprint, GitHub Actions, deployment docs, or final manual deployment in this task. Do not initialize Git or create commits.

