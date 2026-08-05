namespace PasskeyAuthn.Configuration;

/// <summary>
/// Defines the relying-party identity and limits for Passkey ceremonies.
/// </summary>
public sealed class PasskeySettings
{
    /// <summary>
    /// Gets the configuration section used by environment-variable binding.
    /// </summary>
    public const string SectionName = "Passkey";

    /// <summary>
    /// Gets or sets the WebAuthn relying-party ID without a scheme or path.
    /// </summary>
    public string ServerDomain { get; set; } = "localhost";

    /// <summary>
    /// Gets or sets the full HTTPS origin accepted from WebAuthn client data.
    /// </summary>
    public string ExpectedOrigin { get; set; } = "https://localhost:5001";

    /// <summary>
    /// Gets or sets how long an authenticator may wait for user interaction.
    /// </summary>
    public int AuthenticatorTimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Gets or sets the maximum number of Passkeys retained for one account.
    /// </summary>
    public int MaxPasskeysPerUser { get; set; } = 3;

    /// <summary>
    /// Gets or sets the maximum accepted display-name length.
    /// </summary>
    public int MaxDisplayNameLength { get; set; } = 100;
}
