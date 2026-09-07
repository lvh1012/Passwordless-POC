using MagicLinkAuthn.Configuration;
using MagicLinkAuthn.Data;
using MagicLinkAuthn.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;

namespace MagicLinkAuthn.Email;

public sealed class MagicLinkDeliveryService
{
    private const int MaximumAttempts = 8;
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(1);
    private readonly ApplicationDbContext _dbContext;
    private readonly IMagicLinkEmailSender _emailSender;
    private readonly MagicLinkOutboxProtector _protector;
    private readonly MagicLinkSettings _settings;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<MagicLinkDeliveryService> _logger;

    public MagicLinkDeliveryService(
        ApplicationDbContext dbContext,
        IMagicLinkEmailSender emailSender,
        MagicLinkOutboxProtector protector,
        IOptions<MagicLinkSettings> settings,
        TimeProvider timeProvider,
        ILogger<MagicLinkDeliveryService> logger)
    {
        _dbContext = dbContext;
        _emailSender = emailSender;
        _protector = protector;
        _settings = settings.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<MagicLinkDeliveryResult> DeliverAsync(Guid requestId, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var leaseId = Guid.NewGuid();
        var claimed = await _dbContext.MagicLinkOutboxMessages
            .Where(message =>
                message.MagicLinkRequestId == requestId &&
                message.NextAttemptAt <= now &&
                (message.LeaseExpiresAt == null || message.LeaseExpiresAt <= now))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(message => message.LeaseId, leaseId)
                    .SetProperty(message => message.LeaseExpiresAt, now.Add(LeaseDuration))
                    .SetProperty(message => message.Attempts, message => message.Attempts + 1),
                cancellationToken);
        if (claimed != 1)
        {
            return MagicLinkDeliveryResult.NotDue;
        }

        _dbContext.ChangeTracker.Clear();
        var message = await _dbContext.MagicLinkOutboxMessages
            .AsNoTracking()
            .SingleAsync(candidate => candidate.MagicLinkRequestId == requestId, cancellationToken);
        var request = await _dbContext.MagicLinkRequests
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == requestId, cancellationToken);

        if (request.ExpiresAt <= now || request.RevokedAt is not null)
        {
            await AbandonExpiredAsync(requestId, leaseId, now, cancellationToken);
            return MagicLinkDeliveryResult.Expired;
        }

        string token;
        try
        {
            token = _protector.Unprotect(requestId, message.ProtectedToken);
        }
        catch (CryptographicException exception)
        {
            _logger.LogError(exception, "Unable to decrypt Magic Link delivery {RequestId}.", requestId);
            await AbandonExpiredAsync(requestId, leaseId, now, cancellationToken);
            return MagicLinkDeliveryResult.Failed;
        }

        var callback = new Uri(
            new Uri(_settings.PublicBaseUrl, UriKind.Absolute),
            $"/magic-link/callback#token={token}");

        string providerMessageId;
        try
        {
            providerMessageId = await _emailSender.SendAsync(
                request.Email,
                callback,
                requestId,
                cancellationToken);
        }
        catch (EmailDeliveryException exception)
        {
            _logger.LogWarning(exception, "Magic Link delivery {RequestId} will be retried.", requestId);
            await RescheduleAsync(requestId, leaseId, message.Attempts, now, cancellationToken);
            return MagicLinkDeliveryResult.RetryScheduled;
        }

        await MarkDeliveredAsync(request, leaseId, providerMessageId, now, cancellationToken);
        return MagicLinkDeliveryResult.Delivered;
    }

    private async Task MarkDeliveredAsync(
        MagicLinkRequest request,
        Guid leaseId,
        string providerMessageId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({request.NormalizedEmail}, 0))",
            cancellationToken);

        var finalized = await _dbContext.MagicLinkRequests
            .Where(candidate => candidate.Id == request.Id && candidate.SentAt == null && candidate.RevokedAt == null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(candidate => candidate.SentAt, now)
                    .SetProperty(candidate => candidate.ProviderMessageId, providerMessageId),
                cancellationToken);

        if (finalized == 1)
        {
            await _dbContext.MagicLinkRequests
                .Where(candidate =>
                    candidate.NormalizedEmail == request.NormalizedEmail &&
                    candidate.Id != request.Id &&
                    candidate.CreatedAt < request.CreatedAt &&
                    candidate.ConsumedAt == null &&
                    candidate.RevokedAt == null)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(candidate => candidate.RevokedAt, now),
                    cancellationToken);
        }

        await _dbContext.MagicLinkOutboxMessages
            .Where(message => message.MagicLinkRequestId == request.Id && message.LeaseId == leaseId)
            .ExecuteDeleteAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task RescheduleAsync(
        Guid requestId,
        Guid leaseId,
        int attempts,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (attempts >= MaximumAttempts)
        {
            await AbandonExpiredAsync(requestId, leaseId, now, cancellationToken);
            return;
        }

        var delaySeconds = Math.Min(300, 1 << Math.Min(attempts, 8));
        await _dbContext.MagicLinkOutboxMessages
            .Where(message => message.MagicLinkRequestId == requestId && message.LeaseId == leaseId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(message => message.NextAttemptAt, now.AddSeconds(delaySeconds))
                    .SetProperty(message => message.LeaseId, (Guid?)null)
                    .SetProperty(message => message.LeaseExpiresAt, (DateTimeOffset?)null),
                cancellationToken);
    }

    private async Task AbandonExpiredAsync(
        Guid requestId,
        Guid leaseId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _dbContext.MagicLinkRequests
            .Where(request => request.Id == requestId && request.SentAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(request => request.RevokedAt, now),
                cancellationToken);
        await _dbContext.MagicLinkOutboxMessages
            .Where(message => message.MagicLinkRequestId == requestId && message.LeaseId == leaseId)
            .ExecuteDeleteAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}

public enum MagicLinkDeliveryResult
{
    Delivered,
    RetryScheduled,
    NotDue,
    Expired,
    Failed
}
