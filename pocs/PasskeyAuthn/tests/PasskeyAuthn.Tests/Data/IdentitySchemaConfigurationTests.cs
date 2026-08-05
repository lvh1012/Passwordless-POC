using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PasskeyAuthn.Data;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace PasskeyAuthn.Tests.Data;

/// <summary>
/// Verifies that the production Identity configuration enables the Passkey schema.
/// </summary>
public sealed class IdentitySchemaConfigurationTests
{
    [Fact]
    /// <summary>
    /// Verifies that the application model includes the built-in Passkey entity.
    /// </summary>
    public void Production_identity_configuration_includes_builtin_passkey_entity()
    {
        EF.IsDesignTime = true;
        try
        {
            using var factory = new DesignTimeWebApplicationFactory();
            using var scope = factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            Assert.NotNull(context.Model.FindEntityType(typeof(IdentityUserPasskey<string>)));
        }
        finally
        {
            EF.IsDesignTime = false;
        }
    }

    /// <summary>
    /// Hosts the production composition root without opening a database connection during model inspection.
    /// </summary>
    private sealed class DesignTimeWebApplicationFactory : WebApplicationFactory<Program>
    {
        /// <inheritdoc />
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:Default"] = "Host=127.0.0.1;Database=model_only",
                    });
            });
        }
    }
}
