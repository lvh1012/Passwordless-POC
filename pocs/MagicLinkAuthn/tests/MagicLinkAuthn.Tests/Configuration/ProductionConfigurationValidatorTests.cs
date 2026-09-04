using MagicLinkAuthn.Configuration;
using Xunit;

namespace MagicLinkAuthn.Tests.Configuration;

public sealed class ProductionConfigurationValidatorTests
{
    private const string SecureConnection =
        "Host=db.example.test;Database=magiclink;Username=app;Password=<test-password>;SSL Mode=Require";

    [Fact]
    public void ValidateMagicLink_AcceptsSecureConfiguration()
    {
        ProductionConfigurationValidator.ValidateMagicLink(
            new MagicLinkSettings
            {
                PublicBaseUrl = "https://magic-link-authn.onrender.com",
                LifetimeMinutes = 10,
                EmailCooldownSeconds = 60
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
                new MagicLinkSettings { PublicBaseUrl = value },
                SecureConnection));

        Assert.Contains("MagicLink:PublicBaseUrl", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateMagicLink_RejectsPostgresWithoutTls()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ProductionConfigurationValidator.ValidateMagicLink(
                new MagicLinkSettings { PublicBaseUrl = "https://example.test" },
                "Host=db.example.test;Database=magiclink;Username=app;SSL Mode=Disable"));

        Assert.Contains("ConnectionStrings:Default", exception.Message, StringComparison.Ordinal);
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
}
