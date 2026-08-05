using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PasskeyAuthn.Tests.Infrastructure;
using Xunit;

namespace PasskeyAuthn.Tests.Security;

/// <summary>
/// Verifies runtime security settings while keeping TestServer transport overrides test-only.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class RuntimeConfigurationTests
{
    private readonly PostgresFixture _postgres;

    /// <summary>
    /// Initializes runtime tests with the shared real PostgreSQL fixture.
    /// </summary>
    public RuntimeConfigurationTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact]
    /// <summary>
    /// Verifies the application cookie remains secure, HTTP-only, and Lax before any test-only override.
    /// </summary>
    public void Application_cookie_uses_secure_http_only_lax_settings()
    {
        using var factory = CreateSecureRuntimeFactory(_postgres);

        var options = factory.Services
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(IdentityConstants.ApplicationScheme);

        Assert.True(options.Cookie.HttpOnly);
        Assert.Equal(CookieSecurePolicy.Always, options.Cookie.SecurePolicy);
        Assert.Equal(SameSiteMode.Lax, options.Cookie.SameSite);
    }

    [Fact]
    /// <summary>
    /// Verifies the HTTP cookie override is limited to the dedicated Testing host.
    /// </summary>
    public void Testing_factory_alone_relaxes_cookie_transport_for_testserver()
    {
        using var factory = new PasskeyWebApplicationFactory(_postgres);

        var options = factory.Services
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(IdentityConstants.ApplicationScheme);

        Assert.Equal(CookieSecurePolicy.SameAsRequest, options.Cookie.SecurePolicy);
    }

    [Fact]
    /// <summary>
    /// Verifies Render's proxy scheme header is enabled without relying on stable proxy IP ranges.
    /// </summary>
    public void Render_forwarded_header_options_accept_proxy_scheme()
    {
        using var factory = CreateSecureRuntimeFactory(_postgres);
        var options = factory.Services.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

        Assert.Equal(ForwardedHeaders.XForwardedProto, options.ForwardedHeaders);
        Assert.Empty(options.KnownIPNetworks);
        Assert.Empty(options.KnownProxies);
    }

    private static WebApplicationFactory<Program> CreateSecureRuntimeFactory(PostgresFixture postgres) =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                // Testing is the only environment allowed to use the fixture's intentionally non-TLS PostgreSQL endpoint.
                builder.UseEnvironment("Testing");
                builder.ConfigureAppConfiguration((_, configuration) =>
                {
                    configuration.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["ConnectionStrings:Default"] = postgres.ConnectionString,
                        });
                });
            });
}
