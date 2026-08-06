# Passkey health, options validation, and Docker Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace custom readiness and manual production configuration validation with native ASP.NET Core mechanisms, and make the .NET 10 container port/runtime configurable.

**Architecture:** `Program.cs` registers native health checks and maps `/health`. `ProductionConfigurationValidator` implements `IValidateOptions<PasskeySettings>` and reads the connection string from configuration; `ValidateOnStart` runs it before migrations. The Dockerfile uses Noble SDK/runtime images and configures `ASPNETCORE_HTTP_PORTS` through an overridable environment variable.

**Tech Stack:** ASP.NET Core .NET 10, Options pattern, `IValidateOptions<T>`, EF Core health checks, Docker multi-stage build, xUnit.

## Global Constraints

- Delete `pocs/PasskeyAuthn/src/PasskeyAuthn/Endpoints/HealthEndpointExtensions.cs`.
- Keep the `/health` route and database readiness semantics.
- Do not hard-code application port `8080`; configure it with an environment variable and expose it from Dockerfile.
- Use `mcr.microsoft.com/dotnet/sdk:10.0-noble` and `mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra`.
- Preserve safe validation messages and existing Passkey/database invariants.

## File Map

- Modify `pocs/PasskeyAuthn/src/PasskeyAuthn/Program.cs`: native health checks, options registration, startup validation, and port configuration.
- Modify `pocs/PasskeyAuthn/src/PasskeyAuthn/Configuration/ProductionConfigurationValidator.cs`: implement `IValidateOptions<PasskeySettings>` while retaining validation rules.
- Modify `pocs/PasskeyAuthn/tests/PasskeyAuthn.Tests/Configuration/ProductionConfigurationValidatorTests.cs`: test the options validator contract.
- Modify `pocs/PasskeyAuthn/tests/PasskeyAuthn.Tests/Configuration/ProductionStartupValidationTests.cs`: test options validation at startup.
- Modify `pocs/PasskeyAuthn/tests/PasskeyAuthn.Tests/Endpoints/HealthEndpointTests.cs`: retain native `/health` integration coverage.
- Modify `pocs/PasskeyAuthn/Dockerfile`: requested images, runtime port environment, and exposed port.
- Modify `pocs/PasskeyAuthn/docs/deployment.md` and `pocs/PasskeyAuthn/README.md`: document environment-configured port without stale hard-coded application behavior.
- Delete `pocs/PasskeyAuthn/src/PasskeyAuthn/Endpoints/HealthEndpointExtensions.cs`.

### Task 1: Add failing options-validator contract tests

**Files:**
- Modify: `pocs/PasskeyAuthn/tests/PasskeyAuthn.Tests/Configuration/ProductionConfigurationValidatorTests.cs`

- [ ] Add tests that construct the validator with an `IConfiguration`, call `Validate(Options.DefaultName, settings)`, and assert `ValidateOptionsResult.Success` for valid values and `Fail` containing the relevant setting key for invalid values.
- [ ] Run `dotnet test pocs/PasskeyAuthn/tests/PasskeyAuthn.Tests/PasskeyAuthn.Tests.csproj --filter FullyQualifiedName~ProductionConfigurationValidatorTests`.
- [ ] Confirm the new contract tests fail because the current validator is static and does not implement `IValidateOptions<PasskeySettings>`.

### Task 2: Implement options validation and native health checks

**Files:**
- Modify: `pocs/PasskeyAuthn/src/PasskeyAuthn/Configuration/ProductionConfigurationValidator.cs`
- Modify: `pocs/PasskeyAuthn/src/PasskeyAuthn/Program.cs`
- Delete: `pocs/PasskeyAuthn/src/PasskeyAuthn/Endpoints/HealthEndpointExtensions.cs`

- [ ] Implement `IValidateOptions<PasskeySettings>` with `Validate(string? name, PasskeySettings settings)` returning `ValidateOptionsResult`; keep the existing validation rules and avoid including connection-string values in failure messages.
- [ ] Register the validator and bind options with `.ValidateOnStart()` so the options pipeline runs before database initialization; remove the manual static validation and temporary bound-settings object.
- [ ] Register `AddHealthChecks().AddDbContextCheck<ApplicationDbContext>()` and map `app.MapHealthChecks("/health")`.
- [ ] Remove `UseUrls` and configure the listener through the standard ASP.NET Core environment port mechanism rather than an application `8080` fallback.
- [ ] Run the focused configuration and health tests.

### Task 3: Update Docker runtime configuration

**Files:**
- Modify: `pocs/PasskeyAuthn/Dockerfile`

- [ ] Change build image to `mcr.microsoft.com/dotnet/sdk:10.0-noble`.
- [ ] Change runtime image to `mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra`.
- [ ] Set `ASPNETCORE_HTTP_PORTS` to an overridable container default, expose the same port, copy the published output, and retain the JSON entrypoint.
- [ ] Run a Dockerfile text check and, when Docker is available, build the image using `pocs/PasskeyAuthn` as context.

### Task 4: Update deployment documentation and regression tests

**Files:**
- Modify: `pocs/PasskeyAuthn/tests/PasskeyAuthn.Tests/Configuration/ProductionStartupValidationTests.cs`
- Modify: `pocs/PasskeyAuthn/tests/PasskeyAuthn.Tests/Endpoints/HealthEndpointTests.cs`
- Modify: `pocs/PasskeyAuthn/README.md`
- Modify: `pocs/PasskeyAuthn/docs/deployment.md`

- [ ] Assert production startup failure comes from options validation before migration and still identifies the relevant configuration key.
- [ ] Keep health tests focused on native `/health` status behavior and non-disclosure.
- [ ] Document the configurable `ASPNETCORE_HTTP_PORTS` variable and remove stale claims that the application itself defaults to port `8080`.
- [ ] Run the full POC test/build commands and inspect the final diff for unrelated changes.

## Verification Checklist

- [ ] The custom health extension file is absent and no call to `MapHealthEndpoints` remains.
- [ ] `IValidateOptions<PasskeySettings>` is registered and `ValidateOnStart` is present.
- [ ] No application source contains a hard-coded `8080` listener fallback.
- [ ] Dockerfile has both requested image tags, a configurable port environment, and matching `EXPOSE`.
- [ ] Focused tests, full tests, Release build, and Docker validation provide fresh exit-code evidence.
