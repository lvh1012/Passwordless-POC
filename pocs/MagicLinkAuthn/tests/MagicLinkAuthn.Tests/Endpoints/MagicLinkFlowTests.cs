using MagicLinkAuthn.Data;
using MagicLinkAuthn.Security;
using MagicLinkAuthn.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MagicLinkAuthn.Tests.Endpoints;

[Collection(PostgresCollection.Name)]
public sealed class MagicLinkFlowTests
{
    private readonly PostgresFixture _postgres;

    public MagicLinkFlowTests(PostgresFixture postgres) => _postgres = postgres;

    [Fact]
    public async Task IssueAndRedeem_CreatesAccountOnlyAfterRedemption_AndConsumesOnce()
    {
        await using var factory = new MagicLinkWebApplicationFactory(_postgres);

        await using (var issueScope = factory.Services.CreateAsyncScope())
        {
            var service = issueScope.ServiceProvider.GetRequiredService<MagicLinkService>();
            Assert.Equal(
                MagicLinkIssueResult.Sent,
                await service.IssueAsync("new-user@example.test", "/dashboard", CancellationToken.None));

            var db = issueScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Empty(await db.Users.ToListAsync());
        }

        var fragment = factory.EmailSender.Latest!.MagicLink.Fragment.TrimStart('#');
        var token = fragment.Split('=', 2) is ["token", var encodedToken] ? encodedToken : null;
        Assert.NotNull(token);

        await using var firstScope = factory.Services.CreateAsyncScope();
        await using var secondScope = factory.Services.CreateAsyncScope();
        var firstRedemption = firstScope.ServiceProvider
            .GetRequiredService<MagicLinkService>()
            .RedeemAsync(token, CancellationToken.None);
        var secondRedemption = secondScope.ServiceProvider
            .GetRequiredService<MagicLinkService>()
            .RedeemAsync(token, CancellationToken.None);

        var results = await Task.WhenAll(firstRedemption, secondRedemption);
        var successful = Assert.Single(results, result => result is not null)!;
        Assert.Equal("new-user@example.test", successful.User.Email);
        Assert.True(successful.User.EmailConfirmed);

        await using (var replayScope = factory.Services.CreateAsyncScope())
        {
            var service = replayScope.ServiceProvider.GetRequiredService<MagicLinkService>();
            Assert.Null(await service.RedeemAsync(token, CancellationToken.None));
        }
    }
}
