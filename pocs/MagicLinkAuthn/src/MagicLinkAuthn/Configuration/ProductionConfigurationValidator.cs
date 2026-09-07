using Microsoft.Extensions.Options;
using Npgsql;
using System.Net.Mail;

namespace MagicLinkAuthn.Configuration;

public sealed class ProductionConfigurationValidator :
    IValidateOptions<MagicLinkSettings>,
    IValidateOptions<ResendSettings>
{
    private readonly IConfiguration _configuration;

    public ProductionConfigurationValidator(IConfiguration configuration) =>
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

    public ValidateOptionsResult Validate(string? name, MagicLinkSettings settings)
    {
        try
        {
            ValidateMagicLink(settings, _configuration.GetConnectionString("Default"));
            return ValidateOptionsResult.Success;
        }
        catch (InvalidOperationException exception)
        {
            return ValidateOptionsResult.Fail(exception.Message);
        }
    }

    public ValidateOptionsResult Validate(string? name, ResendSettings settings)
    {
        try
        {
            ValidateResend(settings);
            return ValidateOptionsResult.Success;
        }
        catch (InvalidOperationException exception)
        {
            return ValidateOptionsResult.Fail(exception.Message);
        }
    }

    public static void ValidateMagicLink(MagicLinkSettings settings, string? connectionString)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (!Uri.TryCreate(settings.PublicBaseUrl, UriKind.Absolute, out var baseUri) ||
            baseUri.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(baseUri.UserInfo) ||
            !string.IsNullOrEmpty(baseUri.Query) ||
            !string.IsNullOrEmpty(baseUri.Fragment) ||
            baseUri.AbsolutePath != "/" ||
            string.Equals(baseUri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            throw InvalidSetting("MagicLink:PublicBaseUrl", "must be an HTTPS origin without credentials, path, query, or fragment");
        }

        if (settings.LifetimeMinutes is < 1 or > 30)
        {
            throw InvalidSetting("MagicLink:LifetimeMinutes", "must be between 1 and 30");
        }

        if (settings.EmailCooldownSeconds is < 10 or > 600)
        {
            throw InvalidSetting("MagicLink:EmailCooldownSeconds", "must be between 10 and 600");
        }

        if (!TryDecodeEncryptionKey(settings.OutboxEncryptionKey, out _))
        {
            throw InvalidSetting("MagicLink:OutboxEncryptionKey", "must be a Base64-encoded 256-bit key");
        }

        ValidateConnectionString(connectionString);
    }

    public static void ValidateResend(ResendSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (string.IsNullOrWhiteSpace(settings.ApiKey) || !settings.ApiKey.StartsWith("re_", StringComparison.Ordinal))
        {
            throw InvalidSetting("Resend:ApiKey", "must be configured with a Resend API key");
        }

        try
        {
            _ = new MailAddress(settings.From);
        }
        catch (FormatException)
        {
            throw InvalidSetting("Resend:From", "must be a valid sender address on a verified domain");
        }
    }

    private static void ValidateConnectionString(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw InvalidSetting("ConnectionStrings:Default", "must be a valid PostgreSQL connection string with TLS required");
        }

        NpgsqlConnectionStringBuilder builder;
        try
        {
            builder = new NpgsqlConnectionStringBuilder(connectionString);
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException)
        {
            throw InvalidSetting("ConnectionStrings:Default", "must be a valid PostgreSQL connection string with TLS required");
        }

        if (string.IsNullOrWhiteSpace(builder.Host) ||
            string.IsNullOrWhiteSpace(builder.Database) ||
            string.IsNullOrWhiteSpace(builder.Username) ||
            builder.SslMode is not (SslMode.VerifyCA or SslMode.VerifyFull))
        {
            throw InvalidSetting("ConnectionStrings:Default", "must include host, database, username, and SSL Mode VerifyCA/VerifyFull");
        }
    }

    internal static bool TryDecodeEncryptionKey(string? encodedKey, out byte[] key)
    {
        key = [];
        if (string.IsNullOrWhiteSpace(encodedKey))
        {
            return false;
        }

        try
        {
            key = Convert.FromBase64String(encodedKey);
            return key.Length == 32;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static InvalidOperationException InvalidSetting(string key, string requirement) =>
        new($"{key} {requirement}.");
}
