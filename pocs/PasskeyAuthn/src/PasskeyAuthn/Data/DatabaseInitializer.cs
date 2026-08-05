using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PasskeyAuthn.Data;

/// <summary>
/// Applies required database migrations before the application begins serving requests.
/// </summary>
public static class DatabaseInitializer
{
    /// <summary>
    /// Validates database configuration and applies pending Entity Framework Core migrations.
    /// </summary>
    /// <param name="services">The scoped application service provider.</param>
    /// <param name="cancellationToken">Cancels the migration operation during host shutdown.</param>
    /// <returns>A task that completes after the database is ready for authentication persistence.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the required database configuration is missing.</exception>
    public static async Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);

        var configuration = services.GetRequiredService<IConfiguration>();
        if (string.IsNullOrWhiteSpace(configuration.GetConnectionString("Default")))
        {
            // Authentication state cannot be durable without this database, so startup must fail closed.
            throw new InvalidOperationException("ConnectionStrings:Default must be configured.");
        }

        var context = services.GetRequiredService<ApplicationDbContext>();
        await context.Database.MigrateAsync(cancellationToken);
    }
}
