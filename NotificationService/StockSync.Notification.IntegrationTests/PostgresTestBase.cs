using Microsoft.EntityFrameworkCore;
using Npgsql;
using StockSync.Notification.Application.Common;
using StockSync.Notification.Infrastructure;

namespace StockSync.Notification.IntegrationTests;

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("STOCKSYNC_TEST_POSTGRES")))
            Skip = "Set STOCKSYNC_TEST_POSTGRES to run against PostgreSQL.";
    }
}

public abstract class PostgresTestBase : IAsyncLifetime
{
    private readonly string _database = $"stocksync_test_{Guid.NewGuid():N}";
    protected string ConnectionString { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var adminConnection = Environment.GetEnvironmentVariable("STOCKSYNC_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(adminConnection))
            return;

        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{_database}\"", admin);
        await command.ExecuteNonQueryAsync();
        ConnectionString = new NpgsqlConnectionStringBuilder(adminConnection)
        {
            Database = _database,
            Pooling = false
        }.ConnectionString;

        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        var adminConnection = Environment.GetEnvironmentVariable("STOCKSYNC_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(adminConnection))
            return;

        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_database}\" WITH (FORCE)", admin);
        await command.ExecuteNonQueryAsync();
    }

    protected NotificationDbContext CreateContext() => new(Options());

    protected NotificationDbContext CreateContext(TenantContext tenant) => new(Options(), tenant);

    protected NotificationDbContext CreateContext(Guid tenantId)
    {
        var tenant = new TenantContext();
        tenant.Set(tenantId);
        return new NotificationDbContext(Options(), tenant);
    }

    private DbContextOptions<NotificationDbContext> Options() =>
        new DbContextOptionsBuilder<NotificationDbContext>().UseNpgsql(ConnectionString).Options;
}
