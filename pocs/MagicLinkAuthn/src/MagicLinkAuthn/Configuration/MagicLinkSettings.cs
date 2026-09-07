namespace MagicLinkAuthn.Configuration;

public sealed class MagicLinkSettings
{
    public const string SectionName = "MagicLink";

    public string PublicBaseUrl { get; set; } = "http://localhost:8080";

    public int LifetimeMinutes { get; set; } = 10;

    public int EmailCooldownSeconds { get; set; } = 60;

    public string OutboxEncryptionKey { get; set; } = string.Empty;
}
