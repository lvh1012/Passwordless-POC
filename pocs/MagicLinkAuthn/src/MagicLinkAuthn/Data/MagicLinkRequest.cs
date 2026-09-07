namespace MagicLinkAuthn.Data;

public sealed class MagicLinkRequest
{
    public Guid Id { get; set; }

    public required string Email { get; set; }

    public required string NormalizedEmail { get; set; }

    public required byte[] TokenHash { get; set; }

    public string? ReturnUrl { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? SentAt { get; set; }

    public DateTimeOffset? ConsumedAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public string? ProviderMessageId { get; set; }
}
