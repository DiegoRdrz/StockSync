using Microsoft.EntityFrameworkCore;
using Npgsql;
using StockSync.Reporting.Application.Common;
using StockSync.Reporting.Infrastructure;

namespace StockSync.Reporting.IntegrationTests;

public sealed class PostgreSqlFactAttribute : FactAttribute
{
    public PostgreSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("STOCKSYNC_TEST_POSTGRES")))
            Skip = "Set STOCKSYNC_TEST_POSTGRES to run against PostgreSQL (see README).";
    }
}

// Each test creates and deletes its own database, never the application's database.
public abstract class PostgresTestBase : IAsyncLifetime
{
    private readonly string _database = $"stocksync_reporting_test_{Guid.NewGuid():N}";
    private string? _connectionString;

    protected string ConnectionString => _connectionString!;

    public async Task InitializeAsync()
    {
        var adminConnection = Environment.GetEnvironmentVariable("STOCKSYNC_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(adminConnection))
            return;

        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{_database}\"", admin);
        await command.ExecuteNonQueryAsync();
        _connectionString = new NpgsqlConnectionStringBuilder(adminConnection) { Database = _database, Pooling = false }.ConnectionString;

        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        var adminConnection = Environment.GetEnvironmentVariable("STOCKSYNC_TEST_POSTGRES");
        if (_connectionString is null || string.IsNullOrWhiteSpace(adminConnection))
            return;

        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_database}\" WITH (FORCE)", admin);
        await command.ExecuteNonQueryAsync();
    }

    protected ReportingDbContext CreateContext() => new(Options(), new TenantContext());

    protected ReportingDbContext CreateContext(Guid tenantId) => new(Options(), new FixedTenant(tenantId));

    protected DbContextOptions<ReportingDbContext> Options() =>
        new DbContextOptionsBuilder<ReportingDbContext>().UseNpgsql(ConnectionString).Options;

    protected sealed record FixedTenant(Guid TenantId) : ICurrentTenant;
}
