# Task 2: Add Identity, Supabase PostgreSQL persistence, and Data Protection key storage

## Files

- Modify `PasskeyAuthn/Program.cs`.
- Modify `PasskeyAuthn/PasskeyAuthn.csproj`.
- Create `PasskeyAuthn/Data/ApplicationUser.cs`.
- Create `PasskeyAuthn/Data/ApplicationDbContext.cs`.
- Create `PasskeyAuthn/Data/DatabaseInitializer.cs`.
- Create `PasskeyAuthn/appsettings.json`.
- Create `PasskeyAuthn/appsettings.Development.json`.
- Create `PasskeyAuthn.Tests/Infrastructure/PostgresFixture.cs`.
- Create `PasskeyAuthn.Tests/Infrastructure/PasskeyWebApplicationFactory.cs`.
- Create `PasskeyAuthn.Tests/Data/IdentityPersistenceTests.cs`.
- Create `PasskeyAuthn/Migrations/` with EF-generated files for migration `InitialIdentityAndDataProtection`.
- Modify `PasskeyAuthn.Tests/PasskeyAuthn.Tests.csproj` only as needed for real PostgreSQL integration tests.

## Required interfaces and constraints

- Use `.NET 10` and keep both projects at `net10.0`.
- Use PostgreSQL through EF Core/Npgsql; production configuration is the Supabase PostgreSQL connection string named `ConnectionStrings:Default`.
- Do not add a fake in-memory database provider and do not add a second application database.
- `ApplicationUser : IdentityUser` exposes only `DateTimeOffset? PasskeyRegistrationExpiresAt`.
- `ApplicationDbContext : IdentityDbContext<ApplicationUser>, IDataProtectionKeyContext` exposes Identity sets and `DbSet<DataProtectionKey> DataProtectionKeys`.
- `DatabaseInitializer.InitializeAsync(IServiceProvider, CancellationToken)` applies pending migrations and throws a startup error for an unusable/missing database configuration.
- `PasskeyWebApplicationFactory` replaces the production connection string with the fixture PostgreSQL connection string and configures a `Testing` environment. It may use `CookieSecurePolicy.SameAsRequest` only in the test host; production remains secure-only.
- Configure Identity with `RequireUniqueEmail = true` and `RequireConfirmedAccount = false`. The POC uses email as an identifier but does not claim ownership verification.
- Register `AddIdentityCore<ApplicationUser>`, sign-in manager, EF stores, default token providers, application cookie scheme, and `PersistKeysToDbContext<ApplicationDbContext>`.
- Configure the application cookie as `HttpOnly = true`, `SecurePolicy = CookieSecurePolicy.Always`, and `SameSite = SameSiteMode.Lax` in production.
- Actual Supabase credentials must not appear in either appsettings file, tests, comments, logs, or reports.
- Every changed public class/API must have XML documentation. Comments must explain non-obvious persistence/security/platform decisions.

## TDD sequence (mandatory)

Write the persistence tests first and run them to observe the expected failure before adding production persistence code. Then implement the minimum needed, run focused tests to green, refactor only while green, and run the complete Release test suite.

## Required persistence tests

Using a reusable `PostgresFixture` backed by `Testcontainers.PostgreSql` and a real PostgreSQL container:

1. Create an `ApplicationUser` with a unique email and persist it.
2. Load the user by normalized email.
3. Confirm the Data Protection key table exists after migration.
4. Confirm the application startup initializer applies pending migrations.

Do not fake cryptographic or database behavior. Tests must exercise PostgreSQL.

## Required implementation

In `Program.cs`, register the equivalent of:

```csharp
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddSignInManager()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddIdentityCookies();

builder.Services.AddDataProtection()
    .PersistKeysToDbContext<ApplicationDbContext>();
```

Configure the authentication cookie with `HttpOnly`, `SecurePolicy.Always`, and `SameSite.Lax`. Add `UseAuthentication()` before `UseAuthorization()` if authorization middleware is registered by the current host.

Create the migration with:

```powershell
dotnet ef migrations add InitialIdentityAndDataProtection --project PasskeyAuthn/PasskeyAuthn.csproj --output-dir Migrations
```

Implement `DatabaseInitializer.InitializeAsync` using `Database.MigrateAsync()`, call it in a scoped startup block after the app is built, and fail startup rather than serving an app that cannot persist authentication data. Keep `/health` temporarily compatible with the existing smoke test; Task 5 will replace it with readiness behavior.

## Verification

First run the focused persistence tests (they should have been observed failing before implementation):

```powershell
dotnet test PasskeyAuthn.Tests/PasskeyAuthn.Tests.csproj --filter FullyQualifiedName~IdentityPersistenceTests
```

Then run:

```powershell
dotnet test --configuration Release
```

Report the exact red and green commands/output. If Docker is unavailable for Testcontainers, report that as a concern and still validate compile-level behavior; do not replace PostgreSQL with an in-memory provider.

## Scope boundary

Do not implement passkey endpoints, registration-state protection, browser UI, Docker, Render, or GitHub Actions in this task. Do not initialize Git or create commits because this workspace has no Git repository.

