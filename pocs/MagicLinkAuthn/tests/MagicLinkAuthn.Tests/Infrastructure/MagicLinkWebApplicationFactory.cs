using MagicLinkAuthn.Email;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MagicLinkAuthn.Tests.Infrastructure;

public sealed class MagicLinkWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly PostgresFixture _postgres;

    public MagicLinkWebApplicationFactory(PostgresFixture postgres) => _postgres = postgres;

    public TestMagicLinkEmailSender EmailSender { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = _postgres.ConnectionString,
                ["MagicLink:PublicBaseUrl"] = "https://example.test",
                ["MagicLink:EmailCooldownSeconds"] = "10"
            }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IMagicLinkEmailSender>();
            services.AddSingleton<IMagicLinkEmailSender>(EmailSender);
            services.ConfigureApplicationCookie(options =>
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest);
        });
    }
}
