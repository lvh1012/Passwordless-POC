# Passkey Authentication POC Design

## Status

Approved design for implementation planning.

## Goal

Build a small end-to-end Passkey login POC with a browser UI, using .NET 10 and Supabase PostgreSQL. The app must be deployable on Render Free Web Service with an HTTPS `onrender.com` subdomain and use GitHub Actions for CI/CD.

## Current project context

- The repository currently contains a solution with one empty `PasskeyAuthn` project.
- `PasskeyAuthn.csproj` currently targets `net10.0` but uses the base `Microsoft.NET.Sdk` and has no application dependencies.
- There is no existing web host, database layer, authentication setup, test project, Dockerfile, or Git repository.

## Architecture

```text
Browser
  └─ HTTPS → ASP.NET Core Web App on Render
                ├─ ASP.NET Core Identity passkey APIs
                ├─ Razor Pages + vanilla JavaScript WebAuthn API
                └─ EF Core/Npgsql → Supabase PostgreSQL
```

### Application

- Convert `PasskeyAuthn` to an ASP.NET Core Web project.
- Use Razor Pages and vanilla JavaScript for the browser UI. No SPA framework is needed for this POC.
- Use the built-in ASP.NET Core Identity passkey support available in .NET 10. This covers the normal registration and authentication scenarios without custom cryptographic verification or a third-party FIDO2 package.
- Use cookie authentication for the protected page and logout flow.

### Database

- Use ASP.NET Core Identity with an `ApplicationUser` derived from `IdentityUser`.
- Use email as the unique user identifier. Email is an identifier only; the POC does not send or validate email ownership.
- Use EF Core with the Npgsql provider to connect to Supabase PostgreSQL.
- Use the standard Identity tables, including `AspNetUsers` and `AspNetUserPasskeys`.
- Persist ASP.NET Core Data Protection keys in PostgreSQL so authentication cookies remain usable after a Render restart.
- Do not create a custom Passkey table or store private keys. The server stores only public credential data supplied by the Identity passkey implementation.
- Apply pending EF Core migrations during application startup. This is acceptable for a single-instance POC; a production deployment would use a dedicated migration step.

### Render

- Deploy the app as a Render Free Web Service built from a multi-stage `Dockerfile`.
- Configure a health check at `/health`.
- The app listens on the port supplied by Render and binds to `0.0.0.0`.
- Use `https://<render-service>.onrender.com` as the WebAuthn origin and `<render-service>.onrender.com` as the RP ID.
- Configure `ServerDomain` explicitly with the Render hostname and validate the expected HTTPS origin.
- Enable HSTS and secure authentication cookies.
- Accept the Free plan trade-off that the service sleeps after inactivity and may take about one minute to wake up. Credential data remains in Supabase; only the running process and local filesystem are ephemeral.

### GitHub Actions

The workflow runs on pull requests and pushes to `main`:

1. Check out the repository.
2. Install the .NET 10 SDK.
3. Run `dotnet restore`.
4. Run `dotnet build --no-restore`.
5. Run `dotnet test --no-build`.
6. Run `docker build` to validate the production image.
7. On a successful push to `main`, call the Render Deploy Hook.

Pull requests run validation only. The `main` branch deploys only after all checks pass. `RENDER_DEPLOY_HOOK_URL` is stored as a GitHub Actions secret. Supabase connection strings and runtime secrets are stored only in Render environment variables.

Render auto-deploy on commit is disabled so GitHub Actions remains the CI gate for deployment.

## UI and endpoints

### Pages

- `/`: login page with email input and `Sign in with passkey` button.
- `/register`: registration page with email input and `Create passkey` button.
- `/dashboard`: protected page showing the current user's email and a `Logout` button.

### Endpoints

```text
POST /api/passkeys/register/options
POST /api/passkeys/register/complete

POST /api/passkeys/login/options
POST /api/passkeys/login/complete

POST /api/auth/logout
GET  /health
```

The UI and API use the same origin, so CORS is not required.

## Authentication flows

### Registration

1. The user submits a normalized, unique email.
2. The server creates an Identity user and starts a short-lived registration ceremony.
3. The server returns `PublicKeyCredentialCreationOptions`.
4. The browser calls `navigator.credentials.create()`.
5. The authenticator asks the user for biometric, PIN, or device approval.
6. The browser sends the serialized attestation to the server.
7. ASP.NET Core Identity verifies the credential type, origin, challenge, authenticator flags, and public key.
8. The server stores the passkey and signs the user in with an authentication cookie.
9. The browser redirects to `/dashboard`.

If the ceremony is cancelled or fails, the user record may remain without a passkey. It cannot sign in until a passkey is completed; a later registration attempt for that email reuses the incomplete account instead of creating a duplicate.

### Login

1. The user submits their email.
2. The server returns `PublicKeyCredentialRequestOptions` for that user's registered passkeys.
3. The browser calls `navigator.credentials.get()`.
4. The authenticator signs the server challenge after user verification.
5. The browser sends the assertion to the server.
6. ASP.NET Core Identity verifies the signature, challenge, origin, authenticator flags, and signature counter.
7. The server updates passkey metadata and creates the authentication cookie.
8. The browser redirects to `/dashboard`.

## Security baseline

- Require HTTPS for all deployed Passkey operations.
- Configure `RP ID` from the fixed Render hostname and `Origin` from the matching HTTPS URL rather than trusting arbitrary host headers.
- Use `HttpOnly`, `Secure`, and `SameSite=Lax` authentication cookies.
- Apply antiforgery protection to state-changing POST requests.
- Rate-limit the options and completion endpoints.
- Limit each user to at most three passkeys and limit passkey display names to 100 characters.
- Never log credential JSON, public key material, authentication cookies, or database connection strings.
- Do not include password fallback, email verification, account recovery, or multi-device management in this POC.

## Error handling

- Return a generic authentication failure for invalid login attempts.
- Return a clear conflict response for duplicate registration where the existing account already has a passkey.
- Treat expired or mismatched challenges as a failed ceremony and require a new attempt.
- Map browser errors such as `NotAllowedError`, `AbortError`, and unsupported WebAuthn to user-readable messages without exposing sensitive server details.
- Fail health checks when the application cannot reach its required database configuration.
- Do not expose stack traces or database errors in production responses.

## Testing and acceptance criteria

### Automated checks

- Unit tests cover email normalization, registration validation, endpoint authorization, and error mapping.
- Integration tests cover `/health`, protected-page access, logout, and database connectivity configuration.
- CI builds the application and production Docker image.

### Manual end-to-end checks

- Register a Passkey on the deployed HTTPS Render URL.
- Confirm the credential is persisted in Supabase PostgreSQL.
- Logout and log in again with the Passkey.
- Confirm anonymous users are redirected away from `/dashboard`.
- Confirm unsupported browsers, cancelled authenticators, duplicate email, unknown email, and expired ceremonies produce safe user-facing errors.
- Verify the flow in a modern Chromium-based browser with a platform authenticator such as Windows Hello, Touch ID, or an equivalent device authenticator.

### Completion criteria

The POC is complete when:

1. A user can register with an email and Passkey.
2. The public credential data is stored in Supabase PostgreSQL.
3. The user can logout and sign in again with the Passkey.
4. Anonymous requests cannot access `/dashboard`.
5. The app runs on Render over HTTPS.
6. GitHub Actions validates build, tests, and Docker image before deployment.
7. Render does not promote a deployment that fails its health check.

## Explicitly out of scope

- Password authentication or password reset.
- Email ownership verification and email delivery.
- Account recovery after losing all Passkeys.
- Advanced attestation validation.
- Passkey rename/delete UI and multi-device administration.
- Production SLA, monitoring, backups, multi-instance scaling, and high-volume rate limiting.

## References

- [ASP.NET Core passkeys](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/passkeys/?view=aspnetcore-10.0)
- [Supabase PostgreSQL connection methods](https://supabase.com/docs/guides/database/connecting-to-postgres)
- [Render web services](https://render.com/docs/web-services)
- [Render Deploy Hooks and GitHub Actions](https://render.com/docs/deploy-hooks)
- [Render TLS](https://render.com/docs/tls)
