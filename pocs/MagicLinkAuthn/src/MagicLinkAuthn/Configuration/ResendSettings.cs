namespace MagicLinkAuthn.Configuration;

public sealed class ResendSettings
{
    public const string SectionName = "Resend";

    public string ApiKey { get; set; } = string.Empty;

    public string From { get; set; } = string.Empty;
}
