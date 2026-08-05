# Task 4 review package (Git unavailable)

This workspace has no Git repository. Review current Task 4 files as the authoritative change set; the implementer report contains command evidence only. Docker is unavailable locally, so page integration tests may be environment-blocked rather than code-green.

## Files in scope

- `PasskeyAuthn/Pages/_ViewImports.cshtml`
- `PasskeyAuthn/Pages/_ViewStart.cshtml`
- `PasskeyAuthn/Pages/Shared/_Layout.cshtml`
- `PasskeyAuthn/Pages/Index.cshtml`
- `PasskeyAuthn/Pages/Register.cshtml`
- `PasskeyAuthn/Pages/Dashboard.cshtml`
- `PasskeyAuthn/wwwroot/js/passkey.js`
- `PasskeyAuthn/wwwroot/css/site.css`
- `PasskeyAuthn.Tests/Pages/PageSmokeTests.cs`
- `PasskeyAuthn/Program.cs` only for Task 4 cookie redirect changes.

## Binding constraints

- Razor Pages + vanilla JavaScript only; no SPA/CSS framework.
- `window.PasskeyAuth.startLogin(email)`, `startRegistration(email)`, `serializeCredential(credential)`, and `mapError(error)` are the documented browser APIs.
- Use WebAuthn native parse helpers when available and base64url fallback without padding; manually serialize all required credential fields.
- CSRF token is emitted as `meta[name="csrf-token"]`, sent as `X-CSRF-TOKEN`, and requests use same-origin credentials.
- Dashboard must be protected by authorization metadata and anonymous requests must reach the implemented `/` login page.
- UI must redirect to `/dashboard` only after successful server completion; no sensitive values may be logged.
- Preserve production cookie `HttpOnly`, `SecurePolicy.Always`, `SameSite.Lax`.

## Review method

Read the Task 4 brief, report, current pages, JavaScript, CSS, tests, and relevant Program cookie configuration. Check actual browser contract compatibility with Task 3 request/response shapes, fallback serialization, event/UI state, protected redirect behavior, and public API docs. Do not mutate files or broaden into later deployment tasks.

