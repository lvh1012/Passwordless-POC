# Scoped re-review report: final fix wave

Date: 2026-08-04

## Findings

### Critical

1. `PasskeyAuthn/Configuration/ProductionConfigurationValidator.cs:41-52` —
   Production validation does not reject the literal `localhost`. `Uri.CheckHostName("localhost")`
   returns `Dns`, so `localhost` passes every condition in `ValidateServerDomain`.
   Consequently, `ServerDomain=localhost` plus
   `ExpectedOrigin=https://localhost` and a TLS PostgreSQL connection passes
   `Validate`, violating the approved requirement that Production cannot become
   healthy with the localhost fallback. The accompanying source test at
   `PasskeyAuthn.Tests/Configuration/ProductionConfigurationValidatorTests.cs:25-43`
   lists `localhost` as invalid, but its expected origin remains the Render
   hostname; against the reviewed source it would fail later with
   `Passkey:ExpectedOrigin`, not the asserted `Passkey:ServerDomain`.

   Required fix: explicitly reject `localhost` (case-insensitively) as an RP ID
   in Production and add a test with both `ServerDomain=localhost` and
   `ExpectedOrigin=https://localhost` that asserts the `ServerDomain` error.

### Important / Minor

No additional findings.

## Verdicts for the eight final-fix items

1. **Pass** — `PasskeyAuthn/wwwroot/js/passkey.js:90-123` includes root
   `clientExtensionResults`, retains unpadded base64url conversion, and the
   dependency-free Node test covers registration and assertion serialization at
   `PasskeyAuthn.Tests/Browser/passkey-client-contract.test.mjs:44-101`.
   Independent `node --check` and `node --test` both passed (2/2 tests).
2. **Pass** — `PasskeyAuthn/Security/AuthenticationCookieEvents.cs:16-41`
   returns API `401/403` without a redirect and redirects non-API challenges to
   exactly `/`; `Program.cs:61-70` wires both events while retaining secure,
   HTTP-only, Lax cookies.
3. **Fail (Critical)** — Validation is wired before migration at
   `PasskeyAuthn/Program.cs:91-104`, rejects the other reviewed invalid inputs,
   and does not echo connection strings. However, the localhost defect above
   means the fail-closed Production invariant is incomplete.
4. **Pass** — The approved email-first/duplicate-`409` behavior remains in
   `PasskeyAuthn/Endpoints/PasskeyEndpointExtensions.cs:67-80,188-196`, while
   `docs/deployment.md:120-123` and the ledger explicitly document the
   eligibility-oracle limitation without claiming anti-enumeration resistance.
5. **Pass** — `PasskeyAuthn/Program.cs:78-88` explicitly sets rate-limit
   rejection to HTTP `429`.
6. **Pass** — `docs/deployment.md:95-103` requires verification of the joined
   `AspNetUserPasskeys`/`AspNetUsers` row and reuse after logout plus
   restart/redeploy, without directing disclosure of credential blobs or private
   key material.
7. **Pass** — `.github/workflows/ci.yml:30-36` runs Node syntax and contract
   tests before Docker image validation.
8. **Pass** — `.superpowers/sdd/2026-08-04-passkey-authn-poc/progress.md:20-22`
   contains explicit `Final status: handoff pending` wording.

## Regression and evidence notes

- API cookie event handling, rate-limit status, serializer contract, CI order,
  documentation, and comment/XML documentation requirements were reviewed with
  no further regression found.
- The report's previous Docker/Testcontainers, Docker image, GitHub Actions,
  Render/Supabase, and deployed HTTPS browser-ceremony blockers remain honestly
  reported. They are not counted as passing evidence.
- The scoped review did not modify application or test source. The only review
  execution was `node --check` and the Node contract test, which passed.

## Ready for handoff?

**No.** Resolve the Critical localhost Production-validator defect and rerun the
scoped verification. After code review is clean, handoff will still be
**Conditional** on the external Docker/GitHub/Render/Supabase/browser evidence.
