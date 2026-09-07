using MagicLinkAuthn.Email;

namespace MagicLinkAuthn.Tests.Infrastructure;

public sealed class TestMagicLinkEmailSender : IMagicLinkEmailSender
{
    private readonly object _gate = new();

    public TestEmail? Latest { get; private set; }

    public bool FailDelivery { get; set; }

    public Func<Guid, Task>? BeforeSendAsync { get; set; }

    public async Task<string> SendAsync(string recipient, Uri magicLink, Guid requestId, CancellationToken cancellationToken)
    {
        if (BeforeSendAsync is not null)
        {
            await BeforeSendAsync(requestId);
        }

        lock (_gate)
        {
            Latest = new TestEmail(recipient, magicLink, requestId);
        }

        if (FailDelivery)
        {
            throw new EmailDeliveryException("Simulated provider failure.");
        }

        return $"test-{requestId:N}";
    }
}

public sealed record TestEmail(string Recipient, Uri MagicLink, Guid RequestId);
