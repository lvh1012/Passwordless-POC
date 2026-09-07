using MagicLinkAuthn.Data;
using MagicLinkAuthn.Email;
using MagicLinkAuthn.Endpoints;
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
        var requestWasCommittedBeforeSend = false;
        factory.EmailSender.BeforeSendAsync = async requestId =>
        {
            await using var verificationScope = factory.Services.CreateAsyncScope();
            var verificationDb = verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            requestWasCommittedBeforeSend = await verificationDb.MagicLinkRequests
                .AsNoTracking()
                .AnyAsync(request => request.Id == requestId);
        };

        await using (var issueScope = factory.Services.CreateAsyncScope())
        {
            var service = issueScope.ServiceProvider.GetRequiredService<MagicLinkService>();
            Assert.Equal(
                MagicLinkIssueResult.Sent,
                await service.IssueAsync("new-user@example.test", "/dashboard", CancellationToken.None));

            var db = issueScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.False(await db.Users.AnyAsync(user => user.Email == "new-user@example.test"));
            Assert.Empty(await db.MagicLinkOutboxMessages.ToListAsync());
        }

        Assert.True(requestWasCommittedBeforeSend);

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
        Assert.Null(successful.User.ProfileCompletedAt);
        Assert.StartsWith("/onboarding?returnUrl=", MagicLinkEndpointExtensions.GetPostRedemptionDestination(successful));

        await using (var replayScope = factory.Services.CreateAsyncScope())
        {
            var service = replayScope.ServiceProvider.GetRequiredService<MagicLinkService>();
            Assert.Null(await service.RedeemAsync(token, CancellationToken.None));
        }
    }

    [Fact]
    public async Task ProviderFailure_LeavesEncryptedOutboxJobThatCanBeRetried()
    {
        await using var factory = new MagicLinkWebApplicationFactory(_postgres);
        factory.EmailSender.FailDelivery = true;

        Guid requestId;
        string token;
        await using (var issueScope = factory.Services.CreateAsyncScope())
        {
            var service = issueScope.ServiceProvider.GetRequiredService<MagicLinkService>();
            Assert.Equal(
                MagicLinkIssueResult.Queued,
                await service.IssueAsync("retry-user@example.test", "/dashboard", CancellationToken.None));

            requestId = factory.EmailSender.Latest!.RequestId;
            token = new Uri(factory.EmailSender.Latest.MagicLink.AbsoluteUri).Fragment
                .TrimStart('#')
                .Split('=', 2)[1];

            var db = issueScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var message = await db.MagicLinkOutboxMessages.AsNoTracking().SingleAsync(
                candidate => candidate.MagicLinkRequestId == requestId);
            Assert.DoesNotContain(token, Convert.ToBase64String(message.ProtectedToken), StringComparison.Ordinal);
            Assert.Null((await db.MagicLinkRequests.FindAsync(requestId))!.SentAt);
        }

        factory.EmailSender.FailDelivery = false;
        await using (var retryScope = factory.Services.CreateAsyncScope())
        {
            var db = retryScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.MagicLinkOutboxMessages
                .Where(message => message.MagicLinkRequestId == requestId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(message => message.NextAttemptAt, DateTimeOffset.MinValue));

            var delivery = retryScope.ServiceProvider.GetRequiredService<MagicLinkDeliveryService>();
            Assert.Equal(
                MagicLinkDeliveryResult.Delivered,
                await delivery.DeliverAsync(requestId, CancellationToken.None));
            Assert.Empty(await db.MagicLinkOutboxMessages.Where(message => message.MagicLinkRequestId == requestId).ToListAsync());
            Assert.NotNull((await db.MagicLinkRequests.FindAsync(requestId))!.SentAt);
        }

        await using var redeemScope = factory.Services.CreateAsyncScope();
        var redemption = await redeemScope.ServiceProvider
            .GetRequiredService<MagicLinkService>()
            .RedeemAsync(token, CancellationToken.None);
        Assert.NotNull(redemption);
    }

    [Fact]
    public void CompletedProfile_SkipsOnboardingRedirect()
    {
        var user = new ApplicationUser
        {
            ProfileCompletedAt = DateTimeOffset.UtcNow
        };
        var redemption = new MagicLinkRedemption(user, "/dashboard?tab=security");

        Assert.Equal(
            "/dashboard?tab=security",
            MagicLinkEndpointExtensions.GetPostRedemptionDestination(redemption));

        Assert.Equal(
            "/dashboard",
            MagicLinkEndpointExtensions.GetPostRedemptionDestination(
                new MagicLinkRedemption(user, "https://attacker.example")));
    }
}
