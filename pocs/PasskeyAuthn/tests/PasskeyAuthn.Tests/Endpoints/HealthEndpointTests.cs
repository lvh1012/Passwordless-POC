using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using PasskeyAuthn.Tests.Infrastructure;
using Testcontainers.PostgreSql;
using Xunit;

namespace PasskeyAuthn.Tests.Endpoints;

/// <summary>
/// Verifies that the readiness route reflects the real PostgreSQL availability without disclosing configuration.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class HealthEndpointTests : IDisposable
{
    private readonly PasskeyWebApplicationFactory _factory;
    private readonly HttpClient _client;

    /// <summary>
    /// Initializes readiness tests with the PostgreSQL Testcontainer used by the real application host.
    /// </summary>
    public HealthEndpointTests(PostgresFixture postgres)
    {
        _factory = new PasskeyWebApplicationFactory(postgres);
        _client = _factory.CreateClient();
    }

    /// <summary>
    /// Releases the real application host after each readiness test.
    /// </summary>
    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    /// <summary>
    /// Verifies a migrated PostgreSQL-backed application reports readiness successfully.
    /// </summary>
    public async Task Health_endpoint_with_available_postgresql_returns_success()
    {
        using var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    /// <summary>
    /// Verifies readiness never serializes the configured PostgreSQL connection details.
    /// </summary>
    public async Task Health_endpoint_does_not_expose_connection_details()
    {
        using var response = await _client.GetAsync("/health");
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode);
        Assert.DoesNotContain("Host=", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password=", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("postgres", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    /// <summary>
    /// Verifies readiness changes to a generic non-success response when its real PostgreSQL dependency is unavailable.
    /// </summary>
    public async Task Health_endpoint_with_unavailable_postgresql_returns_generic_non_success()
    {
        await using var container = new PostgreSqlBuilder("postgres:16-alpine").Build();
        await container.StartAsync();
        using var factory = CreateFactory(container.GetConnectionString());
        using var client = factory.CreateClient();

        await container.StopAsync();

        using var response = await client.GetAsync("/health");
        var body = await response.Content.ReadAsStringAsync();

        Assert.False(response.IsSuccessStatusCode);
        Assert.DoesNotContain("Host=", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password=", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("postgres", body, StringComparison.OrdinalIgnoreCase);
    }

    private static WebApplicationFactory<Program> CreateFactory(string connectionString) =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureAppConfiguration((_, configuration) =>
                {
                    configuration.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["ConnectionStrings:Default"] = connectionString,
                        });
                });
            });
}

/// <summary>
/// Verifies missing database configuration prevents the host from serving any endpoint.
/// </summary>
public sealed class MissingDatabaseConfigurationTests
{
    [Fact]
    /// <summary>
    /// Verifies startup fails closed when no database connection string is configured, so no false healthy route is served.
    /// </summary>
    public void Missing_connection_string_prevents_application_startup()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureAppConfiguration((_, configuration) =>
                {
                    configuration.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["ConnectionStrings:Default"] = string.Empty,
                        });
                });
            });

        var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

        Assert.Contains("ConnectionStrings:Default", exception.Message, StringComparison.Ordinal);
    }
}
