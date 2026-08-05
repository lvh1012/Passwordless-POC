using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using PasskeyAuthn.Security;
using Xunit;

namespace PasskeyAuthn.Tests.Security;

/// <summary>
/// Verifies cookie authentication produces HTTP semantics appropriate to browser pages and JSON APIs.
/// </summary>
public sealed class AuthenticationCookieEventsTests
{
    [Fact]
    /// <summary>
    /// Verifies an anonymous API challenge returns 401 without an HTML redirect.
    /// </summary>
    public async Task Api_challenge_returns_unauthorized_without_location()
    {
        var context = CreateRedirectContext("/api/auth/logout");

        await AuthenticationCookieEvents.RedirectToLoginAsync(context);

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.False(context.Response.Headers.ContainsKey("Location"));
    }

    [Fact]
    /// <summary>
    /// Verifies a protected Razor Page challenge returns to the implemented login page exactly.
    /// </summary>
    public async Task Page_challenge_redirects_to_root()
    {
        var context = CreateRedirectContext("/dashboard");

        await AuthenticationCookieEvents.RedirectToLoginAsync(context);

        Assert.Equal("/", context.Response.Headers.Location);
    }

    [Fact]
    /// <summary>
    /// Verifies API authorization denial returns 403 without an HTML redirect.
    /// </summary>
    public async Task Api_access_denied_returns_forbidden_without_location()
    {
        var context = CreateRedirectContext("/api/passkeys/register/options");

        await AuthenticationCookieEvents.RedirectToAccessDeniedAsync(context);

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.False(context.Response.Headers.ContainsKey("Location"));
    }

    private static RedirectContext<CookieAuthenticationOptions> CreateRedirectContext(string path)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = path;
        var scheme = new AuthenticationScheme(
            IdentityConstants.ApplicationScheme,
            displayName: null,
            typeof(CookieAuthenticationHandler));

        return new RedirectContext<CookieAuthenticationOptions>(
            httpContext,
            scheme,
            new CookieAuthenticationOptions(),
            new AuthenticationProperties(),
            redirectUri: $"/Account/Login?ReturnUrl={Uri.EscapeDataString(path)}");
    }
}
