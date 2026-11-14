using Microsoft.EntityFrameworkCore;
using Npgsql;
using StockSync.Identity.Infrastructure;
using Xunit;

namespace StockSync.Identity.IntegrationTests;

public sealed class PostgreSqlFactAttribute : FactAttribute
{
    public PostgreSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("STOCKSYNC_TEST_POSTGRES")))
            Skip = "Define STOCKSYNC_TEST_POSTGRES to run real PostgreSQL integration tests.";
    }
}

public abstract class PostgresTestBase : IAsyncLifetime
{
    private readonly string _database = $"stocksync_identity_test_{Guid.NewGuid():N}";
    private string _connectionString = null!;

    public async Task InitializeAsync()
    {
        var adminConnection = Environment.GetEnvironmentVariable("STOCKSYNC_TEST_POSTGRES")!;
        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();

        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{_database}\"", admin);
        await command.ExecuteNonQueryAsync();

        _connectionString = new NpgsqlConnectionStringBuilder(adminConnection)
        {
            Database = _database,
            Pooling = false
        }.ConnectionString;

        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        var adminEnv = Environment.GetEnvironmentVariable("STOCKSYNC_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(adminEnv)) return;

        await using var admin = new NpgsqlConnection(adminEnv);
        await admin.OpenAsync();

        await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_database}\" WITH (FORCE)", admin);
        await command.ExecuteNonQueryAsync();
    }

    protected IdentityDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(_connectionString)
            .Options;

        return new IdentityDbContext(options);
    }
}

