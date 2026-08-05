using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace PasskeyAuthn.Security;

/// <summary>
/// Protects the user identifier and expiry used to continue a Passkey registration ceremony.
/// </summary>
public sealed class RegistrationStateProtector
{
    private const string Purpose = "PasskeyAuthn.RegistrationState.v1";
    private readonly ITimeLimitedDataProtector _protector;

    /// <summary>
    /// Creates a protector isolated from every other application Data Protection payload.
    /// </summary>
    /// <param name="provider">The application Data Protection provider.</param>
    public RegistrationStateProtector(IDataProtectionProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        _protector = provider.CreateProtector(Purpose).ToTimeLimitedDataProtector();
    }

    /// <summary>
    /// Protects a pending user's identifier until the supplied absolute expiry.
    /// </summary>
    /// <param name="userId">The server-side Identity user identifier.</param>
    /// <param name="expiresAt">The absolute time after which the token is rejected.</param>
    /// <returns>An opaque, authenticated registration-state token.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="userId"/> is empty.</exception>
    public string Protect(string userId, DateTimeOffset expiresAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        return _protector.Protect(userId, expiresAt);
    }

    /// <summary>
    /// Restores valid registration state without exposing protection failures to callers.
    /// </summary>
    /// <param name="token">The opaque registration-state token.</param>
    /// <returns>The state when authentic and unexpired; otherwise, <see langword="null"/>.</returns>
    public RegistrationState? Unprotect(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        try
        {
            var userId = _protector.Unprotect(token, out var expiresAt);
            return string.IsNullOrWhiteSpace(userId) ? null : new RegistrationState(userId, expiresAt);
        }
        catch (CryptographicException)
        {
            // Tampering, malformed payloads, and expiry are all intentionally indistinguishable to callers.
            return null;
        }
        catch (FormatException)
        {
            return null;
        }
    }
}

/// <summary>
/// Identifies the pending server-side registration and its protected expiration time.
/// </summary>
/// <param name="UserId">The Identity user identifier.</param>
/// <param name="ExpiresAt">The absolute registration expiry.</param>
public sealed record RegistrationState(string UserId, DateTimeOffset ExpiresAt);
