namespace MagicLinkAuthn.Models;

public sealed record RequestMagicLinkRequest(string? Email, string? ReturnUrl);

public sealed record PrepareMagicLinkRequest(string? Token);

public sealed record ApiError(string Code, string Message);
