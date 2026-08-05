# Task 2 Report: Identity, PostgreSQL persistence, and Data Protection

## Status

`DONE_WITH_CONCERNS`

The Task 2 implementation is present, compiles in Release, and persistence tests target a real `Testcontainers.PostgreSql` instance. Runtime PostgreSQL execution could not be completed because the local Docker daemon is unavailable. No in-memory provider or second application database was introduced.

## Files changed

- Modified `PasskeyAuthn/Program.cs`
- Modified `PasskeyAuthn.Tests/PasskeyAuthn.Tests.csproj`
- Modified `PasskeyAuthn.Tests/SmokeTests.cs`
- Created `PasskeyAuthn/Data/ApplicationUser.cs`
- Created `PasskeyAuthn/Data/ApplicationDbContext.cs`
- Created `PasskeyAuthn/Data/DatabaseInitializer.cs`
- Created `PasskeyAuthn/appsettings.json`
- Created `PasskeyAuthn/appsettings.Development.json`
- Created `PasskeyAuthn/Migrations/20260804110027_InitialIdentityAndDataProtection.cs`
- Created `PasskeyAuthn/Migrations/20260804110027_InitialIdentityAndDataProtection.Designer.cs`
- Created `PasskeyAuthn/Migrations/ApplicationDbContextModelSnapshot.cs`
- Created `PasskeyAuthn.Tests/Infrastructure/PostgresFixture.cs`
- Created `PasskeyAuthn.Tests/Infrastructure/PasskeyWebApplicationFactory.cs`
- Created `PasskeyAuthn.Tests/Data/IdentityPersistenceTests.cs`

## Decisions

- `ApplicationUser` has only the required nullable `PasskeyRegistrationExpiresAt` application property.
- `ApplicationDbContext` derives exactly from `IdentityDbContext<ApplicationUser>` and implements `IDataProtectionKeyContext`; Data Protection keys share the Identity PostgreSQL database.
- Startup uses `Database.MigrateAsync()` through `DatabaseInitializer` and rejects a blank/missing `ConnectionStrings:Default` before serving requests. EF design-time execution skips this startup migration so `dotnet ef` can generate migrations without a live database.
- Production application cookies are `HttpOnly`, `SecurePolicy.Always`, and `SameSite.Lax`. The test factory overrides only the connection string and environment; it does not weaken production cookie configuration.
- `PostgresFixture` uses `Testcontainers.PostgreSql` with PostgreSQL 16 and is shared through an xUnit collection. `SmokeTests` now uses this test factory because application startup correctly requires a database after Task 2.
- `InitialIdentityAndDataProtection` was generated with the required command. No credential is present in configuration, tests, comments, logs, or this report.

## TDD evidence

### RED

Command:

```powershell
dotnet test PasskeyAuthn.Tests/PasskeyAuthn.Tests.csproj --filter FullyQualifiedName~IdentityPersistenceTests
```

Output before production persistence code:

```text
D:\Code\PasswordlessAuthn\PasskeyAuthn.Tests\Data\IdentityPersistenceTests.cs(4,20): error CS0234: The type or namespace name 'Data' does not exist in the namespace 'PasskeyAuthn' (are you missing an assembly reference?)
```

Exit code: `1`. This is the expected failure: the persistence test referenced the missing production persistence namespace.

### Migration generation

Command:

```powershell
dotnet ef migrations add InitialIdentityAndDataProtection --project PasskeyAuthn/PasskeyAuthn.csproj --output-dir Migrations
```

Output:

```text
Build started...
Build succeeded.
Done. To undo this action, use 'ef migrations remove'
```

The EF tool also emitted a non-blocking version warning because the installed `dotnet-ef` tool is `10.0.9` while the runtime package is `10.0.10`.

### Green compile-level verification

Command:

```powershell
dotnet build --configuration Release --no-restore
```

Output:

```text
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

Command:

```powershell
dotnet test --configuration Release --no-build --list-tests
```

Output:

```text
The following Tests are available:
    PasskeyAuthn.Tests.SmokeTests.Health_endpoint_returns_success
    PasskeyAuthn.Tests.Data.IdentityPersistenceTests.Create_user_with_unique_email_persists_to_PostgreSQL
    PasskeyAuthn.Tests.Data.IdentityPersistenceTests.User_can_be_loaded_by_normalized_email
    PasskeyAuthn.Tests.Data.IdentityPersistenceTests.Migration_creates_DataProtectionKeys_table
    PasskeyAuthn.Tests.Data.IdentityPersistenceTests.Startup_initializer_applies_pending_migrations
```

### Required PostgreSQL execution attempts

Focused command:

```powershell
dotnet test PasskeyAuthn.Tests/PasskeyAuthn.Tests.csproj --filter FullyQualifiedName~IdentityPersistenceTests
```

Full Release command:

```powershell
dotnet test --configuration Release
```

Both commands compiled the application and tests, then failed before executing a test assertion because Testcontainers could not connect to Docker:

```text
DotNet.Testcontainers.Builders.DockerUnavailableException : Docker is either not running or misconfigured.
Details:
  Failed to connect to Docker endpoint at 'npipe://./pipe/docker_engine'.
```

The focused command reported `Failed: 4, Passed: 0, Total: 4`; the full Release command reported `Failed: 5, Passed: 0, Total: 5`. These are environment failures, not substitutions for PostgreSQL test results.

## Concerns

1. Docker Desktop/Linux engine is not running or accessible in this workspace. Start Docker and rerun the focused command followed by `dotnet test --configuration Release` to obtain the required real PostgreSQL green evidence.
2. Resolved in Fix Round 1: setting `options.Stores.SchemaVersion = IdentitySchemaVersions.Version3` preserves the exact approved context base type and adds built-in `AspNetUserPasskeys` storage.

## Commits

None. The workspace has no Git repository and no Git initialization or commit was performed.

---

# Fix Round 1 Report

## Review findings addressed

1. `Program.cs` now sets `options.Stores.SchemaVersion = IdentitySchemaVersions.Version3` inside the existing `AddIdentityCore<ApplicationUser>` configuration. `ApplicationDbContext` remains exactly `IdentityDbContext<ApplicationUser>, IDataProtectionKeyContext`.
2. `InitialIdentityAndDataProtection` was regenerated. Its migration and model snapshot now include `AspNetUserPasskeys` and `IdentityUserPasskey<string>`.
3. `Migration_creates_DataProtectionKeys_table` now queries PostgreSQL's `information_schema.tables` catalog and asserts table existence without depending on table rows.
4. `Migration_creates_AspNetUserPasskeys_table` adds the equivalent real PostgreSQL catalog assertion for built-in Passkey storage.
5. `Startup_initializer_applies_pending_migrations_to_fresh_schema` creates a unique PostgreSQL schema, asserts migrations are pending, invokes `DatabaseInitializer.InitializeAsync`, then asserts there are no pending migrations. It no longer depends on migration side effects from the shared test host.

## Files changed in this round

- Modified `PasskeyAuthn/Program.cs`
- Modified `PasskeyAuthn.Tests/Data/IdentityPersistenceTests.cs`
- Modified `PasskeyAuthn.Tests/Infrastructure/PostgresFixture.cs`
- Created `PasskeyAuthn.Tests/Data/IdentitySchemaConfigurationTests.cs`
- Recreated `PasskeyAuthn/Migrations/20260804111409_InitialIdentityAndDataProtection.cs`
- Recreated `PasskeyAuthn/Migrations/20260804111409_InitialIdentityAndDataProtection.Designer.cs`
- Recreated `PasskeyAuthn/Migrations/ApplicationDbContextModelSnapshot.cs`
- Modified this report

## TDD evidence

### RED: production Identity configuration omits the Passkey entity

Command:

```powershell
dotnet test PasskeyAuthn.Tests/PasskeyAuthn.Tests.csproj --filter FullyQualifiedName~IdentitySchemaConfigurationTests
```

Output before setting schema version 3:

```text
Failed PasskeyAuthn.Tests.Data.IdentitySchemaConfigurationTests.Production_identity_configuration_includes_builtin_passkey_entity
Assert.NotNull() Failure: Value is null
Failed!  - Failed:     1, Passed:     0, Skipped:     0, Total:     1
```

The test hosts the actual `Program.cs` composition root with EF design-time startup enabled solely to prevent database migration. It verifies the resulting EF model rather than scanning source text.

### GREEN: schema version 3 includes the Passkey entity

Command:

```powershell
dotnet test PasskeyAuthn.Tests/PasskeyAuthn.Tests.csproj --filter FullyQualifiedName~IdentitySchemaConfigurationTests
```

Output after adding `options.Stores.SchemaVersion = IdentitySchemaVersions.Version3`:

```text
Passed!  - Failed:     0, Passed:     1, Skipped:     0, Total:     1
```

### Migration regeneration

Command:

```powershell
dotnet ef migrations add InitialIdentityAndDataProtection --project PasskeyAuthn/PasskeyAuthn.csproj --output-dir Migrations
```

Output:

```text
Build started...
Build succeeded.
Done. To undo this action, use 'ef migrations remove'
```

Verification of the generated artifact:

```text
PasskeyAuthn\Migrations\20260804111409_InitialIdentityAndDataProtection.cs:132: name: "AspNetUserPasskeys"
PasskeyAuthn\Migrations\ApplicationDbContextModelSnapshot.cs:144: modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserPasskey<string>", ...)
```

`dotnet ef` emitted the existing non-blocking warning: installed tool `10.0.9`, runtime package `10.0.10`.

## PostgreSQL execution and Release verification

Focused command:

```powershell
dotnet test PasskeyAuthn.Tests/PasskeyAuthn.Tests.csproj --filter FullyQualifiedName~IdentityPersistenceTests
```

The test project compiled and discovered all five persistence tests, then Testcontainers failed before any database assertion because Docker is unavailable:

```text
DotNet.Testcontainers.Builders.DockerUnavailableException : Docker is either not running or misconfigured.
Details:
  Failed to connect to Docker endpoint at 'npipe://./pipe/docker_engine'.
```

Summary: `Failed: 5, Passed: 0, Total: 5` (environment failure at fixture construction).

Full command:

```powershell
dotnet test --configuration Release
```

Summary: `Failed: 6, Passed: 1, Skipped: 0, Total: 7`. The passing test is `IdentitySchemaConfigurationTests`; the remaining five persistence tests and the PostgreSQL-backed health smoke test fail at Testcontainers fixture construction with the same exact `DockerUnavailableException` above.

## Concerns

Docker Desktop/Linux engine remains unavailable at `npipe://./pipe/docker_engine`. Start Docker and rerun the focused persistence command followed by the full Release suite to obtain real PostgreSQL green evidence for the catalog and fresh-schema initializer assertions. No in-memory provider was added.
