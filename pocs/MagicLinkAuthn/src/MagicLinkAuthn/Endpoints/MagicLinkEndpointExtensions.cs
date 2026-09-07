using MagicLinkAuthn.Email;
using MagicLinkAuthn.Data;
using MagicLinkAuthn.Models;
using MagicLinkAuthn.Security;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MagicLinkAuthn.Configuration;
using System.Text.Encodings.Web;

namespace MagicLinkAuthn.Endpoints;

public static class MagicLinkEndpointExtensions
{
    public const string RequestRateLimitPolicy = "magic-link-request";
    public const string RedeemCookieName = "MagicLinkAuthn.Redeem";
    private const string GenericRequestMessage = "If the address can receive email, a sign-in link will arrive shortly.";

    public static IEndpointRouteBuilder MapMagicLinkEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/magic-links/request", RequestAsync)
            .AddEndpointFilter<AntiforgeryEndpointFilter>()
            .RequireRateLimiting(RequestRateLimitPolicy);

        endpoints.MapPost("/api/magic-links/prepare", Prepare)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        endpoints.MapPost("/api/magic-links/redeem", RedeemAsync)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        endpoints.MapPost("/api/auth/logout", LogoutAsync)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        return endpoints;
    }

    public static IEndpointRouteBuilder MapDevelopmentOutbox(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/dev/outbox", (DevelopmentOutbox outbox) =>
        {
            var message = outbox.GetLatest();
            if (message is null)
            {
                return Results.Content("<h1>Development outbox</h1><p>No message has been sent.</p>", "text/html");
            }

            var encoder = HtmlEncoder.Default;
            var html = $"<h1>Development outbox</h1><p>Recipient: {encoder.Encode(message.Recipient)}</p>" +
                $"<p><a href=\"{encoder.Encode(message.MagicLink.AbsoluteUri)}\">Open the latest Magic Link</a></p>";
            return Results.Content(html, "text/html");
        });

        return endpoints;
    }

    private static async Task<IResult> RequestAsync(
        [FromBody] RequestMagicLinkRequest body,
        MagicLinkService service,
        CancellationToken cancellationToken)
    {
        try
        {
            _ = await service.IssueAsync(body.Email, body.ReturnUrl, cancellationToken);
            return Results.Accepted(value: new { message = GenericRequestMessage });
        }
        catch (InvalidEmailException)
        {
            return Results.BadRequest(new ApiError("invalid_email", "Enter a valid email address."));
        }
        catch (InvalidReturnUrlException)
        {
            return Results.BadRequest(new ApiError("invalid_return_url", "The return URL must be local."));
        }
        catch (EmailDeliveryException)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Email delivery is temporarily unavailable.");
        }
    }

    private static IResult Prepare(
        [FromBody] PrepareMagicLinkRequest body,
        MagicLinkTokenService tokenService,
        IOptions<MagicLinkSettings> settings,
        HttpContext context)
    {
        if (!tokenService.TryHash(body.Token, out _))
        {
            return Results.BadRequest(new ApiError("invalid_magic_link", "This sign-in link is invalid."));
        }

        context.Response.Cookies.Append(RedeemCookieName, body.Token!, new CookieOptions
        {
            HttpOnly = true,
            Secure = context.Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            IsEssential = true,
            MaxAge = TimeSpan.FromMinutes(settings.Value.LifetimeMinutes),
            Path = "/"
        });
        return Results.Ok(new { redirectUrl = "/magic-link/confirm" });
    }

    private static async Task<IResult> RedeemAsync(
        HttpContext context,
        MagicLinkService service,
        SignInManager<ApplicationUser> signInManager,
        CancellationToken cancellationToken)
    {
        context.Request.Cookies.TryGetValue(RedeemCookieName, out var encodedToken);
        context.Response.Cookies.Delete(RedeemCookieName, new CookieOptions { Path = "/" });

        var redemption = await service.RedeemAsync(encodedToken, cancellationToken);
        if (redemption is null)
        {
            return Results.Redirect("/magic-link/invalid");
        }

        await signInManager.SignInAsync(redemption.User, isPersistent: false);
        return Results.Redirect(GetPostRedemptionDestination(redemption));
    }

    private static async Task<IResult> LogoutAsync(SignInManager<ApplicationUser> signInManager)
    {
        await signInManager.SignOutAsync();
        return Results.Redirect("/");
    }

    public static string GetPostRedemptionDestination(MagicLinkRedemption redemption)
    {
        ArgumentNullException.ThrowIfNull(redemption);
        var returnUrl = UserProfileService.GetSafeReturnUrl(redemption.ReturnUrl);
        if (redemption.User.ProfileCompletedAt is not null)
        {
            return returnUrl;
        }

        return $"/onboarding?returnUrl={Uri.EscapeDataString(returnUrl)}";
    }
}
