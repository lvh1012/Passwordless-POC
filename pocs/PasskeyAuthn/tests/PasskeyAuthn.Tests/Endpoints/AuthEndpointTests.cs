using System.Net;
using PasskeyAuthn.Tests.Infrastructure;
using Xunit;

namespace PasskeyAuthn.Tests.Endpoints;

/// <summary>
/// Verifies logout authorization and authentication-cookie removal through the real application host.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class AuthEndpointTests : IDisposable
{
    private readonly PasskeyWebApplicationFactory _factory;

    /// <summary>
    /// Creates an authentication endpoint test host backed by the shared PostgreSQL container.
    /// </summary>
    public AuthEndpointTests(PostgresFixture postgres)
    {
        _factory = new PasskeyWebApplicationFactory(postgres);
    }

    /// <summary>
    /// Releases the endpoint test host.
    /// </summary>
    public void Dispose() => _factory.Dispose();

    [Fact]
    /// <summary>
    /// Verifies anonymous callers cannot invoke the logout operation.
    /// </summary>
    public async Task Logout_requires_authentication()
    {
        using var client = await AntiforgeryHttpClient.CreateAsync(_factory);

        using var response = await client.PostAsync("/api/auth/logout", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    /// <summary>
    /// Verifies logout expires the application authentication cookie.
    /// </summary>
    public async Task Logout_clears_authentication_cookie()
    {
        using var client = await AntiforgeryHttpClient.CreateAsync(_factory, authenticated: true);

        using var response = await client.PostAsync("/api/auth/logout", content: null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), value =>
            value.Contains(".AspNetCore.Identity.Application=", StringComparison.Ordinal) &&
            value.Contains("expires=Thu, 01 Jan 1970", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    /// <summary>
    /// Verifies an authenticated logout request cannot change session state without antiforgery proof.
    /// </summary>
    public async Task Logout_requires_antiforgery_for_authenticated_callers()
    {
        using var client = await AntiforgeryHttpClient.CreateAsync(_factory, authenticated: true);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");

        using var response = await client.PostAsync("/api/auth/logout", content: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
