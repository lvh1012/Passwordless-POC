using PasskeyAuthn.Tests.Infrastructure;
using Xunit;

namespace PasskeyAuthn.Tests;

/// <summary>
/// Verifies that the application host can start and expose its liveness endpoint.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class SmokeTests : IDisposable
{
    private readonly PasskeyWebApplicationFactory _factory;
    private readonly HttpClient _client;

    /// <summary>
    /// Initializes the test client backed by the real application entry point.
    /// </summary>
    public SmokeTests(PostgresFixture postgres)
    {
        _factory = new PasskeyWebApplicationFactory(postgres);
        _client = _factory.CreateClient();
    }

    /// <summary>
    /// Releases the application host after the smoke test completes.
    /// </summary>
    public void Dispose() => _factory.Dispose();

    [Fact]
    /// <summary>
    /// Verifies that the temporary health endpoint returns an HTTP success response.
    /// </summary>
    public async Task Health_endpoint_returns_success()
    {
        using var response = await _client.GetAsync("/health");

        Assert.True(response.IsSuccessStatusCode);
    }
}
