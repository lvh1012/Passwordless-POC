using Microsoft.EntityFrameworkCore;

namespace MagicLinkAuthn.Data;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);

        var configuration = services.GetRequiredService<IConfiguration>();
        if (string.IsNullOrWhiteSpace(configuration.GetConnectionString("Default")))
        {
            throw new InvalidOperationException("ConnectionStrings:Default must be configured.");
        }

        var context = services.GetRequiredService<ApplicationDbContext>();
        await context.Database.MigrateAsync(cancellationToken);
    }
}
