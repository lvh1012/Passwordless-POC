# Passkey health, options validation, and Docker design

## Goal

Remove the custom health endpoint implementation, use ASP.NET Core native health checks, validate `PasskeySettings` through `IValidateOptions`, make the listening port environment-configurable, and update the container images to the requested .NET 10 Noble variants.

## Design

- Register `AddHealthChecks().AddDbContextCheck<ApplicationDbContext>()` and map the existing `/health` route with `MapHealthChecks`. The native middleware preserves the readiness behavior: database availability produces healthy/unhealthy HTTP status codes without serializing connection details.
- Convert `ProductionConfigurationValidator` into an `IValidateOptions<PasskeySettings>` implementation. It receives the configured database connection string through `IConfiguration`, returns `ValidateOptionsResult.Fail` with key-specific safe messages, and is invoked at startup with `ValidateOnStart` before database initialization.
- Remove the manual production validation call and the custom `HealthEndpointExtensions` file. Keep the existing settings validation rules and `/health` route contract.
- Remove `UseUrls` and the application-level `8080` fallback. Configure `ASPNETCORE_HTTP_PORTS` in the runtime image and expose that port in the Dockerfile; deployment environments can override the variable.
- Use `mcr.microsoft.com/dotnet/sdk:10.0-noble` for build and `mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra` for runtime. Publish only the application output and retain the existing POC build context.

## Testing

- Unit tests exercise the `IValidateOptions<PasskeySettings>` result for valid and invalid settings, including safe error messages.
- Integration tests verify `/health` returns success when PostgreSQL is available and a non-success status when it is stopped.
- Startup tests verify invalid production settings fail during options startup validation.
- Static/Docker checks verify the custom health file and hard-coded application port are gone, requested base images are present, and the runtime port is exposed/configured.

