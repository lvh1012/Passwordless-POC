using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace PasskeyAuthn.Tests.Infrastructure;

/// <summary>
/// Provides one real PostgreSQL container for persistence integration tests.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine").Build();

    /// <summary>
    /// Gets the isolated PostgreSQL connection string exposed by the test container.
    /// </summary>
    public string ConnectionString => _container.GetConnectionString();

    /// <summary>
    /// Creates a PostgreSQL connection string scoped to a new schema for deterministic migration tests.
    /// </summary>
    public async Task<string> CreateIsolatedConnectionStringAsync()
    {
        var schemaName = $"test_{Guid.NewGuid():N}";
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE SCHEMA \"{schemaName}\"";
        await command.ExecuteNonQueryAsync();

        var connectionString = new NpgsqlConnectionStringBuilder(ConnectionString)
        {
            SearchPath = schemaName,
        };

        return connectionString.ConnectionString;
    }

    /// <summary>
    /// Starts PostgreSQL before tests use the application persistence layer.
    /// </summary>
    public Task InitializeAsync() => _container.StartAsync();

    /// <summary>
    /// Releases the PostgreSQL container after the test collection has completed.
    /// </summary>
    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

/// <summary>
/// Serializes tests that share one PostgreSQL container to keep their database lifecycle deterministic.
/// </summary>
[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    /// <summary>
    /// Gets the xUnit collection name for PostgreSQL-backed tests.
    /// </summary>
    public const string Name = "PostgreSQL";
}
