using System.Text.Json;

namespace PasskeyAuthn.Models;

/// <summary>
/// Carries the account email used to begin a Passkey ceremony.
/// </summary>
/// <param name="Email">The account email address.</param>
public sealed record EmailRequest(string Email);

/// <summary>
/// Carries the browser-produced WebAuthn credential without transforming its cryptographic fields.
/// </summary>
/// <param name="Credential">The serialized browser credential object.</param>
public sealed record CredentialRequest(JsonElement Credential);
