# Task 4: Build the browser UI and WebAuthn client

## Files

- Create `PasskeyAuthn/Pages/_ViewImports.cshtml`.
- Create `PasskeyAuthn/Pages/_ViewStart.cshtml`.
- Create `PasskeyAuthn/Pages/Shared/_Layout.cshtml`.
- Create `PasskeyAuthn/Pages/Index.cshtml`.
- Create `PasskeyAuthn/Pages/Register.cshtml`.
- Create `PasskeyAuthn/Pages/Dashboard.cshtml`.
- Create `PasskeyAuthn/wwwroot/js/passkey.js`.
- Create `PasskeyAuthn/wwwroot/css/site.css`.
- Create `PasskeyAuthn.Tests/Pages/PageSmokeTests.cs`.
- Modify `PasskeyAuthn/Program.cs` only as needed to make the protected dashboard redirect to `/` and keep all production cookie security settings.

## Interfaces

- `window.PasskeyAuth.startLogin(email)` starts the login ceremony and redirects to `/dashboard` only after a successful response.
- `window.PasskeyAuth.startRegistration(email)` starts registration and redirects to `/dashboard` only after a successful response.
- `window.PasskeyAuth.serializeCredential(credential)` returns the JSON-safe credential shape expected by the server's `CredentialRequest`.
- `window.PasskeyAuth.mapError(error)` returns a safe user-facing message.
- Exported JavaScript APIs must have JSDoc; public Razor page models/attributes must have appropriate documentation/comments where code is introduced.

## Approved design constraints

- Use Razor Pages and vanilla JavaScript only. Do not add a SPA framework or CSS framework.
- Browser UI must support the complete registration and login ceremony against the existing server endpoints.
- Do not implement custom cryptographic verification in JavaScript; the browser only calls the WebAuthn API and serializes its result.
- Keep the user-facing UI in English because the POC API and browser messages are already English; keep software technical terms in English in code comments.
- Do not log credential JSON, public-key material, cookies, or connection strings in browser console or server code.

## TDD/page tests first

Write `PageSmokeTests` before adding the pages and run the focused test to observe the expected missing-page failure. Then implement the minimal pages and client, run tests to green, and refactor only while green.

Required page tests:

- `GET /` returns `200`, contains the email field, and contains the login button.
- `GET /register` returns `200` and contains the registration action.
- `GET /dashboard` redirects an anonymous client to `/` (not to a missing default Identity page).
- The shared layout contains the antiforgery meta tag and `passkey.js` reference.
- The dashboard contains logout action and authenticated email rendering marker/text.

Use the existing real PostgreSQL `PasskeyWebApplicationFactory` for page tests; do not introduce an in-memory provider. If Docker is unavailable, report those integration tests as environment-blocked while still running compile/static checks.

## Shared layout requirements

- Render a request antiforgery token into a meta element named `csrf-token` and configure the server header as `X-CSRF-TOKEN`.
- Load `/js/passkey.js` with `defer`.
- Provide navigation links for login/register and a consistent status/error element for login and registration pages.
- Use a logout button that performs the required POST through `fetch` with the antiforgery header; do not submit credentials or tokens to query strings.

## Page requirements

`Index.cshtml`:

- Route `/`.
- Email input with a stable id/name.
- `Sign in with passkey` button.
- Status and error elements.
- Hook the button to `PasskeyAuth.startLogin(email)`.

`Register.cshtml`:

- Route `/register`.
- Email input with a stable id/name.
- `Create passkey` button.
- Status and error elements.
- Hook the button to `PasskeyAuth.startRegistration(email)`.

`Dashboard.cshtml`:

- Route `/dashboard`.
- Require authorization using Razor authorization metadata, not UI hiding.
- Display the authenticated email from the claims principal.
- Provide logout action.

Configure the Identity application cookie login path to `/` if necessary so an anonymous dashboard request redirects to the existing login page. Preserve `HttpOnly`, `SecurePolicy.Always`, and `SameSite.Lax` in production; test-only cookie relaxation remains in the test factory.

## WebAuthn client requirements

In `passkey.js`:

1. Read the CSRF token from the `csrf-token` meta element.
2. POST the email to the corresponding options endpoint with JSON and `X-CSRF-TOKEN`.
3. Parse creation options with `PublicKeyCredential.parseCreationOptionsFromJSON()` and request options with `PublicKeyCredential.parseRequestOptionsFromJSON()` when available.
4. Include a browser fallback that converts base64url values to `ArrayBuffer` for challenge, user id, exclude credentials, and allow credentials when parse helpers are unavailable.
5. Call `navigator.credentials.create({ publicKey: options })` for registration and `navigator.credentials.get({ publicKey: options })` for login.
6. Serialize `rawId`, `clientDataJSON`, `attestationObject`, `authenticatorData`, `signature`, and optional `userHandle` to base64url without padding. Preserve credential `id` and `type`.
7. POST the serialized credential to the complete endpoint with JSON and CSRF header.
8. Redirect to `/dashboard` only after a successful response.
9. Display safe messages for missing WebAuthn support, `NotAllowedError`, `AbortError`, `InvalidStateError`, challenge failures, server errors, and malformed API responses.
10. Include the manual serialization fallback because some password managers do not implement `PublicKeyCredential.toJSON()` correctly.

Use `fetch` with `credentials: "same-origin"`; do not include secrets in URLs or logs. Disable the active button during a ceremony and re-enable it in a `finally` path.

## Styling requirements

Create minimal accessible CSS for a centered form, visible focus states, disabled buttons during ceremonies, status messages, error messages, navigation, and dashboard layout. Do not add a CSS framework.

## Verification

Run:

```powershell
dotnet test PasskeyAuthn.Tests/PasskeyAuthn.Tests.csproj --filter FullyQualifiedName~PageSmokeTests
dotnet test --configuration Release
dotnet format PasswordlessAuthn.slnx --verify-no-changes --no-restore
```

Also inspect `passkey.js` for valid syntax and run a local static check if available. Docker-blocked page integration tests must remain real PostgreSQL tests and be reported honestly.

## Scope boundary

Do not implement health readiness, Docker, Render, GitHub Actions, or deployment docs in this task. Do not initialize Git or create commits.

