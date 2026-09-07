using MagicLinkAuthn.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace MagicLinkAuthn.Security;

public sealed class ProfileCompletionMiddleware
{
    private readonly RequestDelegate _next;

    public ProfileCompletionMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, ApplicationDbContext dbContext)
    {
        if (context.User.Identity?.IsAuthenticated != true || IsAllowedBeforeCompletion(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userState = string.IsNullOrWhiteSpace(userId)
            ? null
            : await dbContext.Users
                .AsNoTracking()
                .Where(user => user.Id == userId)
                .Select(user => new { user.ProfileCompletedAt })
                .SingleOrDefaultAsync(context.RequestAborted);

        if (userState is null)
        {
            await context.SignOutAsync(IdentityConstants.ApplicationScheme);
            context.Response.Redirect("/");
            return;
        }

        if (userState.ProfileCompletedAt is null)
        {
            var returnUrl = $"{context.Request.PathBase}{context.Request.Path}{context.Request.QueryString}";
            context.Response.Redirect($"/onboarding?returnUrl={Uri.EscapeDataString(returnUrl)}");
            return;
        }

        await _next(context);
    }

    internal static bool IsAllowedBeforeCompletion(PathString path) =>
        path.StartsWithSegments("/onboarding") ||
        path.StartsWithSegments("/api/auth/logout") ||
        path.StartsWithSegments("/api/magic-links") ||
        path.StartsWithSegments("/magic-link") ||
        path.StartsWithSegments("/dev/outbox") ||
        path.StartsWithSegments("/health") ||
        path.StartsWithSegments("/css") ||
        path.StartsWithSegments("/js") ||
        path.StartsWithSegments("/favicon.ico");
}
