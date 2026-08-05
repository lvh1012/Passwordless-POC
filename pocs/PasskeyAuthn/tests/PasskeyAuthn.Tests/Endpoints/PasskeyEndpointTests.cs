using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PasskeyAuthn.Data;
using PasskeyAuthn.Models;
using PasskeyAuthn.Tests.Infrastructure;
using Xunit;

namespace PasskeyAuthn.Tests.Endpoints;

/// <summary>
/// Verifies Passkey endpoint validation and persistence against the real application host and PostgreSQL.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class PasskeyEndpointTests : IDisposable
{
    private readonly PasskeyWebApplicationFactory _factory;

    /// <summary>
    /// Creates an endpoint test host backed by the shared PostgreSQL container.
    /// </summary>
    public PasskeyEndpointTests(PostgresFixture postgres)
    {
        _factory = new PasskeyWebApplicationFactory(postgres);
    }

    /// <summary>
    /// Releases the endpoint test host.
    /// </summary>
    public void Dispose() => _factory.Dispose();

    [Fact]
    /// <summary>
    /// Verifies invalid email input receives a stable client-safe error.
    /// </summary>
    public async Task Registration_options_with_invalid_email_returns_safe_bad_request()
    {
        using var client = await AntiforgeryHttpClient.CreateAsync(_factory);

        using var response = await client.PostAsJsonAsync("/api/passkeys/register/options", new { Email = "not-an-email" });
        var error = await response.Content.ReadFromJsonAsync<ApiError>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(new ApiError("invalid_email", "A valid email address is required."), error);
    }

    [Fact]
    /// <summary>
    /// Verifies malformed option JSON is converted to the stable invalid-email contract.
    /// </summary>
    public async Task Registration_options_with_malformed_json_returns_safe_invalid_email()
    {
        using var client = await AntiforgeryHttpClient.CreateAsync(_factory);
        using var malformedBody = new StringContent("{", Encoding.UTF8, "application/json");

        using var response = await client.PostAsync("/api/passkeys/register/options", malformedBody);
        var error = await response.Content.ReadFromJsonAsync<ApiError>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(new ApiError("invalid_email", "A valid email address is required."), error);
    }

    [Fact]
    /// <summary>
    /// Verifies a missing option body is converted to the stable invalid-email contract.
    /// </summary>
    public async Task Login_options_with_empty_body_returns_safe_invalid_email()
    {
        using var client = await AntiforgeryHttpClient.CreateAsync(_factory);

        using var response = await client.PostAsync("/api/passkeys/login/options", content: null);
        var error = await response.Content.ReadFromJsonAsync<ApiError>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(new ApiError("invalid_email", "A valid email address is required."), error);
    }

    [Fact]
    /// <summary>
    /// Verifies an email used as the display name remains valid at the approved 100-character boundary.
    /// </summary>
    public async Task Registration_options_accept_email_at_display_name_limit()
    {
        var email = CreateEmailOfLength(100);
        using var client = await AntiforgeryHttpClient.CreateAsync(_factory);

        using var response = await client.PostAsJsonAsync("/api/passkeys/register/options", new { Email = email });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    /// <summary>
    /// Verifies an email used as the display name is rejected immediately above the approved limit.
    /// </summary>
    public async Task Registration_options_reject_email_above_display_name_limit()
    {
        var email = CreateEmailOfLength(101);
        using var client = await AntiforgeryHttpClient.CreateAsync(_factory);

        using var response = await client.PostAsJsonAsync("/api/passkeys/register/options", new { Email = email });
        var error = await response.Content.ReadFromJsonAsync<ApiError>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(new ApiError("invalid_email", "A valid email address is required."), error);
    }

    [Fact]
    /// <summary>
    /// Verifies unknown accounts receive only the generic authentication failure contract.
    /// </summary>
    public async Task Login_options_with_unknown_email_does_not_reveal_account_existence()
    {
        const string email = "missing@example.test";
        using var client = await AntiforgeryHttpClient.CreateAsync(_factory);

        using var response = await client.PostAsJsonAsync("/api/passkeys/login/options", new { Email = email });
        var body = await response.Content.ReadAsStringAsync();
        var error = await response.Content.ReadFromJsonAsync<ApiError>();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(new ApiError("authentication_failed", "Authentication failed."), error);
        Assert.DoesNotContain(email, body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    /// <summary>
    /// Verifies an existing credential prevents a second public registration ceremony.
    /// </summary>
    public async Task Registration_options_for_existing_passkey_returns_conflict()
    {
        var email = $"registered-{Guid.NewGuid():N}@example.test";
        await SeedUserWithPasskeysAsync(email, count: 1);
        using var client = await AntiforgeryHttpClient.CreateAsync(_factory);

        using var response = await client.PostAsJsonAsync("/api/passkeys/register/options", new { Email = email });
        var body = await response.Content.ReadAsStringAsync();
        var error = await response.Content.ReadFromJsonAsync<ApiError>();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(new ApiError("registration_conflict", "Registration cannot be started."), error);
        Assert.DoesNotContain("account", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("passkey", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("credential", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    /// <summary>
    /// Verifies the configured Passkey limit uses the same non-disclosing registration conflict.
    /// </summary>
    public async Task Registration_options_at_passkey_limit_returns_generic_conflict()
    {
        var email = $"limited-{Guid.NewGuid():N}@example.test";
        await SeedUserWithPasskeysAsync(email, count: 3);
        using var client = await AntiforgeryHttpClient.CreateAsync(_factory);

        using var response = await client.PostAsJsonAsync("/api/passkeys/register/options", new { Email = email });
        var error = await response.Content.ReadFromJsonAsync<ApiError>();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(new ApiError("registration_conflict", "Registration cannot be started."), error);
    }

    [Fact]
    /// <summary>
    /// Verifies registration options create one pending user and reuse it on retry.
    /// </summary>
    public async Task Registration_options_create_and_reuse_pending_user()
    {
        var email = $"pending-{Guid.NewGuid():N}@example.test";
        using var firstClient = await AntiforgeryHttpClient.CreateAsync(_factory);
        using var firstResponse = await firstClient.PostAsJsonAsync("/api/passkeys/register/options", new { Email = email });
        using var secondClient = await AntiforgeryHttpClient.CreateAsync(_factory);
        using var secondResponse = await secondClient.PostAsJsonAsync("/api/passkeys/register/options", new { Email = email });

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal("application/json", firstResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.Contains(firstResponse.Headers.GetValues("Set-Cookie"), value =>
            value.Contains("passkey-registration=", StringComparison.Ordinal) &&
            value.Contains("httponly", StringComparison.OrdinalIgnoreCase) &&
            value.Contains("secure", StringComparison.OrdinalIgnoreCase) &&
            value.Contains("samesite=lax", StringComparison.OrdinalIgnoreCase));

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var normalizedEmail = email.ToUpperInvariant();
        var users = await context.Users.Where(user => user.NormalizedEmail == normalizedEmail).ToListAsync();
        Assert.Single(users);
        Assert.True(users[0].PasskeyRegistrationExpiresAt > DateTimeOffset.UtcNow);
    }

    [Fact]
    /// <summary>
    /// Verifies registration completion cannot select a user without protected server state.
    /// </summary>
    public async Task Registration_completion_without_state_returns_safe_bad_request()
    {
        using var client = await AntiforgeryHttpClient.CreateAsync(_factory);

        using var response = await client.PostAsJsonAsync(
            "/api/passkeys/register/complete",
            new { Credential = new { id = "unused" } });
        var error = await response.Content.ReadFromJsonAsync<ApiError>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(new ApiError("invalid_registration_state", "Registration could not be completed."), error);
    }

    [Fact]
    /// <summary>
    /// Verifies malformed assertion JSON returns no parser or credential details.
    /// </summary>
    public async Task Login_completion_with_invalid_credential_returns_safe_failure()
    {
        using var client = await AntiforgeryHttpClient.CreateAsync(_factory);

        using var response = await client.PostAsJsonAsync(
            "/api/passkeys/login/complete",
            new { Credential = new { invalid = true } });
        var body = await response.Content.ReadAsStringAsync();
        var error = await response.Content.ReadFromJsonAsync<ApiError>();

        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized });
        Assert.Equal("authentication_failed", error?.Code);
        Assert.Equal("Authentication failed.", error?.Message);
        Assert.DoesNotContain("credential", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("invalid", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    /// <summary>
    /// Verifies malformed credential JSON does not expose parser details.
    /// </summary>
    public async Task Login_completion_with_malformed_json_returns_safe_authentication_failure()
    {
        using var client = await AntiforgeryHttpClient.CreateAsync(_factory);
        using var malformedBody = new StringContent("{", Encoding.UTF8, "application/json");

        using var response = await client.PostAsync("/api/passkeys/login/complete", malformedBody);
        var error = await response.Content.ReadFromJsonAsync<ApiError>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(new ApiError("authentication_failed", "Authentication failed."), error);
    }

    [Fact]
    /// <summary>
    /// Verifies a missing credential body uses the same generic authentication failure.
    /// </summary>
    public async Task Login_completion_with_empty_body_returns_safe_authentication_failure()
    {
        using var client = await AntiforgeryHttpClient.CreateAsync(_factory);

        using var response = await client.PostAsync("/api/passkeys/login/complete", content: null);
        var error = await response.Content.ReadFromJsonAsync<ApiError>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(new ApiError("authentication_failed", "Authentication failed."), error);
    }

    [Fact]
    /// <summary>
    /// Verifies a JSON null credential uses the generic authentication failure.
    /// </summary>
    public async Task Login_completion_with_null_credential_returns_safe_authentication_failure()
    {
        using var client = await AntiforgeryHttpClient.CreateAsync(_factory);

        using var response = await client.PostAsJsonAsync<object>(
            "/api/passkeys/login/complete",
            new { Credential = (object?)null });
        var error = await response.Content.ReadFromJsonAsync<ApiError>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(new ApiError("authentication_failed", "Authentication failed."), error);
    }

    [Fact]
    /// <summary>
    /// Verifies state-changing Passkey routes reject requests without antiforgery proof.
    /// </summary>
    public async Task Registration_options_without_antiforgery_is_rejected()
    {
        using var client = _factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/passkeys/register/options",
            new { Email = "person@example.test" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task SeedUserWithPasskeysAsync(string email, int count)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = email, Email = email };
        Assert.True((await userManager.CreateAsync(user)).Succeeded);

        for (var index = 0; index < count; index++)
        {
            // These seeds only exercise conflict branches; no test pretends they came from valid ceremonies.
            var passkey = new UserPasskeyInfo(
                Guid.NewGuid().ToByteArray(),
                new byte[] { 1, 2, 3 },
                DateTimeOffset.UtcNow,
                0,
                [],
                true,
                false,
                false,
                [],
                []);
            Assert.True((await userManager.AddOrUpdatePasskeyAsync(user, passkey)).Succeeded);
        }
    }

    private static string CreateEmailOfLength(int length)
    {
        const string prefix = "user@";
        var email = prefix + new string('a', 63) + "." + new string('b', length - prefix.Length - 64);
        Assert.Equal(length, email.Length);
        return email;
    }
}
