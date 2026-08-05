# Task 2 fix round 1 review package (Git unavailable)

The previous Task 2 review reported three Important findings: schema v3/passkey table missing, weak DataProtectionKeys table assertion, and non-deterministic startup migration test. Git is unavailable; inspect the current changed files and migration, plus the appended fix report.

## Findings to verify

1. `Program.cs` must configure `IdentitySchemaVersions.Version3` and the generated migration must create `AspNetUserPasskeys`.
2. `IdentityPersistenceTests` must verify `DataProtectionKeys` table existence through real PostgreSQL behavior, independent of row count.
3. `Startup_initializer_applies_pending_migrations` must use a fresh isolated PostgreSQL schema/database and prove pending migrations before and none after invoking the initializer.

Review only the fix-related changes for these findings and any new breakage in them. Do not mutate files.

