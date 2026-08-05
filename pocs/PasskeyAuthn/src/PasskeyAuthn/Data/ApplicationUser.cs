using Microsoft.AspNetCore.Identity;

namespace PasskeyAuthn.Data;

/// <summary>
/// Represents an Identity user and the transient expiry for a pending Passkey registration.
/// </summary>
public class ApplicationUser : IdentityUser
{
    /// <summary>
    /// Gets or sets when an incomplete Passkey registration can no longer be completed.
    /// </summary>
    public DateTimeOffset? PasskeyRegistrationExpiresAt { get; set; }
}
