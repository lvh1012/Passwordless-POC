using MagicLinkAuthn.Data;
using MagicLinkAuthn.Security;
using MagicLinkAuthn.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MagicLinkAuthn.Tests.Security;

[Collection(PostgresCollection.Name)]
public sealed class UserProfileServiceTests
{
    private readonly PostgresFixture _postgres;

    public UserProfileServiceTests(PostgresFixture postgres) => _postgres = postgres;

    [Fact]
    public async Task CompleteAsync_RequiresFullNameButAllowsMissingPhoneNumber()
    {
        await using var factory = new MagicLinkWebApplicationFactory(_postgres);
        var userId = await CreateUserAsync(factory, "optional-phone@example.test");

        await using var scope = factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<UserProfileService>();
        var result = await service.CompleteAsync(
            userId,
            "  Ada   Lovelace  ",
            null,
            CancellationToken.None);

        Assert.Equal(UserProfileCompletionStatus.Completed, result.Status);
        var user = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .Users.AsNoTracking().SingleAsync(candidate => candidate.Id == userId);
        Assert.Equal("Ada Lovelace", user.FullName);
        Assert.Null(user.PhoneNumber);
        Assert.False(user.PhoneNumberConfirmed);
        Assert.NotNull(user.ProfileCompletedAt);
    }

    [Fact]
    public async Task CompleteAsync_AcceptsOptionalE164PhoneWithoutConfirmingIt()
    {
        await using var factory = new MagicLinkWebApplicationFactory(_postgres);
        var userId = await CreateUserAsync(factory, "phone-user@example.test");

        await using var scope = factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<UserProfileService>();
        var result = await service.CompleteAsync(
            userId,
            "Grace Hopper",
            "+84901234567",
            CancellationToken.None);

        Assert.Equal(UserProfileCompletionStatus.Completed, result.Status);
        var user = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .Users.AsNoTracking().SingleAsync(candidate => candidate.Id == userId);
        Assert.Equal("+84901234567", user.PhoneNumber);
        Assert.False(user.PhoneNumberConfirmed);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(" ", null)]
    [InlineData("A", null)]
    [InlineData("Valid Name", "0901234567")]
    [InlineData("Valid Name", "+012345678")]
    public async Task CompleteAsync_RejectsInvalidProfileWithoutCompletingIt(
        string? fullName,
        string? phoneNumber)
    {
        await using var factory = new MagicLinkWebApplicationFactory(_postgres);
        var userId = await CreateUserAsync(factory, $"invalid-{Guid.NewGuid():N}@example.test");

        await using var scope = factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<UserProfileService>();
        var result = await service.CompleteAsync(userId, fullName, phoneNumber, CancellationToken.None);

        Assert.Equal(UserProfileCompletionStatus.Invalid, result.Status);
        Assert.False(await service.IsCompleteAsync(userId, CancellationToken.None));
    }

    [Fact]
    public async Task CompleteAsync_IsAtomicAcrossConcurrentSubmissions()
    {
        await using var factory = new MagicLinkWebApplicationFactory(_postgres);
        var userId = await CreateUserAsync(factory, "concurrent-profile@example.test");

        await using var firstScope = factory.Services.CreateAsyncScope();
        await using var secondScope = factory.Services.CreateAsyncScope();
        var first = firstScope.ServiceProvider.GetRequiredService<UserProfileService>()
            .CompleteAsync(userId, "First Submission", null, CancellationToken.None);
        var second = secondScope.ServiceProvider.GetRequiredService<UserProfileService>()
            .CompleteAsync(userId, "Second Submission", "+84901234567", CancellationToken.None);

        var results = await Task.WhenAll(first, second);
        Assert.Single(results, result => result.Status == UserProfileCompletionStatus.Completed);
        Assert.Single(results, result => result.Status == UserProfileCompletionStatus.AlreadyCompleted);
    }

    [Theory]
    [InlineData(null, "/dashboard")]
    [InlineData("https://attacker.example", "/dashboard")]
    [InlineData("/onboarding", "/dashboard")]
    [InlineData("/dashboard?tab=profile", "/dashboard?tab=profile")]
    public void GetSafeReturnUrl_RejectsExternalAndRecursiveDestinations(string? supplied, string expected)
    {
        Assert.Equal(expected, UserProfileService.GetSafeReturnUrl(supplied));
    }

    private static async Task<string> CreateUserAsync(
        MagicLinkWebApplicationFactory factory,
        string email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };
        var result = await userManager.CreateAsync(user);
        Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(error => error.Code)));
        return user.Id;
    }
}
