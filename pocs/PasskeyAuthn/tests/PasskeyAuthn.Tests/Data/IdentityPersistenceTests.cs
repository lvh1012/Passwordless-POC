using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PasskeyAuthn.Data;
using PasskeyAuthn.Tests.Infrastructure;
using Xunit;

namespace PasskeyAuthn.Tests.Data;

/// <summary>
/// Verifies that Identity and Data Protection state use a real PostgreSQL database.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class IdentityPersistenceTests : IAsyncLifetime
{
    private readonly PasskeyWebApplicationFactory _factory;
    private readonly PostgresFixture _postgres;

    /// <summary>
    /// Initializes the application factory with the PostgreSQL fixture.
    /// </summary>
    public IdentityPersistenceTests(PostgresFixture postgres)
    {
        _postgres = postgres;
        _factory = new PasskeyWebApplicationFactory(postgres);
    }

    /// <summary>
    /// Starts the host so its startup initializer can apply migrations.
    /// </summary>
    public Task InitializeAsync()
    {
        _ = _factory.CreateClient();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Disposes the test host after each test class.
    /// </summary>
    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    /// <summary>
    /// Persists a user with a unique email to PostgreSQL.
    /// </summary>
    public async Task Create_user_with_unique_email_persists_to_PostgreSQL()
    {
        const string email = "person@example.test";

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.Users.Add(new ApplicationUser
        {
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
        });

        await context.SaveChangesAsync();

        Assert.Equal(1, await context.Users.CountAsync(user => user.Email == email));
    }

    [Fact]
    /// <summary>
    /// Loads a persisted user by its normalized email.
    /// </summary>
    public async Task User_can_be_loaded_by_normalized_email()
    {
        const string email = "lookup@example.test";
        const string normalizedEmail = "LOOKUP@EXAMPLE.TEST";

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.Users.Add(new ApplicationUser
        {
            UserName = email,
            NormalizedUserName = normalizedEmail,
            Email = email,
            NormalizedEmail = normalizedEmail,
        });
        await context.SaveChangesAsync();

        var user = await context.Users.SingleOrDefaultAsync(candidate => candidate.NormalizedEmail == normalizedEmail);

        Assert.NotNull(user);
        Assert.Equal(email, user.Email);
    }

    [Fact]
    /// <summary>
    /// Confirms migration creates the Data Protection key table independently of its row count.
    /// </summary>
    public async Task Migration_creates_DataProtectionKeys_table()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var tableExists = await TableExistsAsync(context, "DataProtectionKeys");

        Assert.True(tableExists);
    }

    [Fact]
    /// <summary>
    /// Confirms migration creates the built-in Passkey storage table.
    /// </summary>
    public async Task Migration_creates_AspNetUserPasskeys_table()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var tableExists = await TableExistsAsync(context, "AspNetUserPasskeys");

        Assert.True(tableExists);
    }

    [Fact]
    /// <summary>
    /// Confirms the startup initializer applies migrations to a schema that begins with every migration pending.
    /// </summary>
    public async Task Startup_initializer_applies_pending_migrations_to_fresh_schema()
    {
        var connectionString = await _postgres.CreateIsolatedConnectionStringAsync();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Default"] = connectionString,
                })
            .Build());
        services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connectionString));

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var pendingBeforeInitialization = await context.Database.GetPendingMigrationsAsync();
        Assert.NotEmpty(pendingBeforeInitialization);

        await DatabaseInitializer.InitializeAsync(scope.ServiceProvider, CancellationToken.None);

        var pendingAfterInitialization = await context.Database.GetPendingMigrationsAsync();
        Assert.Empty(pendingAfterInitialization);
    }

    private static async Task<bool> TableExistsAsync(ApplicationDbContext context, string tableName)
    {
        await context.Database.OpenConnectionAsync();
        try
        {
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = """
                SELECT EXISTS (
                    SELECT 1
                    FROM information_schema.tables
                    WHERE table_schema = current_schema()
                      AND table_name = @tableName)
                """;
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@tableName";
            parameter.Value = tableName;
            command.Parameters.Add(parameter);

            return (bool)(await command.ExecuteScalarAsync())!;
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }
}
