using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Features;

namespace PasskeyAuthn.Security;

/// <summary>
/// Adds explicit antiforgery validation to JSON Minimal API endpoints.
/// </summary>
internal static class AntiforgeryEndpointExtensions
{
    /// <summary>
    /// Validates the request before executing an endpoint that does not bind form data.
    /// </summary>
    /// <param name="builder">The endpoint convention builder to protect.</param>
    /// <returns>The same builder for further endpoint configuration.</returns>
    internal static TBuilder ValidateAntiforgery<TBuilder>(
        this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddEndpointFilter(async (context, next) =>
        {
            try
            {
                // UseAntiforgery records invalid JSON-request verdicts but does not short-circuit them.
                context.HttpContext.Features.Set<IAntiforgeryValidationFeature?>(null);
                var antiforgery = context.HttpContext.RequestServices.GetRequiredService<IAntiforgery>();
                await antiforgery.ValidateRequestAsync(context.HttpContext);
            }
            catch (AntiforgeryValidationException)
            {
                return Results.BadRequest();
            }

            return await next(context);
        });

        return builder;
    }
}
