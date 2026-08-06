using PasskeyAuthn.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace PasskeyAuthn.Tests.Configuration;

/// <summary>
/// Verifies production startup rejects unsafe Passkey and PostgreSQL configuration before connecting.
/// </summary>
public sealed class ProductionConfigurationValidatorTests
{
    private const string ValidConnectionString =
        "Host=aws-0-ap-southeast-1.pooler.supabase.com;Database=postgres;Username=app;Password=test-only;SSL Mode=VerifyFull";

    [Fact]
    /// <summary>
    /// Verifies production validation participates in the standard options pipeline.
    /// </summary>
    public void Options_validator_accepts_valid_production_configuration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = ValidConnectionString,
            })
            .Build();
        var validator = new ProductionConfigurationValidator(configuration);

        var result = validator.Validate(Options.DefaultName, CreateValidSettings());

        Assert.True(result.Succeeded);
    }

    [Fact]
    /// <summary>
    /// Verifies the options pipeline reports an invalid Passkey key without exposing database values.
    /// </summary>
    public void Options_validator_reports_invalid_setting_without_secret_values()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = ValidConnectionString,
            })
            .Build();
        var settings = CreateValidSettings();
        settings.ServerDomain = "https://invalid.example";
        var validator = new ProductionConfigurationValidator(configuration);

        var result = validator.Validate(Options.DefaultName, settings);

        Assert.False(result.Succeeded);
        Assert.Contains("Passkey:ServerDomain", string.Join(" ", result.Failures!), StringComparison.Ordinal);
        Assert.DoesNotContain("test-only", string.Join(" ", result.Failures!), StringComparison.Ordinal);
    }

    [Fact]
    /// <summary>
    /// Verifies a Render-style HTTPS relying party and TLS PostgreSQL connection are accepted.
    /// </summary>
    public void Valid_production_configuration_is_accepted()
    {
        var settings = CreateValidSettings();

        ProductionConfigurationValidator.Validate(settings, ValidConnectionString);
    }

    [Theory]
    [InlineData("")]
    [InlineData("https://passkey-authn.onrender.com")]
    [InlineData("passkey-authn.onrender.com/path")]
    [InlineData("passkey-authn.onrender.com:443")]
    /// <summary>
    /// Verifies the RP ID accepts a host only, never a URL, path, or port.
    /// </summary>
    public void Invalid_server_domain_is_rejected(string serverDomain)
    {
        var settings = CreateValidSettings();
        settings.ServerDomain = serverDomain;

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ProductionConfigurationValidator.Validate(settings, ValidConnectionString));

        Assert.Contains("Passkey:ServerDomain", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("localhost", "https://localhost")]
    [InlineData("LOCALHOST", "https://LOCALHOST")]
    /// <summary>
    /// Verifies Production rejects the localhost fallback even when its matching HTTPS origin is supplied.
    /// </summary>
    public void Localhost_server_domain_is_rejected_before_expected_origin_validation(
        string serverDomain,
        string expectedOrigin)
    {
        var settings = CreateValidSettings();
        settings.ServerDomain = serverDomain;
        settings.ExpectedOrigin = expectedOrigin;

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ProductionConfigurationValidator.Validate(settings, ValidConnectionString));

        Assert.Contains("Passkey:ServerDomain", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("http://passkey-authn.onrender.com")]
    [InlineData("https://other.onrender.com")]
    [InlineData("https://passkey-authn.onrender.com:8443")]
    [InlineData("https://passkey-authn.onrender.com/path")]
    [InlineData("https://passkey-authn.onrender.com?query=1")]
    /// <summary>
    /// Verifies the accepted WebAuthn origin is the exact HTTPS origin for the RP host.
    /// </summary>
    public void Invalid_expected_origin_is_rejected(string expectedOrigin)
    {
        var settings = CreateValidSettings();
        settings.ExpectedOrigin = expectedOrigin;

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ProductionConfigurationValidator.Validate(settings, ValidConnectionString));

        Assert.Contains("Passkey:ExpectedOrigin", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0, 3, 100, "Passkey:AuthenticatorTimeoutSeconds")]
    [InlineData(300, 0, 100, "Passkey:MaxPasskeysPerUser")]
    [InlineData(300, 4, 100, "Passkey:MaxPasskeysPerUser")]
    [InlineData(300, 3, 0, "Passkey:MaxDisplayNameLength")]
    [InlineData(300, 3, 101, "Passkey:MaxDisplayNameLength")]
    /// <summary>
    /// Verifies production cannot override approved positive timeout and POC safety limits.
    /// </summary>
    public void Invalid_timeout_or_limits_are_rejected(
        int timeoutSeconds,
        int maxPasskeys,
        int maxDisplayNameLength,
        string expectedSetting)
    {
        var settings = CreateValidSettings();
        settings.AuthenticatorTimeoutSeconds = timeoutSeconds;
        settings.MaxPasskeysPerUser = maxPasskeys;
        settings.MaxDisplayNameLength = maxDisplayNameLength;

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ProductionConfigurationValidator.Validate(settings, ValidConnectionString));

        Assert.Contains(expectedSetting, exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-postgresql-connection-string")]
    [InlineData("Host=localhost;Database=postgres;Username=app;Password=do-not-echo;SSL Mode=Prefer")]
    /// <summary>
    /// Verifies malformed or non-TLS PostgreSQL configuration fails without echoing secret values.
    /// </summary>
    public void Invalid_database_configuration_is_rejected_without_secret_values(string connectionString)
    {
        var settings = CreateValidSettings();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ProductionConfigurationValidator.Validate(settings, connectionString));

        Assert.Contains("ConnectionStrings:Default", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("do-not-echo", exception.Message, StringComparison.Ordinal);
    }

    private static PasskeySettings CreateValidSettings() => new()
    {
        ServerDomain = "passkey-authn.onrender.com",
        ExpectedOrigin = "https://passkey-authn.onrender.com",
        AuthenticatorTimeoutSeconds = 300,
        MaxPasskeysPerUser = 3,
        MaxDisplayNameLength = 100,
    };
}
