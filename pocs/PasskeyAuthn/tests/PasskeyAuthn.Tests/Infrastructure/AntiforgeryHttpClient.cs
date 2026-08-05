using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace PasskeyAuthn.Tests.Infrastructure;

internal static class AntiforgeryHttpClient
{
    internal static async Task<HttpClient> CreateAsync(
        PasskeyWebApplicationFactory factory,
        bool authenticated = false)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider,
        };

        if (authenticated)
        {
            var identity = new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "test-user"), new Claim(ClaimTypes.Name, "test@example.test")],
                IdentityConstants.ApplicationScheme);
            context.User = new ClaimsPrincipal(identity);
            await context.SignInAsync(IdentityConstants.ApplicationScheme, context.User);
        }

        var tokens = scope.ServiceProvider.GetRequiredService<IAntiforgery>().GetAndStoreTokens(context);
        var cookieHeader = string.Join(
            "; ",
            context.Response.Headers.SetCookie.Select(value => value!.Split(';', 2)[0]));

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
        });
        client.DefaultRequestHeaders.Add("Cookie", cookieHeader);
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", tokens.RequestToken);
        return client;
    }
}
