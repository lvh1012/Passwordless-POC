using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace PasskeyAuthn.Security;

/// <summary>
/// Applies browser-page redirects while preserving status-code semantics for JSON API requests.
/// </summary>
public static class AuthenticationCookieEvents
{
    /// <summary>
    /// Returns 401 for API challenges and redirects protected browser pages to the implemented login page.
    /// </summary>
    /// <param name="context">The cookie redirect context created by ASP.NET Core authentication.</param>
    /// <returns>A completed task after the response has been configured.</returns>
    public static Task RedirectToLoginAsync(RedirectContext<CookieAuthenticationOptions> context) =>
        CompleteAsync(context, StatusCodes.Status401Unauthorized);

    /// <summary>
    /// Returns 403 for API authorization failures and sends browser pages to the safe public root page.
    /// </summary>
    /// <param name="context">The cookie redirect context created by ASP.NET Core authentication.</param>
    /// <returns>A completed task after the response has been configured.</returns>
    public static Task RedirectToAccessDeniedAsync(RedirectContext<CookieAuthenticationOptions> context) =>
        CompleteAsync(context, StatusCodes.Status403Forbidden);

    private static Task CompleteAsync(RedirectContext<CookieAuthenticationOptions> context, int apiStatusCode)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = apiStatusCode;
        }
        else
        {
            // The POC has no return-url or access-denied page; root is the only implemented safe destination.
            context.Response.Redirect("/");
        }

        return Task.CompletedTask;
    }
}
