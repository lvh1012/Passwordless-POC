# Deployment

This POC runs as a Docker Web Service on Render and stores all application state
in Supabase PostgreSQL. No database credential, passkey secret, private key, or
Deploy Hook URL belongs in this repository.

## 1. Create Supabase storage

1. Create a Supabase project.
2. In the Supabase dashboard, open the database connection details and select
   the **Shared Pooler** connection using **session mode**. Use that PostgreSQL
   connection string for the persistent backend; do not use the transaction
   pooler mode for this application.
3. Require TLS in the connection string or Render environment value, for
   example by adding `SSL Mode=Require`. Replace any placeholders with values
   from your own project, but never paste the resulting secret into this file.
4. Set `ConnectionStrings__Default` in Npgsql's key/value format, for example
   `Host=<pooler-host>;Port=5432;Database=postgres;Username=<user>;Password=<password>;SSL Mode=Require`.
   Do not paste Supabase's `postgresql://...` URI unchanged: the application's
   production validator parses Npgsql connection strings and rejects URI syntax
   before migrations can run. Keep the actual password only in Render.

Supabase is the only application database. EF Core migrations and Data
Protection keys are persisted there, so the Render container's local filesystem
does not need to be durable.

## 2. Create the Render service

1. Create a Render Web Service from the repository, or create it from the
   `pocs/PasskeyAuthn/render.yaml` Blueprint file. When using a custom Blueprint
   Path in Render, enter exactly `pocs/PasskeyAuthn/render.yaml`. The service's
   `rootDir: pocs/PasskeyAuthn` scopes the service to this POC; Docker paths and
   the `dockerContext: .` build context are resolved relative to that root, so
   `dockerfilePath: ./Dockerfile` uses this POC's Dockerfile and does not widen
   the build context to the monorepo root.
2. Select Docker as the runtime and the Free plan.
3. Keep the generated platform hostname, which will be
   `https://<service>.onrender.com`. Do not configure a custom domain for this
   POC.
4. Set **Auto-Deploy** to off. Task 7 will trigger deployment through a Render
   Deploy Hook after GitHub Actions validation succeeds.

The checked-in Blueprint declares these environment values as externally
provided with `sync: false`:

- `ConnectionStrings__Default`: the Supabase Shared Pooler session-mode
  PostgreSQL connection string, including `SSL Mode=Require`.
- `Passkey__ServerDomain`: `<service>.onrender.com`, host/RP ID only; no scheme
  and no path.
- `Passkey__ExpectedOrigin`: `https://<service>.onrender.com`, the full origin.

Set all three in the Render service's **Environment** settings. Render's TLS
termination and HTTP-to-HTTPS behavior provide the deployed HTTPS origin; the
application consumes the forwarded HTTPS scheme. The container listens on the
standard `ASPNETCORE_HTTP_PORTS` setting, which the hosting environment can
override without rebuilding the image. Production startup rejects local/mismatched
Passkey origins, limits above 3 Passkeys or 100 display-name characters, and
PostgreSQL connections whose SSL mode is not `Require`, `VerifyCA`, or
`VerifyFull` before it attempts database migration.

## 3. Configure the GitHub Actions deploy trigger

After the Render service exists, open the service's **Settings** → **Deploy
Hook**, create a hook, and copy its URL. In the GitHub repository, open
**Settings** → **Secrets and variables** → **Actions** → **New repository
secret**, then save the URL under the exact name
`RENDER_DEPLOY_HOOK_URL_PASSKEY_AUTHN`.

Each deployable POC must use its own GitHub Actions secret with a unique name.
The secret name is declared by that POC's `poc.json`; discovery rejects
duplicate non-empty secret names, and the root workflow reads the matching
secret for each entry in its deploy matrix after all validation jobs succeed.

Keep Render **Auto-Deploy** off because GitHub Actions triggers the hook. The
   workflow in [`../../.github/workflows/ci.yml`](../../../.github/workflows/ci.yml)
   relative to the POC directory runs only for pull request or push events that
   change `.github/workflows/ci.yml`, `scripts/**`, `pocs/**`, or
   `PasswordlessAuthn.slnx`:

- Pull requests run restore, build, test, and Docker image validation only.
- Direct pushes to non-`main` branches do not trigger this workflow, so they run
  neither validation nor the Render Deploy Hook.
- A successful push to `main` runs those validations first and then calls the
  Render Deploy Hook.

The Deploy Hook URL is never stored in the repository or in Render environment
variables. It is read only from the GitHub Actions repository secret
`RENDER_DEPLOY_HOOK_URL_PASSKEY_AUTHN` for this POC and is not printed in
workflow logs. Other POCs use their own secret and deploy matrix entry.

## 4. Verify before Passkey testing

Open `https://<service>.onrender.com/health` and confirm the response is healthy
before attempting browser Passkey registration or login. A failed health check
usually means the connection string, SSL requirement, or database availability
needs attention.

## 5. Manual HTTPS acceptance checklist

Run this checklist only against the deployed
`https://<service>.onrender.com` hostname in a modern browser with a platform
authenticator. The real browser and authenticator are required because an
automated HTTP client cannot prove the WebAuthn cryptographic ceremony.

1. Open `/register` and create a platform Passkey.
2. Confirm the browser redirects to `/dashboard` and displays the expected
   email address.
3. Log out from `/dashboard`.
4. Open `/`, enter the same email address, and sign in with the same Passkey.
5. In a new anonymous browser session, open `/dashboard` and confirm it
   redirects to `/`.
6. Confirm safe user-facing behavior for a cancelled authenticator, unsupported
   WebAuthn, duplicate registration, an unknown email, a malformed credential,
   and an expired challenge.
7. In Supabase PostgreSQL, confirm that `AspNetUserPasskeys` contains a row whose
   `UserId` joins to the registered row in `AspNetUsers`. Inspect only the row
   relationship and public credential metadata; never expose credential blobs
   or private key material in screenshots, logs, or documentation.
8. Restart or redeploy the Render service, wait for `/health`, then log out and
   sign in again with the same Passkey. This verifies the PostgreSQL credential
   row and persisted Data Protection keys survive application restart.
9. Confirm `/health` is healthy both before and after the Render Free service
   wakes from an idle period.

Successful cryptographic ceremony and deployment evidence require all of the
following external prerequisites: a real browser and compatible authenticator,
valid Supabase credentials, a Render service, a GitHub repository, the
repository secret `RENDER_DEPLOY_HOOK_URL_PASSKEY_AUTHN`, and a successful
push to `main`.
Until those prerequisites exist, this checklist is handoff work rather than
evidence that the deployed flow has passed.

## POC limitations

- Render Free services sleep after inactivity and may need time to wake up on
  the first request; repeat the `/health` check after wake-up before running a
  Passkey ceremony.
- Supabase project availability, quotas, connection limits, and other Free-plan
  limitations apply; they are outside this application's control.
- This POC does not implement account recovery or email verification.
- The email-first flow deliberately returns different statuses/options for
  unknown login and duplicate registration. Generic messages avoid exposing
  Passkey details, but eligibility can still be inferred through status,
  options, or timing; this is not a production anti-enumeration design.
- The POC has no password fallback, Passkey management UI, production SLA,
  monitoring, backups, multi-instance scaling, or high-volume rate limiting.
- It is not a production authentication service; production use would require
  a security review, operational monitoring, recovery policy, and an appropriate
  hosting/database plan.
