using Npgsql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace PasskeyAuthn.Configuration;

/// <summary>
/// Validates deployment configuration that must fail closed before PostgreSQL migrations run.
/// </summary>
public sealed class ProductionConfigurationValidator : IValidateOptions<PasskeySettings>
{
    private readonly IConfiguration _configuration;

    /// <summary>
    /// Initializes the validator with the application's configuration provider.
    /// </summary>
    /// <param name="configuration">Configuration containing the default PostgreSQL connection string.</param>
    public ProductionConfigurationValidator(IConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <summary>
    /// Validates bound Passkey settings through the standard options pipeline.
    /// </summary>
    /// <param name="name">The options instance name.</param>
    /// <param name="settings">The bound Passkey settings.</param>
    /// <returns>A success result or a safe failure result identifying the invalid setting.</returns>
    public ValidateOptionsResult Validate(string? name, PasskeySettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        try
        {
            Validate(settings, _configuration.GetConnectionString("Default"));
            return ValidateOptionsResult.Success;
        }
        catch (InvalidOperationException exception)
        {
            // Options validation must preserve the existing safe, key-specific error without exposing secrets.
            return ValidateOptionsResult.Fail(exception.Message);
        }
    }

    /// <summary>
    /// Rejects unsafe relying-party settings and PostgreSQL connections without including configured values in errors.
    /// </summary>
    /// <param name="settings">The bound Passkey relying-party settings.</param>
    /// <param name="connectionString">The PostgreSQL connection string supplied by the deployment environment.</param>
    /// <exception cref="InvalidOperationException">Thrown when any required production invariant is invalid.</exception>
    public static void Validate(PasskeySettings settings, string? connectionString)
    {
        ArgumentNullException.ThrowIfNull(settings);

        ValidateServerDomain(settings.ServerDomain);
        ValidateExpectedOrigin(settings.ExpectedOrigin, settings.ServerDomain);

        if (settings.AuthenticatorTimeoutSeconds <= 0)
        {
            throw InvalidSetting("Passkey:AuthenticatorTimeoutSeconds", "must be greater than zero");
        }

        if (settings.MaxPasskeysPerUser is < 1 or > 3)
        {
            throw InvalidSetting("Passkey:MaxPasskeysPerUser", "must be between 1 and 3");
        }

        if (settings.MaxDisplayNameLength is < 1 or > 100)
        {
            throw InvalidSetting("Passkey:MaxDisplayNameLength", "must be between 1 and 100");
        }

        ValidateConnectionString(connectionString);
    }

    private static void ValidateServerDomain(string serverDomain)
    {
        // RP IDs are host names, so URL syntax or an explicit port would alter WebAuthn scope.
        if (string.IsNullOrWhiteSpace(serverDomain) ||
            string.Equals(serverDomain, "localhost", StringComparison.OrdinalIgnoreCase) ||
            serverDomain.Contains("://", StringComparison.Ordinal) ||
            serverDomain.Contains('/') ||
            serverDomain.Contains(':') ||
            Uri.CheckHostName(serverDomain) == UriHostNameType.Unknown)
        {
            throw InvalidSetting("Passkey:ServerDomain", "must be a host/RP ID without scheme, path, or port");
        }
    }

    private static void ValidateExpectedOrigin(string expectedOrigin, string serverDomain)
    {
        var requiredOrigin = $"https://{serverDomain}";
        // Exact origin comparison rejects paths, queries, fragments, credentials, and explicit ports together.
        if (!Uri.TryCreate(expectedOrigin, UriKind.Absolute, out _) ||
            !string.Equals(expectedOrigin, requiredOrigin, StringComparison.OrdinalIgnoreCase))
        {
            throw InvalidSetting("Passkey:ExpectedOrigin", "must be the exact HTTPS origin for Passkey:ServerDomain");
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
            // The inner parser error can contain connection-string text, so expose only the configuration key.
            throw InvalidSetting("ConnectionStrings:Default", "must be a valid PostgreSQL connection string with TLS required");
        }

        if (string.IsNullOrWhiteSpace(builder.Host) ||
            string.IsNullOrWhiteSpace(builder.Database) ||
            string.IsNullOrWhiteSpace(builder.Username) ||
            builder.SslMode is not (SslMode.Require or SslMode.VerifyCA or SslMode.VerifyFull))
        {
            throw InvalidSetting("ConnectionStrings:Default", "must include host, database, username, and SSL Mode Require/VerifyCA/VerifyFull");
        }
    }

    private static InvalidOperationException InvalidSetting(string key, string requirement) =>
        new($"{key} {requirement}.");
}
