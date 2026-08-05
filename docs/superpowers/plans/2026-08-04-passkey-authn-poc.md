# Passkey Authentication POC Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build and deploy a .NET 10 browser-based Passkey registration/login POC backed by Supabase PostgreSQL, hosted on Render Free Web Service, and gated by GitHub Actions CI/CD.

**Architecture:** Convert the existing project into an ASP.NET Core Web App using Razor Pages, vanilla JavaScript WebAuthn APIs, and ASP.NET Core Identity's built-in .NET 10 passkey support. Store Identity and Data Protection state in Supabase PostgreSQL through EF Core/Npgsql, package the app in a multi-stage Docker image, and trigger Render deployment from GitHub Actions after validation succeeds.

**Tech Stack:** .NET 10, ASP.NET Core Identity passkeys, Razor Pages, vanilla JavaScript WebAuthn API, EF Core, Npgsql, Supabase PostgreSQL, Docker, Render Free Web Service, GitHub Actions, xUnit, ASP.NET Core integration testing, Testcontainers PostgreSQL.

## Global Constraints

- Use `.NET 10` and keep the project target framework at `net10.0`.
- Use Supabase PostgreSQL as the persistent database; do not add a second application database.
- Use ASP.NET Core Identity's built-in passkey support; do not add a third-party FIDO2/WebAuthn verification library.
- Use email as the unique user identifier, without email ownership verification or email delivery.
- Use Razor Pages and vanilla JavaScript; do not add a SPA framework.
- Use Render Free Web Service with the platform-provided `onrender.com` HTTPS subdomain.
- WebAuthn `Origin` is `https://<render-service>.onrender.com`; WebAuthn `RP ID` and `ServerDomain` are `<render-service>.onrender.com`.
- Persist Identity data and ASP.NET Core Data Protection keys in Supabase PostgreSQL.
- Apply EF Core migrations during application startup for this single-instance POC.
- GitHub Actions must run restore, build, test, and Docker image validation before a `main` deployment.
- Store `RENDER_DEPLOY_HOOK_URL` only as a GitHub Actions secret; store database and runtime secrets only in Render environment variables.
- Apply antiforgery protection to state-changing POST endpoints and use secure authentication cookies.
- Do not log credential JSON, public key material, authentication cookies, or database connection strings.
- Every changed public class, endpoint contract, and exported JavaScript API must have documentation comments appropriate to its language.
- Code comments must explain non-obvious security, persistence, serialization, or platform decisions rather than restating code.

---

## File map

### Existing files to modify or remove

- Modify: `PasskeyAuthn/PasskeyAuthn.csproj` — convert to the ASP.NET Core Web SDK and add runtime, EF Core, Identity, Data Protection, and test-compatible dependencies.
- Modify: `PasswordlessAuthn.slnx` — add the new test project.
- Delete: `PasskeyAuthn/Class1.cs` — remove the empty scaffold type after the web host is created.

### Application files to create

- Create: `PasskeyAuthn/Program.cs` — service registration, middleware pipeline, endpoint/page mapping, and startup migration.
- Create: `PasskeyAuthn/Data/ApplicationUser.cs` — Identity user and registration-pending state.
- Create: `PasskeyAuthn/Data/ApplicationDbContext.cs` — Identity and Data Protection EF Core context.
- Create: `PasskeyAuthn/Data/DatabaseInitializer.cs` — apply migrations and fail startup when required database configuration is unavailable.
- Create: `PasskeyAuthn/Configuration/PasskeySettings.cs` — strongly typed Render hostname, RP ID, origin, timeout, and passkey limits.
- Create: `PasskeyAuthn/Security/RegistrationStateProtector.cs` — protect and unprotect the short-lived pending registration user ID.
- Create: `PasskeyAuthn/Models/PasskeyRequests.cs` — email and serialized credential request contracts.
- Create: `PasskeyAuthn/Models/ApiError.cs` — safe, stable client-facing error contract.
- Create: `PasskeyAuthn/Endpoints/PasskeyEndpointExtensions.cs` — registration and login options/complete endpoints.
- Create: `PasskeyAuthn/Endpoints/AuthEndpointExtensions.cs` — logout endpoint.
- Create: `PasskeyAuthn/Endpoints/HealthEndpointExtensions.cs` — liveness/readiness endpoint with database check.
- Create: `PasskeyAuthn/Pages/_ViewImports.cshtml` — Razor namespaces and tag helpers.
- Create: `PasskeyAuthn/Pages/_ViewStart.cshtml` — shared Razor layout selection.
- Create: `PasskeyAuthn/Pages/Shared/_Layout.cshtml` — shared layout, antiforgery token metadata, and scripts.
- Create: `PasskeyAuthn/Pages/Index.cshtml` — login UI.
- Create: `PasskeyAuthn/Pages/Register.cshtml` — registration UI.
- Create: `PasskeyAuthn/Pages/Dashboard.cshtml` — protected authenticated UI.
- Create: `PasskeyAuthn/wwwroot/js/passkey.js` — WebAuthn browser orchestration and error mapping.
- Create: `PasskeyAuthn/wwwroot/css/site.css` — minimal accessible POC styling.
- Create: `PasskeyAuthn/appsettings.json` — non-secret defaults and logging configuration.
- Create: `PasskeyAuthn/appsettings.Development.json` — local development defaults.

### Test files to create

- Create: `PasskeyAuthn.Tests/PasskeyAuthn.Tests.csproj` — xUnit, ASP.NET Core test host, and PostgreSQL Testcontainers dependencies.
- Create: `PasskeyAuthn.Tests/Infrastructure/PasskeyWebApplicationFactory.cs` — test host with isolated PostgreSQL configuration.
- Create: `PasskeyAuthn.Tests/Infrastructure/PostgresFixture.cs` — reusable PostgreSQL container fixture.
- Create: `PasskeyAuthn.Tests/Endpoints/HealthEndpointTests.cs` — health and database readiness tests.
- Create: `PasskeyAuthn.Tests/Endpoints/PasskeyEndpointTests.cs` — validation, authorization, conflict, and safe-error tests.
- Create: `PasskeyAuthn.Tests/Endpoints/AuthEndpointTests.cs` — logout and protected endpoint behavior.
- Create: `PasskeyAuthn.Tests/Security/RegistrationStateProtectorTests.cs` — expiry, tampering, and round-trip tests.
- Create: `PasskeyAuthn.Tests/Pages/PageSmokeTests.cs` — login, registration, and protected-page response tests.

### Deployment files to create

- Create: `Dockerfile` — reproducible multi-stage .NET 10 production image.
- Create: `.dockerignore` — exclude build output, local secrets, and repository metadata from the Docker context.
- Create: `render.yaml` — Render Web Service configuration, health check, free plan, and disabled commit auto-deploy.
- Create: `.github/workflows/ci.yml` — restore, build, test, Docker validation, and Render deploy hook.
- Create: `docs/deployment.md` — Supabase, Render, GitHub secret, and manual acceptance setup.

## Implementation Tasks

### Task 1: Convert the skeleton into a testable ASP.NET Core web solution

**Files:**
- Modify: `PasskeyAuthn/PasskeyAuthn.csproj`
- Modify: `PasswordlessAuthn.slnx`
- Delete: `PasskeyAuthn/Class1.cs`
- Create: `PasskeyAuthn/Program.cs`
- Create: `PasskeyAuthn.Tests/PasskeyAuthn.Tests.csproj`
- Create: `PasskeyAuthn.Tests/SmokeTests.cs`

**Interfaces:**
- Produces a `WebApplication` entry point exposed to `WebApplicationFactory<Program>` through a public partial `Program` declaration.
- Produces a test project runnable with `dotnet test` from the solution root.

- [ ] **Step 1: Create the failing host smoke test**

Add a test that creates `WebApplicationFactory<Program>`, sends `GET /health`, and expects HTTP 200. The test should initially fail because the web host and endpoint do not exist.

~~~csharp
public sealed class SmokeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public SmokeTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_endpoint_returns_success()
    {
        using var response = await _client.GetAsync("/health");

        Assert.True(response.IsSuccessStatusCode);
    }
}
~~~

- [ ] **Step 2: Run the focused test and verify failure**

Run:

~~~powershell
dotnet test PasskeyAuthn.Tests/PasskeyAuthn.Tests.csproj --filter FullyQualifiedName~SmokeTests
~~~

Expected: FAIL because `Program` and `/health` are not implemented.

- [ ] **Step 3: Convert the project SDK and add the initial web host**

Change the project SDK to `Microsoft.NET.Sdk.Web`, preserve `net10.0`, `Nullable`, and `ImplicitUsings`, and add the packages needed by later tasks: `Microsoft.AspNetCore.Identity.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Design`, `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore`, `Npgsql.EntityFrameworkCore.PostgreSQL`, and the matching .NET 10-compatible versions.

Create `Program.cs` with a minimal `WebApplication` that enables Razor Pages, static files, routing, and a temporary `/health` endpoint returning `Results.Ok()`. Add `public partial class Program { }` so the test host can discover it.

- [ ] **Step 4: Add the test project to the solution**

Create the xUnit project, add references to the application project and `Microsoft.AspNetCore.Mvc.Testing`, then update `PasswordlessAuthn.slnx` to include `PasskeyAuthn.Tests/PasskeyAuthn.Tests.csproj`.

- [ ] **Step 5: Run the focused test and verify success**

Run the same focused `dotnet test` command. Expected: PASS with the temporary health endpoint.

- [ ] **Step 6: Commit the independently testable scaffold**

~~~powershell
git add PasskeyAuthn/PasskeyAuthn.csproj PasskeyAuthn/Program.cs PasskeyAuthn.Tests PasswordlessAuthn.slnx
git rm PasskeyAuthn/Class1.cs
git commit -m "chore: scaffold ASP.NET Core passkey app"
~~~

The current workspace has no Git repository; do not run the commit command until the repository exists.

### Task 2: Add Identity, Supabase PostgreSQL persistence, and Data Protection key storage

**Files:**
- Modify: `PasskeyAuthn/Program.cs`
- Modify: `PasskeyAuthn/PasskeyAuthn.csproj`
- Create: `PasskeyAuthn/Data/ApplicationUser.cs`
- Create: `PasskeyAuthn/Data/ApplicationDbContext.cs`
- Create: `PasskeyAuthn/Data/DatabaseInitializer.cs`
- Create: `PasskeyAuthn/appsettings.json`
- Create: `PasskeyAuthn/appsettings.Development.json`
- Create: `PasskeyAuthn.Tests/Infrastructure/PostgresFixture.cs`
- Create: `PasskeyAuthn.Tests/Infrastructure/PasskeyWebApplicationFactory.cs`
- Create: `PasskeyAuthn.Tests/Data/IdentityPersistenceTests.cs`
- Create: `PasskeyAuthn/Migrations/` — EF-generated files from migration `InitialIdentityAndDataProtection`.

**Interfaces:**
- `ApplicationUser : IdentityUser` exposes `DateTimeOffset? PasskeyRegistrationExpiresAt`.
- `ApplicationDbContext : IdentityDbContext<ApplicationUser>, IDataProtectionKeyContext` exposes the Identity sets and `DbSet<DataProtectionKey> DataProtectionKeys`.
- `DatabaseInitializer.InitializeAsync(IServiceProvider, CancellationToken)` applies pending migrations and throws a startup error for an unusable database.
- `PasskeyWebApplicationFactory` replaces the production connection string with the fixture's PostgreSQL connection string.

- [ ] **Step 1: Write persistence tests before the database implementation**

Add tests that:

1. Create an `ApplicationUser` with a unique email and persist it.
2. Confirm the user can be loaded by normalized email.
3. Confirm the Data Protection key table is present after migration.
4. Confirm the application startup initializer applies pending migrations.

Use `PostgresFixture` backed by `Testcontainers.PostgreSql`; do not use a fake in-memory provider because the production database is PostgreSQL.

- [ ] **Step 2: Run the persistence tests and verify failure**

Run:

~~~powershell
dotnet test PasskeyAuthn.Tests/PasskeyAuthn.Tests.csproj --filter FullyQualifiedName~IdentityPersistenceTests
~~~

Expected: FAIL because `ApplicationDbContext`, the Identity model, and the fixture do not exist.

- [ ] **Step 3: Implement the Identity user and EF Core context**

Create `ApplicationUser` with only the pending-registration expiry property required by the approved design. Create `ApplicationDbContext` inheriting from `IdentityDbContext<ApplicationUser>` and implementing `IDataProtectionKeyContext`.

Configure Identity with `RequireUniqueEmail = true` and `RequireConfirmedAccount = false`; the POC uses email as an identifier but does not claim ownership verification.

- [ ] **Step 4: Configure EF Core, Identity stores, and Data Protection**

In `Program.cs`, register:

~~~csharp
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddSignInManager()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddIdentityCookies();

builder.Services.AddDataProtection()
    .PersistKeysToDbContext<ApplicationDbContext>();
~~~

Configure the application cookie with `HttpOnly = true`, `SecurePolicy = CookieSecurePolicy.Always`, and `SameSite = SameSiteMode.Lax`.

Configure the test host to use an explicit `Testing` environment and `CookieSecurePolicy.SameAsRequest` only inside `PasskeyWebApplicationFactory`; production and Render must continue to use `CookieSecurePolicy.Always`.

- [ ] **Step 5: Add configuration defaults without secrets**

Create `appsettings.json` with a named `ConnectionStrings:Default` entry that is empty or explicitly documented as environment-provided, and standard logging levels. Keep actual Supabase credentials out of both appsettings files.

- [ ] **Step 6: Create and apply the initial migration**

Run:

~~~powershell
dotnet ef migrations add InitialIdentityAndDataProtection --project PasskeyAuthn/PasskeyAuthn.csproj --output-dir Migrations
~~~

Implement `DatabaseInitializer.InitializeAsync` using `Database.MigrateAsync()`, call it in a scoped startup block after the app is built, and make startup fail rather than serving an app that cannot persist authentication data.

- [ ] **Step 7: Run persistence tests and verify success**

Run the focused persistence tests and then:

~~~powershell
dotnet test --configuration Release
~~~

Expected: PASS, with Identity and Data Protection tables created in the ephemeral PostgreSQL container.

- [ ] **Step 8: Commit the persistence slice**

~~~powershell
git add PasskeyAuthn PasskeyAuthn.Tests
git commit -m "feat: add Identity PostgreSQL persistence"
~~~

### Task 3: Implement Passkey settings, registration state, and server endpoints

**Files:**
- Create: `PasskeyAuthn/Configuration/PasskeySettings.cs`
- Create: `PasskeyAuthn/Security/RegistrationStateProtector.cs`
- Create: `PasskeyAuthn/Models/PasskeyRequests.cs`
- Create: `PasskeyAuthn/Models/ApiError.cs`
- Create: `PasskeyAuthn/Endpoints/PasskeyEndpointExtensions.cs`
- Create: `PasskeyAuthn/Endpoints/AuthEndpointExtensions.cs`
- Modify: `PasskeyAuthn/Program.cs`
- Create: `PasskeyAuthn.Tests/Security/RegistrationStateProtectorTests.cs`
- Create: `PasskeyAuthn.Tests/Endpoints/PasskeyEndpointTests.cs`
- Create: `PasskeyAuthn.Tests/Endpoints/AuthEndpointTests.cs`

**Interfaces:**
- `PasskeySettings` contains `ServerDomain`, `ExpectedOrigin`, `AuthenticatorTimeoutSeconds`, `MaxPasskeysPerUser`, and `MaxDisplayNameLength`.
- `RegistrationStateProtector.Protect(string userId, DateTimeOffset expiresAt)` returns a protected token; `Unprotect(string token)` returns `RegistrationState?`, where `RegistrationState` is `sealed record RegistrationState(string UserId, DateTimeOffset ExpiresAt)` and `null` means invalid or expired.
- `PasskeyEndpointExtensions.MapPasskeyEndpoints(IEndpointRouteBuilder endpoints)` maps the four `/api/passkeys/*` endpoints.
- `AuthEndpointExtensions.MapAuthEndpoints(IEndpointRouteBuilder endpoints)` maps `POST /api/auth/logout`.
- `EmailRequest` contains `string Email`.
- `CredentialRequest` contains a `JsonElement Credential`; endpoints pass `Credential.GetRawText()` to ASP.NET Core Identity.
- `ApiError` contains stable `Code` and safe `Message` values.

- [ ] **Step 1: Write registration-state tests**

Cover:

- A valid protected user ID round-trips.
- An expired token is rejected.
- A modified token is rejected.
- Empty user IDs are rejected.

- [ ] **Step 2: Run the focused security test and verify failure**

~~~powershell
dotnet test PasskeyAuthn.Tests/PasskeyAuthn.Tests.csproj --filter FullyQualifiedName~RegistrationStateProtectorTests
~~~

Expected: FAIL because the protector does not exist.

- [ ] **Step 3: Implement settings and protected registration state**

Bind `PasskeySettings` from `Passkey` configuration. Use ASP.NET Core Data Protection to protect a token containing the user ID and a five-minute expiry. Keep the token in an `HttpOnly`, `Secure`, `SameSite=Lax` registration cookie; never put the user ID in an unprotected client value.

- [ ] **Step 4: Write endpoint validation tests**

Add integration tests for:

- Invalid email returns `400` with a safe `ApiError`.
- Unknown login email returns a generic authentication failure without revealing account existence.
- Duplicate registration for an account with an existing passkey returns `409`.
- Registration options create or reuse a pending user and return JSON.
- Registration completion without a valid registration state returns `400`.
- Login completion with invalid credential JSON returns a safe `401`/`400` response.
- Logout clears the authentication cookie.

Do not fake cryptographic attestation/assertion success in these tests; the built-in Identity implementation is covered by the framework and the real ceremony is verified manually in Task 8.

- [ ] **Step 5: Run endpoint tests and verify failure**

~~~powershell
dotnet test PasskeyAuthn.Tests/PasskeyAuthn.Tests.csproj --filter FullyQualifiedName~EndpointTests
~~~

Expected: FAIL because the endpoint mappings and contracts do not exist.

- [ ] **Step 6: Configure built-in Identity passkey options**

Register `IdentityPasskeyOptions` with:

~~~csharp
options.ServerDomain = settings.ServerDomain;
options.UserVerificationRequirement = "required";
options.ResidentKeyRequirement = "preferred";
options.AuthenticatorTimeout = TimeSpan.FromSeconds(settings.AuthenticatorTimeoutSeconds);
options.ValidateOrigin = context =>
    string.Equals(context.Origin, settings.ExpectedOrigin, StringComparison.OrdinalIgnoreCase);
~~~

Use `ServerDomain` only for the RP ID and `ExpectedOrigin` only for the full HTTPS origin.

- [ ] **Step 7: Implement registration endpoints**

`POST /api/passkeys/register/options` must:

1. Validate and normalize the email.
2. Find the existing user by normalized email.
3. Reject an existing user with a passkey; reuse an uncredentialed pending user.
4. Create a new `ApplicationUser` when needed.
5. Reject a user already at `MaxPasskeysPerUser`.
6. Set `PasskeyRegistrationExpiresAt` to five minutes in the future.
7. Call `SignInManager.MakePasskeyCreationOptionsAsync(new PasskeyUserEntity { Id = user.Id, Name = email, DisplayName = email })`.
8. Protect the user ID in the registration cookie.
9. Return the JSON options with `application/json` content type.

`POST /api/passkeys/register/complete` must:

1. Read and validate the protected registration cookie.
2. Load the associated user.
3. Call `SignInManager.PerformPasskeyAttestationAsync(credentialJson)`.
4. Return a safe `400` when attestation verification fails.
5. Call `UserManager.AddOrUpdatePasskeyAsync(user, attestationResult.Passkey)`.
6. Clear the pending expiry and registration cookie.
7. Sign in with the application cookie and return success.

- [ ] **Step 8: Implement login and logout endpoints**

`POST /api/passkeys/login/options` must find the user by normalized email, call `SignInManager.MakePasskeyRequestOptionsAsync(user)`, and return generic failure behavior for unknown users.

`POST /api/passkeys/login/complete` must call `SignInManager.PasskeySignInAsync(credentialJson)` and return success only when `SignInResult.Succeeded` is true. Do not include the failure reason in the response.

`POST /api/auth/logout` must call `SignInManager.SignOutAsync()` and return `204 No Content`.

- [ ] **Step 9: Add antiforgery, rate limiting, and endpoint authorization metadata**

Register `AddAuthorization()`, configure the antiforgery header as `X-CSRF-TOKEN`, require antiforgery on all state-changing endpoints, add a fixed-window rate limiter for the four Passkey endpoints, and require authorization on the dashboard page rather than relying on UI hiding. Add `UseAuthentication()` before `UseAuthorization()` in the middleware pipeline.

- [ ] **Step 10: Run endpoint and security tests and verify success**

~~~powershell
dotnet test --configuration Release
~~~

Expected: PASS for registration-state, endpoint validation, logout, and protected request tests.

- [ ] **Step 11: Commit the server authentication slice**

~~~powershell
git add PasskeyAuthn PasskeyAuthn.Tests
git commit -m "feat: add passkey authentication endpoints"
~~~

### Task 4: Build the browser UI and WebAuthn client

**Files:**
- Create: `PasskeyAuthn/Pages/_ViewImports.cshtml`
- Create: `PasskeyAuthn/Pages/_ViewStart.cshtml`
- Create: `PasskeyAuthn/Pages/Shared/_Layout.cshtml`
- Create: `PasskeyAuthn/Pages/Index.cshtml`
- Create: `PasskeyAuthn/Pages/Register.cshtml`
- Create: `PasskeyAuthn/Pages/Dashboard.cshtml`
- Create: `PasskeyAuthn/wwwroot/js/passkey.js`
- Create: `PasskeyAuthn/wwwroot/css/site.css`
- Create: `PasskeyAuthn.Tests/Pages/PageSmokeTests.cs`

**Interfaces:**
- `window.PasskeyAuth.startLogin(email)` starts the login ceremony and redirects to `/dashboard` on success.
- `window.PasskeyAuth.startRegistration(email)` starts the registration ceremony and redirects to `/dashboard` on success.
- `window.PasskeyAuth.serializeCredential(credential)` returns the JSON-safe credential shape expected by `CredentialRequest`.
- `window.PasskeyAuth.mapError(error)` returns a safe user-facing message.

- [ ] **Step 1: Write page smoke tests**

Add tests that:

- `GET /` returns 200 and contains the email field and login button.
- `GET /register` returns 200 and contains the registration action.
- `GET /dashboard` redirects an anonymous client to the login page.
- The shared layout contains the antiforgery meta tag and `passkey.js` reference.

- [ ] **Step 2: Run page tests and verify failure**

~~~powershell
dotnet test PasskeyAuthn.Tests/PasskeyAuthn.Tests.csproj --filter FullyQualifiedName~PageSmokeTests
~~~

Expected: FAIL because the Razor pages do not exist.

- [ ] **Step 3: Implement the shared Razor layout and antiforgery token**

Render a request token into a meta element named `csrf-token`, configure the shared navigation, and load `passkey.js` with `defer`. Use a consistent error/status element on login and registration pages.

- [ ] **Step 4: Implement login, registration, and dashboard pages**

The login page must include an email input and `Sign in with passkey` button. The registration page must include an email input and `Create passkey` button. The dashboard must use Razor authorization metadata and display the authenticated user's email through the current claims principal. The logout button must submit through `fetch` as a POST with the antiforgery header.

- [ ] **Step 5: Implement WebAuthn option parsing and credential serialization**

In `passkey.js`:

1. Read the CSRF token from the layout meta element.
2. POST the email to the options endpoint.
3. Parse options with `PublicKeyCredential.parseCreationOptionsFromJSON()` or `parseRequestOptionsFromJSON()` when available.
4. Call `navigator.credentials.create()` for registration and `navigator.credentials.get()` for login.
5. Serialize `rawId`, `clientDataJSON`, `attestationObject`, `authenticatorData`, `signature`, and `userHandle` to base64url without padding.
6. POST the credential JSON to the complete endpoint.
7. Redirect to `/dashboard` only after a successful response.
8. Display safe messages for missing WebAuthn support, `NotAllowedError`, `AbortError`, `InvalidStateError`, challenge failures, and server errors.

Include the manual serialization fallback because some password managers do not implement `PublicKeyCredential.toJSON()` correctly.

- [ ] **Step 6: Add accessible styling and browser states**

Create minimal CSS for a centered form, visible focus states, disabled buttons during ceremonies, status messages, and error messages. Do not introduce a CSS framework.

- [ ] **Step 7: Run page tests and verify success**

~~~powershell
dotnet test --configuration Release
~~~

Expected: PASS for page rendering, protected-page redirect, and shared antiforgery/script assertions.

- [ ] **Step 8: Commit the browser UI slice**

~~~powershell
git add PasskeyAuthn PasskeyAuthn.Tests
git commit -m "feat: add passkey browser UI"
~~~

### Task 5: Add health checks, forwarded HTTPS handling, and production-safe runtime behavior

**Files:**
- Modify: `PasskeyAuthn/Program.cs`
- Create: `PasskeyAuthn/Endpoints/HealthEndpointExtensions.cs`
- Create: `PasskeyAuthn.Tests/Endpoints/HealthEndpointTests.cs`
- Create: `PasskeyAuthn.Tests/Security/RuntimeConfigurationTests.cs`

**Interfaces:**
- `HealthEndpointExtensions.MapHealthEndpoints(IEndpointRouteBuilder endpoints)` maps `GET /health`.
- `/health` returns `200` only when the application is configured and the database can respond to a lightweight query; otherwise it returns a non-success response without exposing connection details.

- [ ] **Step 1: Write health and runtime tests**

Add tests for a healthy database response, a missing connection string failure, and configured `Secure`/`HttpOnly` authentication cookie settings.

- [ ] **Step 2: Run focused health tests and verify failure**

~~~powershell
dotnet test PasskeyAuthn.Tests/PasskeyAuthn.Tests.csproj --filter FullyQualifiedName~HealthEndpointTests
~~~

Expected: FAIL until the readiness endpoint and runtime configuration are implemented.

- [ ] **Step 3: Implement health and forwarded-header middleware**

Map `/health` to a lightweight database check. Configure forwarded headers for Render's TLS-terminating proxy before HTTPS-sensitive middleware, enable HTTPS redirection and HSTS outside local development, and ensure the app binds to `0.0.0.0` using Render's `PORT` value. Use `builder.WebHost.UseUrls($"http://0.0.0.0:{Environment.GetEnvironmentVariable("PORT") ?? "8080"}")` so the Docker container and Render agree on the listening port.

- [ ] **Step 4: Verify runtime behavior**

Run:

~~~powershell
dotnet test --configuration Release
dotnet run --project PasskeyAuthn/PasskeyAuthn.csproj
~~~

Verify that the local app serves pages, rejects an invalid database configuration at startup, and does not emit secrets in logs.

- [ ] **Step 5: Commit the runtime hardening slice**

~~~powershell
git add PasskeyAuthn PasskeyAuthn.Tests
git commit -m "feat: add health checks and secure runtime configuration"
~~~

### Task 6: Add Docker and Render deployment configuration

**Files:**
- Create: `Dockerfile`
- Create: `.dockerignore`
- Create: `render.yaml`
- Create: `docs/deployment.md`
- Modify: `PasskeyAuthn/Program.cs` — consume the Render `PORT` environment variable as defined in Task 5.

**Interfaces:**
- The Docker image starts with `dotnet PasskeyAuthn.dll`.
- The Render service exposes `/health` and uses a free web-service plan.
- Runtime configuration is supplied through Render environment variables, not checked-in files.

- [ ] **Step 1: Write the Docker build verification command**

Define the verification target before adding the image:

~~~powershell
docker build --tag passkeyauthn:poc .
docker image inspect passkeyauthn:poc
~~~

Expected before implementation: FAIL because `Dockerfile` does not exist.

- [ ] **Step 2: Create the multi-stage Dockerfile**

Use this structure and keep the build context at the repository root:

~~~dockerfile
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
~~~

Add `.dockerignore` entries for `.git`, `.idea`, `bin`, `obj`, local environment files, and test artifacts.

- [ ] **Step 3: Add Render Blueprint configuration**

Create `render.yaml` with a Docker web service, `free` plan, health check `/health`, and commit auto-deploy disabled. Declare the following environment keys as externally supplied values without putting secrets in the file:

~~~yaml
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
~~~

- [ ] **Step 4: Document Supabase and Render setup**

In `docs/deployment.md`, document:

1. Create a Supabase project and obtain the Shared Pooler session-mode PostgreSQL connection string.
2. Require SSL in the connection string/configuration.
3. Create the Render Web Service from the repository/Blueprint.
4. Set `ConnectionStrings__Default` in Render.
5. Set `Passkey__ServerDomain` to `<service>.onrender.com`.
6. Set `Passkey__ExpectedOrigin` to `https://<service>.onrender.com`.
7. Copy the Render Deploy Hook URL into the GitHub `RENDER_DEPLOY_HOOK_URL` secret.
8. Confirm `/health` is healthy before manual Passkey testing.

- [ ] **Step 5: Build and smoke-test the image**

Run:

~~~powershell
docker build --tag passkeyauthn:poc .
docker run --rm --env PORT=10000 --publish 10000:10000 passkeyauthn:poc
~~~

With a valid local database configuration, call `GET http://localhost:10000/health` and expect a success response. Stop the container after verification.

- [ ] **Step 6: Commit the container/deployment slice**

~~~powershell
git add Dockerfile .dockerignore render.yaml docs/deployment.md PasskeyAuthn
git commit -m "build: add Render Docker deployment"
~~~

### Task 7: Add GitHub Actions CI and Render deployment trigger

**Files:**
- Create: `.github/workflows/ci.yml`
- Modify: `docs/deployment.md` if the workflow setup requires an additional documented secret.

**Interfaces:**
- Workflow name: `CI`.
- Required secret: `RENDER_DEPLOY_HOOK_URL`.
- Deploy condition: only a successful push to `refs/heads/main`.

- [ ] **Step 1: Write the workflow validation target**

Before adding the workflow, define the local equivalent:

~~~powershell
dotnet restore
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build
docker build --tag passkeyauthn:ci .
~~~

Expected before implementation: the workflow file is absent and GitHub cannot run the required checks.

- [ ] **Step 2: Create the GitHub Actions workflow**

Create `.github/workflows/ci.yml` with this behavior:

~~~yaml
name: CI

on:
  pull_request:
  push:
    branches: [main]

permissions:
  contents: read

jobs:
  validate:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 10.0.x

      - name: Restore
        run: dotnet restore

      - name: Build
        run: dotnet build --configuration Release --no-restore

      - name: Test
        run: dotnet test --configuration Release --no-build

      - name: Build Docker image
        run: docker build --tag passkeyauthn:ci .

      - name: Trigger Render deploy
        if: github.event_name == 'push' && github.ref == 'refs/heads/main'
        env:
          RENDER_DEPLOY_HOOK_URL: ${{ secrets.RENDER_DEPLOY_HOOK_URL }}
        run: |
          test -n "$RENDER_DEPLOY_HOOK_URL"
          curl --fail-with-body --request POST "$RENDER_DEPLOY_HOOK_URL"
~~~

Use GitHub-hosted Linux runners so Docker is available for the image validation and PostgreSQL Testcontainers tests.

- [ ] **Step 3: Validate the workflow locally**

Run the local equivalent from Step 1, then inspect the workflow YAML for:

- PRs never calling the Render hook.
- Non-`main` pushes never calling the Render hook.
- The Render URL coming only from a secret.
- No Supabase connection string in workflow logs or checked-in files.

- [ ] **Step 4: Configure the GitHub and Render integration**

Connect Render to the repository, set auto-deploy to off, create the Deploy Hook, and add it as the `RENDER_DEPLOY_HOOK_URL` repository secret. Update `docs/deployment.md` with the exact dashboard locations used.

- [ ] **Step 5: Commit the CI/CD slice**

~~~powershell
git add .github/workflows/ci.yml docs/deployment.md
git commit -m "ci: validate and deploy passkey app"
~~~

### Task 8: Perform end-to-end verification and finalize handoff documentation

**Files:**
- Modify: `docs/deployment.md`
- Modify: `docs/superpowers/specs/2026-08-04-passkey-authn-design.md` only if implementation decisions materially differ from the approved design.

- [ ] **Step 1: Run the complete local verification suite**

~~~powershell
dotnet restore
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build
docker build --tag passkeyauthn:verification .
~~~

Expected: all tests pass and the Docker build exits with code 0.

- [ ] **Step 2: Deploy through GitHub Actions**

Push the implementation to `main`, confirm the workflow passes, confirm the Render Deploy Hook starts a deployment, and wait for the `/health` check to pass.

- [ ] **Step 3: Execute the manual Passkey acceptance flow**

On the deployed HTTPS Render URL:

1. Open `/register` and create a Passkey with a supported platform authenticator.
2. Confirm the browser redirects to `/dashboard` and shows the expected email.
3. Logout.
4. Open `/` and sign in with the same email and Passkey.
5. Confirm anonymous access to `/dashboard` redirects to login.
6. Test cancelled authenticator, unsupported WebAuthn, duplicate registration, unknown email, and expired challenge behavior.
7. Verify that the credential exists in Supabase PostgreSQL without exposing private key data.

- [ ] **Step 4: Record deployment limitations**

Document Render Free sleep/wake behavior, Supabase project availability/limits, the lack of account recovery, and the fact that this is a POC rather than a production authentication service.

- [ ] **Step 5: Final verification before claiming completion**

Run the full verification commands again and inspect `git diff --check`. Confirm every approved acceptance criterion has evidence from automated output or the manual HTTPS test.

## Plan self-review checklist

- Spec coverage: Tasks 1–2 cover project conversion, Identity, PostgreSQL, Data Protection, and migrations; Tasks 3–4 cover all authentication endpoints and UI; Task 5 covers HTTPS/runtime security and health; Tasks 6–7 cover Docker, Render, GitHub Actions, and secrets; Task 8 covers acceptance and handoff.
- Placeholder scan: no unresolved implementation markers are used in the task steps.
- Type consistency: endpoint request/response types, settings names, cookie state, and `Program` test-host exposure are defined before their consumers.
- Scope: the plan intentionally excludes password fallback, email verification, recovery, advanced attestation, and production scaling as approved.



