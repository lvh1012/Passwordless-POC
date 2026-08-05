using Microsoft.EntityFrameworkCore;
using PasskeyAuthn.Data;

namespace PasskeyAuthn.Endpoints;

/// <summary>
/// Maps unauthenticated readiness endpoints used by the hosting platform.
/// </summary>
public static class HealthEndpointExtensions
{
    /// <summary>
    /// Maps the database-backed readiness endpoint.
    /// </summary>
    /// <param name="endpoints">The application's endpoint route builder.</param>
    /// <returns>The supplied route builder for further endpoint composition.</returns>
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet("/health", CheckDatabaseAsync).AllowAnonymous();
        return endpoints;
    }

    private static async Task<IResult> CheckDatabaseAsync(
        ApplicationDbContext database,
        HttpContext context)
    {
        try
        {
            return await database.Database.CanConnectAsync(context.RequestAborted)
                ? Results.Ok()
                : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
        catch (Exception)
        {
            // Readiness must fail closed without returning or logging database details that could include secrets.
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }
}
