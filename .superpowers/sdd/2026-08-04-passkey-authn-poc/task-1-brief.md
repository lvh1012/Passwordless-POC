# Task 1: Convert the skeleton into a testable ASP.NET Core web solution

## Files

- Modify `PasskeyAuthn/PasskeyAuthn.csproj`.
- Modify `PasswordlessAuthn.slnx`.
- Delete `PasskeyAuthn/Class1.cs`.
- Create `PasskeyAuthn/Program.cs`.
- Create `PasskeyAuthn.Tests/PasskeyAuthn.Tests.csproj`.
- Create `PasskeyAuthn.Tests/SmokeTests.cs`.

## Required interfaces and constraints

- Use `.NET 10`; the application target framework remains `net10.0`.
- Convert the application to `Microsoft.NET.Sdk.Web`.
- The web host exposes a `WebApplication` entry point discoverable by `WebApplicationFactory<Program>` through `public partial class Program { }`.
- The solution contains the application and test project.
- The test project references the application and `Microsoft.AspNetCore.Mvc.Testing` and is runnable with `dotnet test` from the solution root.
- Preserve nullable reference types and implicit usings.
- Add the runtime packages needed by later tasks: `Microsoft.AspNetCore.Identity.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Design`, `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore`, and `Npgsql.EntityFrameworkCore.PostgreSQL`, using versions compatible with .NET 10. Do not add a third-party FIDO2/WebAuthn library.
- Every changed public class/API needs XML documentation appropriate to C#; comments must explain non-obvious decisions rather than restating trivial code.

## Required behavior

Create a smoke test using `WebApplicationFactory<Program>` that sends `GET /health` and expects HTTP 200. The initial skeleton does not have the host or endpoint; after implementation the focused test must pass.

Create `Program.cs` with a minimal `WebApplication` that enables Razor Pages, static files, routing, and a temporary `GET /health` endpoint returning `Results.Ok()`. Keep the host minimal; later tasks will replace the temporary endpoint with the database readiness endpoint.

## Verification

Run the focused test:

```powershell
dotnet test PasskeyAuthn.Tests/PasskeyAuthn.Tests.csproj --filter FullyQualifiedName~SmokeTests
```

Then run a solution-level build or test if practical. Report exact commands and outputs in the report.

## Scope boundary

Do not implement Identity persistence, passkey endpoints, browser UI, Docker, Render, or GitHub Actions in this task. Do not initialize Git or create commits because this workspace has no Git repository.

