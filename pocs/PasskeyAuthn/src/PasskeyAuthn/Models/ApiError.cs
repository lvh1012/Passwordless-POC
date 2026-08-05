namespace PasskeyAuthn.Models;

/// <summary>
/// Defines a stable error code and a client-safe message for authentication API failures.
/// </summary>
/// <param name="Code">The machine-readable stable error code.</param>
/// <param name="Message">The safe message suitable for display to a user.</param>
public sealed record ApiError(string Code, string Message);
