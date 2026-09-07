using MagicLinkAuthn.Configuration;
using Xunit;

namespace MagicLinkAuthn.Tests.Configuration;

public sealed class ProductionConfigurationValidatorTests
{
    private const string EncryptionKey = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";
    private const string SecureConnection =
        "Host=db.example.test;Database=magiclink;Username=app;Password=<test-password>;SSL Mode=VerifyFull";

    [Fact]
    public void ValidateMagicLink_AcceptsSecureConfiguration()
    {
        ProductionConfigurationValidator.ValidateMagicLink(
            new MagicLinkSettings
            {
                PublicBaseUrl = "https://magic-link-authn.onrender.com",
                LifetimeMinutes = 10,
                EmailCooldownSeconds = 60,
                OutboxEncryptionKey = EncryptionKey
            },
            SecureConnection);
    }

    [Theory]
    [InlineData("http://magic-link-authn.onrender.com")]
    [InlineData("https://localhost")]
    [InlineData("https://magic-link-authn.onrender.com/path")]
    [InlineData("https://user@example.test")]
    public void ValidateMagicLink_RejectsUnsafePublicBaseUrl(string value)
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ProductionConfigurationValidator.ValidateMagicLink(
                ValidSettings(value),
                SecureConnection));

        Assert.Contains("MagicLink:PublicBaseUrl", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Prevents accepting modes that permit unencrypted database connections.</summary>
    [Theory]
    [InlineData("Disable")]
    [InlineData("Allow")]
    [InlineData("Prefer")]
    public void ValidateMagicLink_RejectsPostgresWithoutRequiredTls(string sslMode)
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ProductionConfigurationValidator.ValidateMagicLink(
                ValidSettings("https://example.test"),
                $"Host=db.example.test;Database=magiclink;Username=app;SSL Mode={sslMode}"));

        Assert.Contains("ConnectionStrings:Default", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Allows required TLS with optional certificate verification for POC deployments.</summary>
    [Theory]
    [InlineData("Require")]
    [InlineData("VerifyCA")]
    [InlineData("VerifyFull")]
    public void ValidateMagicLink_AcceptsRequiredTls(string sslMode)
    {
        ProductionConfigurationValidator.ValidateMagicLink(
            ValidSettings("https://example.test"),
            $"Host=db.example.test;Database=magiclink;Username=app;SSL Mode={sslMode}");
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-base64")]
    [InlineData("c2hvcnQ=")]
    public void ValidateMagicLink_RejectsInvalidOutboxEncryptionKey(string key)
    {
        var settings = ValidSettings("https://example.test");
        settings.OutboxEncryptionKey = key;

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ProductionConfigurationValidator.ValidateMagicLink(settings, SecureConnection));

        Assert.Contains("MagicLink:OutboxEncryptionKey", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateResend_RequiresApiKeyAndValidSender()
    {
        ProductionConfigurationValidator.ValidateResend(new ResendSettings
        {
            ApiKey = "re_test_value",
            From = "Magic Link POC <login@example.test>"
        });

        Assert.Throws<InvalidOperationException>(() =>
            ProductionConfigurationValidator.ValidateResend(new ResendSettings
            {
                ApiKey = "invalid",
                From = "not-an-address"
            }));
    }

    private static MagicLinkSettings ValidSettings(string publicBaseUrl) => new()
    {
        PublicBaseUrl = publicBaseUrl,
        LifetimeMinutes = 10,
        EmailCooldownSeconds = 60,
        OutboxEncryptionKey = EncryptionKey
    };
}
