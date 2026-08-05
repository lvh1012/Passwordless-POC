using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using PasskeyAuthn.Tests.Infrastructure;
using Xunit;

namespace PasskeyAuthn.Tests.Pages;

/// <summary>
/// Verifies the browser-facing Razor Pages render through the real PostgreSQL-backed host.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class PageSmokeTests : IDisposable
{
    private readonly PasskeyWebApplicationFactory _factory;

    /// <summary>
    /// Initializes the page test host with the shared real PostgreSQL fixture.
    /// </summary>
    public PageSmokeTests(PostgresFixture postgres)
    {
        _factory = new PasskeyWebApplicationFactory(postgres);
    }

    /// <summary>
    /// Releases the browser test host after each test.
    /// </summary>
    public void Dispose() => _factory.Dispose();

    [Fact]
    /// <summary>
    /// Verifies the login page exposes the email input and Passkey login action.
    /// </summary>
    public async Task Login_page_contains_email_field_and_login_action()
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync("/");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("id=\"email\"", body, StringComparison.Ordinal);
        Assert.Contains("Sign in with passkey", body, StringComparison.Ordinal);
    }

    [Fact]
    /// <summary>
    /// Verifies the registration page exposes the Passkey registration action.
    /// </summary>
    public async Task Registration_page_contains_registration_action()
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync("/register");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Create passkey", body, StringComparison.Ordinal);
    }

    [Fact]
    /// <summary>
    /// Verifies an anonymous dashboard request redirects to the existing login page.
    /// </summary>
    public async Task Dashboard_redirects_anonymous_client_to_login_page()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
        using var response = await client.GetAsync("/dashboard");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Headers.Location?.OriginalString);
    }

    [Fact]
    /// <summary>
    /// Verifies the shared layout emits the CSRF token and WebAuthn client reference.
    /// </summary>
    public async Task Shared_layout_contains_antiforgery_meta_tag_and_passkey_script()
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync("/");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("name=\"csrf-token\"", body, StringComparison.Ordinal);
        Assert.Contains("src=\"/js/passkey.js\"", body, StringComparison.Ordinal);
    }

    [Fact]
    /// <summary>
    /// Verifies the protected dashboard renders its logout action and authenticated email marker.
    /// </summary>
    public async Task Dashboard_contains_logout_action_and_authenticated_email()
    {
        using var client = await AntiforgeryHttpClient.CreateAsync(_factory, authenticated: true);
        using var response = await client.GetAsync("/dashboard");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("id=\"authenticated-email\"", body, StringComparison.Ordinal);
        Assert.Contains("test@example.test", body, StringComparison.Ordinal);
        Assert.Contains("Sign out", body, StringComparison.Ordinal);
    }
}
