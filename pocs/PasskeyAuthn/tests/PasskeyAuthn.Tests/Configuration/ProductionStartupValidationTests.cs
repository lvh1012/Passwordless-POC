using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace PasskeyAuthn.Tests.Configuration;

/// <summary>
/// Verifies the production host invokes configuration validation before it can access PostgreSQL.
/// </summary>
public sealed class ProductionStartupValidationTests
{
    [Fact]
    /// <summary>
    /// Verifies checked-in localhost Passkey defaults cannot start a Production host or reach database migration.
    /// </summary>
    public void Production_startup_rejects_local_passkey_defaults_before_database_migration()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseEnvironment("Production"));

        var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

        Assert.Contains("Passkey:ServerDomain", exception.Message, StringComparison.Ordinal);
    }
}
