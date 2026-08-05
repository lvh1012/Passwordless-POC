using Microsoft.Extensions.Configuration;
using PasskeyAuthn.Configuration;
using Xunit;

namespace PasskeyAuthn.Tests.Configuration;

/// <summary>
/// Verifies that local Passkey limits match the approved security design.
/// </summary>
public sealed class PasskeySettingsTests
{
    [Fact]
    /// <summary>
    /// Verifies code defaults preserve the approved limits when no configuration overrides are present.
    /// </summary>
    public void Defaults_use_three_passkeys_and_one_hundred_character_display_names()
    {
        var settings = new PasskeySettings();

        Assert.Equal(3, settings.MaxPasskeysPerUser);
        Assert.Equal(100, settings.MaxDisplayNameLength);
    }

    [Fact]
    /// <summary>
    /// Verifies checked-in local configuration binds the same approved limits as code defaults.
    /// </summary>
    public void Appsettings_bind_approved_passkey_limits()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json")
            .Build();
        var settings = configuration.GetSection(PasskeySettings.SectionName).Get<PasskeySettings>();

        Assert.NotNull(settings);
        Assert.Equal(3, settings.MaxPasskeysPerUser);
        Assert.Equal(100, settings.MaxDisplayNameLength);
    }
}
