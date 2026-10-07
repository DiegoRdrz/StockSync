using Microsoft.EntityFrameworkCore;
using Npgsql;
using StockSync.Inventory.Infrastructure;

namespace StockSync.Inventory.IntegrationTests;

// Cada prueba crea y elimina su propia base de datos; nunca modifica la base de la aplicación.
public abstract class PostgresTestBase : IAsyncLifetime
{
    private readonly string _database = $"stocksync_test_{Guid.NewGuid():N}";
    private string _connectionString = null!;

    public async Task InitializeAsync()
    {
        var adminConnection = Environment.GetEnvironmentVariable("STOCKSYNC_TEST_POSTGRES")!;
        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{_database}\"", admin);
        await command.ExecuteNonQueryAsync();
        _connectionString = new NpgsqlConnectionStringBuilder(adminConnection) { Database = _database, Pooling = false }.ConnectionString;

        await using var context = CrearContexto();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await using var admin = new NpgsqlConnection(Environment.GetEnvironmentVariable("STOCKSYNC_TEST_POSTGRES"));
        await admin.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_database}\" WITH (FORCE)", admin);
        await command.ExecuteNonQueryAsync();
    }

    protected InventoryDbContext CrearContexto() => new(
        new DbContextOptionsBuilder<InventoryDbContext>().UseNpgsql(_connectionString).Options);
}
