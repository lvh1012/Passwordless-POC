using MagicLinkAuthn.Data;
using MagicLinkAuthn.Security;
using MagicLinkAuthn.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using Xunit;

namespace MagicLinkAuthn.Tests.Security;

[Collection(PostgresCollection.Name)]
public sealed class ProfileCompletionMiddlewareTests
{
    private readonly PostgresFixture _postgres;

    public ProfileCompletionMiddlewareTests(PostgresFixture postgres) => _postgres = postgres;

    [Fact]
    public async Task InvokeAsync_RedirectsIncompleteUserToOnboarding()
    {
        await using var factory = new MagicLinkWebApplicationFactory(_postgres);
        await using var scope = factory.Services.CreateAsyncScope();
        var user = await CreateUserAsync(scope.ServiceProvider, completed: false);
        var nextCalled = false;
        var middleware = new ProfileCompletionMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = CreateHttpContext(
            scope.ServiceProvider,
            user.Id,
            "/dashboard",
            new QueryString("?tab=security"));

        await middleware.InvokeAsync(
            context,
            scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status302Found, context.Response.StatusCode);
        Assert.Equal(
            "/onboarding?returnUrl=%2Fdashboard%3Ftab%3Dsecurity",
            context.Response.Headers["Location"].ToString());
    }

    [Fact]
    public async Task InvokeAsync_AllowsCompletedUserToContinue()
    {
        await using var factory = new MagicLinkWebApplicationFactory(_postgres);
        await using var scope = factory.Services.CreateAsyncScope();
        var user = await CreateUserAsync(scope.ServiceProvider, completed: true);
        var nextCalled = false;
        var middleware = new ProfileCompletionMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = CreateHttpContext(scope.ServiceProvider, user.Id, "/dashboard", QueryString.Empty);

        await middleware.InvokeAsync(
            context,
            scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    private static DefaultHttpContext CreateHttpContext(
        IServiceProvider services,
        string userId,
        PathString path,
        QueryString queryString)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = services,
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId)],
                "TestAuthentication"))
        };
        context.Request.Path = path;
        context.Request.QueryString = queryString;
        return context;
    }

    private static async Task<ApplicationUser> CreateUserAsync(IServiceProvider services, bool completed)
    {
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var email = $"middleware-{Guid.NewGuid():N}@example.test";
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = completed ? "Completed User" : null,
            ProfileCompletedAt = completed ? DateTimeOffset.UtcNow : null
        };
        var result = await userManager.CreateAsync(user);
        Assert.True(result.Succeeded);
        return user;
    }
}
