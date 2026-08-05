# Task 3: Implement Passkey settings, registration state, and server endpoints

## Files

- Create `PasskeyAuthn/Configuration/PasskeySettings.cs`.
- Create `PasskeyAuthn/Security/RegistrationStateProtector.cs`.
- Create `PasskeyAuthn/Models/PasskeyRequests.cs`.
- Create `PasskeyAuthn/Models/ApiError.cs`.
- Create `PasskeyAuthn/Endpoints/PasskeyEndpointExtensions.cs`.
- Create `PasskeyAuthn/Endpoints/AuthEndpointExtensions.cs`.
- Modify `PasskeyAuthn/Program.cs`.
- Create `PasskeyAuthn.Tests/Security/RegistrationStateProtectorTests.cs`.
- Create `PasskeyAuthn.Tests/Endpoints/PasskeyEndpointTests.cs`.
- Create `PasskeyAuthn.Tests/Endpoints/AuthEndpointTests.cs`.
- Modify test infrastructure only as needed to exercise real endpoint behavior.

## Current interfaces from earlier tasks

- `ApplicationUser` is `IdentityUser` with nullable `PasskeyRegistrationExpiresAt`.
- `ApplicationDbContext` is `IdentityDbContext<ApplicationUser>, IDataProtectionKeyContext`.
- Identity schema v3 is enabled in `Program.cs`, so the migration includes `AspNetUserPasskeys`.
- `PasskeyWebApplicationFactory` supplies a real Testcontainers PostgreSQL connection and uses `Testing` environment. Production cookie policy must stay `SecurePolicy.Always`; a test-only `SameAsRequest` override is allowed where endpoint tests need HTTP cookies.
- Startup applies migrations before serving requests and requires `ConnectionStrings:Default`.

## Required interfaces and exact values

- `PasskeySettings` contains `ServerDomain`, `ExpectedOrigin`, `AuthenticatorTimeoutSeconds`, `MaxPasskeysPerUser`, and `MaxDisplayNameLength`.
- Bind it from the `Passkey` configuration section. Use safe non-secret defaults suitable for local tests, but production Render configuration must override the hostname/origin with environment variables.
- `RegistrationStateProtector.Protect(string userId, DateTimeOffset expiresAt)` returns a protected token.
- `RegistrationStateProtector.Unprotect(string token)` returns `RegistrationState?`; invalid, tampered, malformed, empty, or expired tokens return null.
- `RegistrationState` is `sealed record RegistrationState(string UserId, DateTimeOffset ExpiresAt)`.
- Registration state is protected with ASP.NET Core Data Protection and stored in an `HttpOnly`, `Secure`, `SameSite=Lax` cookie. Never put the user ID in an unprotected client value.
- `PasskeyEndpointExtensions.MapPasskeyEndpoints(IEndpointRouteBuilder endpoints)` maps exactly:
  - `POST /api/passkeys/register/options`
  - `POST /api/passkeys/register/complete`
  - `POST /api/passkeys/login/options`
  - `POST /api/passkeys/login/complete`
- `AuthEndpointExtensions.MapAuthEndpoints(IEndpointRouteBuilder endpoints)` maps `POST /api/auth/logout`.
- `EmailRequest` contains `string Email`.
- `CredentialRequest` contains `JsonElement Credential`; endpoints pass `Credential.GetRawText()` to ASP.NET Core Identity and never log it.
- `ApiError` contains stable `Code` and safe `Message` values. Do not expose credential JSON, public key material, cookies, connection strings, or account-existence details.
- Maximum passkeys per user is `MaxPasskeysPerUser`; display name input must not exceed `MaxDisplayNameLength`.

## TDD sequence (mandatory)

1. Write `RegistrationStateProtectorTests` first and run them to observe a correct missing-production-code failure.
2. Implement the protector minimally and run the focused security tests green.
3. Write endpoint validation/integration tests before endpoint implementation and observe the expected missing-mapping/behavior failures.
4. Implement endpoints minimally, run focused tests, then the full Release suite. Do not fake cryptographic attestation/assertion success; the real ceremony is manual in Task 8.

## Required state protector tests

Cover:

- Valid protected user ID round-trips.
- Expired token is rejected.
- Modified token is rejected.
- Empty user IDs are rejected.

## Required endpoint tests

Add integration tests using the real application host and PostgreSQL fixture for:

- Invalid email returns `400` with a safe `ApiError`.
- Unknown login email returns generic authentication failure without revealing account existence.
- Duplicate registration for an account with an existing passkey returns `409`.
- Registration options create or reuse a pending user and return JSON.
- Registration completion without a valid registration state returns `400`.
- Login completion with invalid credential JSON returns a safe `401`/`400` response.
- Logout clears the authentication cookie.
- Protected behavior is covered by the logout/auth endpoint tests where relevant.

Do not generate fake attestation/assertion payloads. Tests may seed users/passkey rows only to test validation/conflict behavior; cryptographic success is manual.

## Required Identity passkey configuration

Register `IdentityPasskeyOptions`:

```csharp
options.ServerDomain = settings.ServerDomain;
options.UserVerificationRequirement = "required";
options.ResidentKeyRequirement = "preferred";
options.AuthenticatorTimeout = TimeSpan.FromSeconds(settings.AuthenticatorTimeoutSeconds);
options.ValidateOrigin = context =>
    string.Equals(context.Origin, settings.ExpectedOrigin, StringComparison.OrdinalIgnoreCase);
```

`ServerDomain` is only the RP ID/host, for example `<render-service>.onrender.com`; `ExpectedOrigin` is only the full HTTPS origin, for example `https://<render-service>.onrender.com`.

## Required registration behavior

`POST /api/passkeys/register/options` must:

1. Validate and normalize the email.
2. Find an existing user by normalized email.
3. Reject an existing user with a passkey; reuse an uncredentialed pending user.
4. Create an `ApplicationUser` when needed.
5. Reject a user already at `MaxPasskeysPerUser`.
6. Set `PasskeyRegistrationExpiresAt` to five minutes in the future.
7. Call `SignInManager.MakePasskeyCreationOptionsAsync(new PasskeyUserEntity { Id = user.Id, Name = email, DisplayName = email })`.
8. Protect the user ID and expiry in the registration cookie.
9. Return the JSON options with `application/json` content type.

`POST /api/passkeys/register/complete` must:

1. Read and validate the protected registration cookie.
2. Load the associated user and reject missing/expired state.
3. Call `SignInManager.PerformPasskeyAttestationAsync(credentialJson)`.
4. Return a safe `400` when attestation verification fails.
5. Call `UserManager.AddOrUpdatePasskeyAsync(user, attestationResult.Passkey)`.
6. Clear pending expiry and registration cookie.
7. Sign in with the application cookie and return success.

## Required login/logout behavior

`POST /api/passkeys/login/options` must find the user by normalized email, call `SignInManager.MakePasskeyRequestOptionsAsync(user)`, and return generic failure behavior for unknown users.

`POST /api/passkeys/login/complete` must call `SignInManager.PasskeySignInAsync(credentialJson)` and return success only when `SignInResult.Succeeded` is true. Do not include failure reasons in the response.

`POST /api/auth/logout` must call `SignInManager.SignOutAsync()` and return `204 No Content`.

## Middleware/security requirements

- Register `AddAuthorization()` and call `UseAuthentication()` before `UseAuthorization()`.
- Configure antiforgery header `X-CSRF-TOKEN`; require antiforgery on all state-changing POST endpoints.
- Add a fixed-window rate limiter for the four passkey endpoints and apply its policy.
- Require authorization on the dashboard later; do not depend on UI hiding.
- Keep all errors safe and stable. Do not log sensitive credential values.

## Verification

Run:

```powershell
dotnet test PasskeyAuthn.Tests/PasskeyAuthn.Tests.csproj --filter FullyQualifiedName~RegistrationStateProtectorTests
dotnet test PasskeyAuthn.Tests/PasskeyAuthn.Tests.csproj --filter FullyQualifiedName~EndpointTests
dotnet test --configuration Release
```

If Docker is unavailable, preserve the real PostgreSQL/Testcontainers tests, report the exact environment failure, and still run the security unit tests and Release build. Do not switch to in-memory storage.

## Scope boundary

Do not implement Razor pages/JavaScript UI, Docker, Render, GitHub Actions, or manual deployment in this task. Do not initialize Git or create commits.

