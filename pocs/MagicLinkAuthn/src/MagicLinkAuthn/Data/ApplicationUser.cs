using Microsoft.AspNetCore.Identity;

namespace MagicLinkAuthn.Data;

public sealed class ApplicationUser : IdentityUser
{
    public string? FullName { get; set; }

    public DateTimeOffset? ProfileCompletedAt { get; set; }
}
