using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PasskeyAuthn.Tests.Infrastructure;

/// <summary>
/// Hosts the application with the PostgreSQL container instead of production configuration.
/// </summary>
public sealed class PasskeyWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly PostgresFixture _postgres;

    /// <summary>
    /// Initializes the factory with the PostgreSQL fixture shared by persistence tests.
    /// </summary>
    public PasskeyWebApplicationFactory(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            // The test host must never read a real deployment connection string.
            configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Default"] = _postgres.ConnectionString,
                });
        });
        builder.ConfigureServices(services =>
        {
            // TestServer uses HTTP, so only the test host relaxes transport enforcement for cookie round-trips.
            services.ConfigureApplicationCookie(options =>
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest);
        });
    }
}
