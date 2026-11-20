using Microsoft.EntityFrameworkCore;
using Npgsql;
using StockSync.Inventory.Application.Common;
using StockSync.Inventory.Infrastructure;

namespace StockSync.Inventory.IntegrationTests;

// Each test creates and deletes its own database, never the application's database.
public abstract class PostgresTestBase : IAsyncLifetime
{
    private readonly string _database = $"stocksync_test_{Guid.NewGuid():N}";
    protected string ConnectionString { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var adminConnection = Environment.GetEnvironmentVariable("STOCKSYNC_TEST_POSTGRES")!;
        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{_database}\"", admin);
        await command.ExecuteNonQueryAsync();
        ConnectionString = new NpgsqlConnectionStringBuilder(adminConnection) { Database = _database, Pooling = false }.ConnectionString;

        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await using var admin = new NpgsqlConnection(Environment.GetEnvironmentVariable("STOCKSYNC_TEST_POSTGRES"));
        await admin.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_database}\" WITH (FORCE)", admin);
        await command.ExecuteNonQueryAsync();
    }

    protected InventoryDbContext CreateContext() => new(Options());

    protected InventoryDbContext CreateContext(Guid tenantId) => new(Options(), new TestTenant(tenantId));

    private DbContextOptions<InventoryDbContext> Options() =>
        new DbContextOptionsBuilder<InventoryDbContext>().UseNpgsql(ConnectionString).Options;
}

internal sealed record TestTenant(Guid TenantId) : ICurrentTenant;
