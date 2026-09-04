namespace MagicLinkAuthn.Email;

public interface IMagicLinkEmailSender
{
    Task<string> SendAsync(
        string recipient,
        Uri magicLink,
        Guid requestId,
        CancellationToken cancellationToken);
}
