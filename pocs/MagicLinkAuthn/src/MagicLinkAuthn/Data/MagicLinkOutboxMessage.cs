namespace MagicLinkAuthn.Data;

public sealed class MagicLinkOutboxMessage
{
    public Guid MagicLinkRequestId { get; set; }

    public required byte[] ProtectedToken { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset NextAttemptAt { get; set; }

    public int Attempts { get; set; }

    public Guid? LeaseId { get; set; }

    public DateTimeOffset? LeaseExpiresAt { get; set; }

    public MagicLinkRequest MagicLinkRequest { get; set; } = null!;
}
