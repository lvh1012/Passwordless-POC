using MagicLinkAuthn.Data;
using Microsoft.EntityFrameworkCore;

namespace MagicLinkAuthn.Email;

public sealed class MagicLinkOutboxWorker : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<MagicLinkOutboxWorker> _logger;

    public MagicLinkOutboxWorker(
        IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider,
        ILogger<MagicLinkOutboxWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval, _timeProvider);
        do
        {
            try
            {
                await DeliverDueMessagesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Magic Link outbox polling failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task DeliverDueMessagesAsync(CancellationToken cancellationToken)
    {
        Guid[] requestIds;
        await using (var scope = _scopeFactory.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var now = _timeProvider.GetUtcNow();
            requestIds = await dbContext.MagicLinkOutboxMessages
                .AsNoTracking()
                .Where(message =>
                    message.NextAttemptAt <= now &&
                    (message.LeaseExpiresAt == null || message.LeaseExpiresAt <= now))
                .OrderBy(message => message.NextAttemptAt)
                .Select(message => message.MagicLinkRequestId)
                .Take(20)
                .ToArrayAsync(cancellationToken);
        }

        foreach (var requestId in requestIds)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var delivery = scope.ServiceProvider.GetRequiredService<MagicLinkDeliveryService>();
            await delivery.DeliverAsync(requestId, cancellationToken);
        }
    }
}
