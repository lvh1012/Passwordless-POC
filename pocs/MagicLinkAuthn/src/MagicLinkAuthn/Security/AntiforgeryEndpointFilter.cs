using Microsoft.AspNetCore.Antiforgery;

namespace MagicLinkAuthn.Security;

public sealed class AntiforgeryEndpointFilter : IEndpointFilter
{
    private readonly IAntiforgery _antiforgery;

    public AntiforgeryEndpointFilter(IAntiforgery antiforgery) => _antiforgery = antiforgery;

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            await _antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid request.");
        }

        return await next(context);
    }
}
