using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using PasskeyAuthn.Data;
using PasskeyAuthn.Security;

namespace PasskeyAuthn.Endpoints;

/// <summary>
/// Maps authentication-session operations that are separate from Passkey ceremonies.
/// </summary>
public static class AuthEndpointExtensions
{
    /// <summary>
    /// Maps the authorized, antiforgery-protected logout endpoint.
    /// </summary>
    /// <param name="endpoints">The application's endpoint route builder.</param>
    /// <returns>The supplied route builder for further endpoint composition.</returns>
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapPost("/api/auth/logout", async (SignInManager<ApplicationUser> signInManager) =>
            {
                await signInManager.SignOutAsync();
                return Results.NoContent();
            })
            .RequireAuthorization()
            .WithMetadata(new RequireAntiforgeryTokenAttribute(required: true))
            .ValidateAntiforgery();

        return endpoints;
    }
}
