# Task 3 review package (Git unavailable)

This workspace has no Git repository, so there is no BASE/HEAD or git diff. Review the current Task 3 source/test files as the authoritative change set, using the implementer report for command evidence only. Docker is unavailable locally; separate environment-blocked PostgreSQL execution from code defects.

## Files in scope

- `PasskeyAuthn/Configuration/PasskeySettings.cs`
- `PasskeyAuthn/Security/RegistrationStateProtector.cs`
- `PasskeyAuthn/Models/PasskeyRequests.cs`
- `PasskeyAuthn/Models/ApiError.cs`
- `PasskeyAuthn/Endpoints/PasskeyEndpointExtensions.cs`
- `PasskeyAuthn/Endpoints/AuthEndpointExtensions.cs`
- `PasskeyAuthn/Program.cs`
- `PasskeyAuthn.Tests/Security/RegistrationStateProtectorTests.cs`
- `PasskeyAuthn.Tests/Endpoints/PasskeyEndpointTests.cs`
- `PasskeyAuthn.Tests/Endpoints/AuthEndpointTests.cs`
- `PasskeyAuthn.Tests/Infrastructure/AntiforgeryHttpClient.cs`
- `PasskeyAuthn.Tests/Infrastructure/PasskeyWebApplicationFactory.cs`
- `PasskeyAuthn.Tests/Data/IdentitySchemaConfigurationTests.cs` if affected by Task 3.

## Binding approved-design constraints

- Use built-in ASP.NET Core Identity passkey support on .NET 10; no third-party FIDO2/WebAuthn verification library.
- Use email as unique identifier without email verification.
- Use protected server-side registration state, `HttpOnly`, `Secure`, `SameSite=Lax` cookies, antiforgery on state-changing POSTs, and rate limiting on options/completion endpoints.
- Limit each user to at most **three** passkeys and limit passkey display names to **100** characters.
- Keep WebAuthn `ServerDomain` as host/RP ID and `ExpectedOrigin` as the full HTTPS origin; do not conflate them.
- Never expose/log credential JSON, public-key material, cookies, or connection strings; error responses must be safe and generic for unknown accounts/failed ceremonies.
- Use the .NET 10 built-in methods `MakePasskeyCreationOptionsAsync`, `PerformPasskeyAttestationAsync`, `MakePasskeyRequestOptionsAsync`, `PasskeySignInAsync`, and `AddOrUpdatePasskeyAsync`.

## Review method

Read the Task 3 brief, implementer report, and current source files. Review public contracts/docs, endpoint metadata/middleware order, cookie/antiforgery/rate-limit behavior, error handling, state expiry/tamper protection, and tests. Do not mutate files or broaden to later UI/deployment tasks.

