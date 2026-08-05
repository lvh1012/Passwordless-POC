# SDD ledger — plan: docs/superpowers/plans/2026-08-04-passkey-authn-poc.md

Execution note: the workspace has no Git repository, so commit hashes and git-based review packages are unavailable. Task artifacts and review verdicts are tracked here and reviewers must inspect the current files plus the task brief.

Task 1: fix round 1/5 (1 addressed, 0 open — public SmokeTests method lacked XML documentation; addressed by implementer and scoped re-review; commits none)
Task 1: complete (current workspace, review clean)
Task 2: fix round 1/5 (3 addressed, 0 open — Identity schema v3/passkey table, DataProtectionKeys table assertion, and deterministic startup migration test; commits none)
Task 2: complete (current workspace, review clean; runtime PostgreSQL execution remains environment-blocked until Docker daemon is available)
Task 3: fix round 1/5 (3 addressed, 0 open — approved limits, generic registration-conflict message, and stable malformed-body errors; commits none)
Task 3: minor (deferred): broader antiforgery matrix coverage is useful but endpoint group metadata and middleware are already verified; triage again in final review.
Task 3: complete (current workspace, review clean; PostgreSQL-backed endpoint assertions remain environment-blocked until Docker daemon is available)
Task 4: fix round 1/5 (1 addressed, 0 open — anonymous protected-page redirect now exact `/` without ReturnUrl; commits none)
Task 4: complete (current workspace, review clean; PostgreSQL-backed page assertions remain environment-blocked until Docker daemon is available)
Task 5: complete (current workspace, review approved; PostgreSQL-backed health/runtime assertions remain environment-blocked until Docker daemon is available)
Task 6: complete (current workspace, review approved; Docker image/runtime verification remains environment-blocked until Docker daemon is available)
Task 7: complete (current workspace, review approved; GitHub-hosted Docker/Testcontainers validation remains to be performed after repository setup)
Task 8: complete (current workspace, review approved; external HTTPS/browser/deployment acceptance remains handoff-only because no external resources exist)
Final review fix wave: implemented (WebAuthn JSON contract, API cookie status semantics, fail-closed production configuration, rate-limit status, CI browser test, and handoff documentation addressed; 32 non-Docker C# tests plus 2 Node tests passed, while 34 PostgreSQL integration tests and Docker image build remain blocked by the unavailable Docker daemon)
Account-enumeration ruling: the approved email-first and duplicate-registration HTTP 409 behavior remains; generic messages do not remove the eligibility oracle, so no strong anti-enumeration claim is made.
Post-review correction: Production now rejects the literal `localhost` RP ID case-insensitively before ExpectedOrigin validation; matching regression coverage passed 20/20 validator tests, and Development/Testing behavior is unchanged.
Controller final audit: Release build and format checks passed; focused non-Docker tests and Node contract tests passed; the full suite reported 33 passed and 34 Docker-blocked tests, and the Docker image probe could not reach the local Docker Desktop Linux engine.
Final status: handoff pending — Docker/Testcontainers, GitHub-hosted workflow, Render/Supabase provisioning, and deployed HTTPS browser-ceremony evidence are still external or environment-blocked.
