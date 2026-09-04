namespace MagicLinkAuthn.Email;

public sealed class DevelopmentMagicLinkEmailSender : IMagicLinkEmailSender
{
    private readonly DevelopmentOutbox _outbox;

    public DevelopmentMagicLinkEmailSender(DevelopmentOutbox outbox) => _outbox = outbox;

    public Task<string> SendAsync(string recipient, Uri magicLink, Guid requestId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _outbox.Store(recipient, magicLink, requestId);
        return Task.FromResult($"development-{requestId:N}");
    }
}

public sealed class DevelopmentOutbox
{
    private readonly object _gate = new();
    private DevelopmentOutboxMessage? _latest;

    public void Store(string recipient, Uri magicLink, Guid requestId)
    {
        lock (_gate)
        {
            _latest = new DevelopmentOutboxMessage(recipient, magicLink, requestId, DateTimeOffset.UtcNow);
        }
    }

    public DevelopmentOutboxMessage? GetLatest()
    {
        lock (_gate)
        {
            return _latest;
        }
    }
}

public sealed record DevelopmentOutboxMessage(string Recipient, Uri MagicLink, Guid RequestId, DateTimeOffset CreatedAt);
