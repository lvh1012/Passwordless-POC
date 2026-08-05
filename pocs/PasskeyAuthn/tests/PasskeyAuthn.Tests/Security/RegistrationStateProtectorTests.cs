using Microsoft.AspNetCore.DataProtection;
using PasskeyAuthn.Security;
using Xunit;

namespace PasskeyAuthn.Tests.Security;

/// <summary>
/// Verifies that pending registration state remains confidential, tamper-resistant, and short-lived.
/// </summary>
public sealed class RegistrationStateProtectorTests
{
    private readonly RegistrationStateProtector _protector = new(new EphemeralDataProtectionProvider());

    [Fact]
    /// <summary>
    /// Verifies that valid protected state preserves both the user identifier and expiry.
    /// </summary>
    public void Valid_state_round_trips()
    {
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(5);

        var token = _protector.Protect("user-123", expiresAt);
        var state = _protector.Unprotect(token);

        Assert.NotNull(state);
        Assert.Equal("user-123", state.UserId);
        Assert.Equal(expiresAt, state.ExpiresAt);
    }

    [Fact]
    /// <summary>
    /// Verifies that registration state cannot be used after its protected expiry.
    /// </summary>
    public void Expired_state_is_rejected()
    {
        var token = _protector.Protect("user-123", DateTimeOffset.UtcNow.AddMinutes(-1));

        var state = _protector.Unprotect(token);

        Assert.Null(state);
    }

    [Fact]
    /// <summary>
    /// Verifies that changing any protected token data invalidates the registration state.
    /// </summary>
    public void Modified_state_is_rejected()
    {
        var token = _protector.Protect("user-123", DateTimeOffset.UtcNow.AddMinutes(5));
        var finalCharacter = token[^1] == 'A' ? 'B' : 'A';
        var modifiedToken = token[..^1] + finalCharacter;

        var state = _protector.Unprotect(modifiedToken);

        Assert.Null(state);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    /// <summary>
    /// Verifies that a token can never represent a missing user identifier.
    /// </summary>
    public void Empty_user_id_is_rejected(string userId)
    {
        Assert.Throws<ArgumentException>(() =>
            _protector.Protect(userId, DateTimeOffset.UtcNow.AddMinutes(5)));
    }
}
