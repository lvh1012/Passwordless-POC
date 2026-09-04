using MagicLinkAuthn.Email;

namespace MagicLinkAuthn.Tests.Infrastructure;

public sealed class TestMagicLinkEmailSender : IMagicLinkEmailSender
{
    private readonly object _gate = new();

    public TestEmail? Latest { get; private set; }

    public Task<string> SendAsync(string recipient, Uri magicLink, Guid requestId, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            Latest = new TestEmail(recipient, magicLink, requestId);
        }

        return Task.FromResult($"test-{requestId:N}");
    }
}

public sealed record TestEmail(string Recipient, Uri MagicLink, Guid RequestId);
