using Microsoft.EntityFrameworkCore;
using Npgsql;
using StockSync.Inventory.Application.Common.Exceptions;
using StockSync.Inventory.Application.Movimientos;
using StockSync.Inventory.Domain.Entities;
using StockSync.Inventory.Infrastructure;
using StockSync.Inventory.Infrastructure.Repositories;

namespace StockSync.Inventory.IntegrationTests;

public sealed class PostgreSqlFactAttribute : FactAttribute
{
    public PostgreSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("STOCKSYNC_TEST_POSTGRES")))
            Skip = "Defina STOCKSYNC_TEST_POSTGRES para ejecutar contra PostgreSQL (ver README).";
    }
}

// Cada prueba crea y elimina su propia base de datos; nunca modifica la base de la aplicación.
public class MovimientosPostgresTests : IAsyncLifetime
{
    private readonly string _database = $"stocksync_test_{Guid.NewGuid():N}";
    private string _connectionString = null!;
    private Guid _stockId;

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
        var producto = Producto.Crear("Prueba", "TEST", null, 1, 2, 0, null);
        var stock = Stock.Crear(producto.Id, Guid.NewGuid(), 10);
        context.Productos.Add(producto);
        context.Stocks.Add(stock);
        await context.SaveChangesAsync();
        _stockId = stock.Id;
    }

    public async Task DisposeAsync()
    {
        await using var admin = new NpgsqlConnection(Environment.GetEnvironmentVariable("STOCKSYNC_TEST_POSTGRES"));
        await admin.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_database}\" WITH (FORCE)", admin);
        await command.ExecuteNonQueryAsync();
    }

    private InventoryDbContext CrearContexto() => new(
        new DbContextOptionsBuilder<InventoryDbContext>().UseNpgsql(_connectionString).Options);

    [PostgreSqlFact]
    public async Task ListadoGeneral_PaginaDeDiezEnDiezEIncluyeProductosInactivos()
    {
        await using var context = CrearContexto();
        await InventoryDbSeeder.SeedAsync(context);
        var stock = await context.Stocks.SingleAsync(s => s.Id == _stockId);
        for (var i = 0; i < 12; i++)
            context.MovimientosStock.Add(stock.RegistrarMovimiento(TipoMovimientoStock.Entrada, 1));
        var martillo = await context.Productos.SingleAsync(p => p.Sku == "DEMO-HER-001");
        martillo.Desactivar();
        await context.SaveChangesAsync();
        var service = new MovimientoStockService(new StockRepository(context), new ProductoRepository(context),
            new MovimientoStockRepository(context), new UnidadDeTrabajo(context));

        var pagina1 = await service.ListarGeneralAsync(1, default);
        var pagina2 = await service.ListarGeneralAsync(2, default);
        Assert.Equal(10, pagina1.Items.Count);
        Assert.Equal(7, pagina2.Items.Count);
        Assert.Equal(10, pagina1.TamanoPagina);
        Assert.Equal(17, pagina1.Total);
        Assert.Equal(17, pagina2.Total);
        Assert.Equal(2, pagina2.TotalPaginas);
        var esperados = await context.MovimientosStock.OrderByDescending(m => m.Fecha)
            .ThenByDescending(m => m.Id).Select(m => m.Id).ToListAsync();
        var todos = pagina1.Items.Concat(pagina2.Items).ToList();
        Assert.Equal(esperados, todos.Select(m => m.Id));
        var delMartillo = todos.Where(m => m.ProductoId == martillo.Id).ToList();
        Assert.Equal(2, delMartillo.Count);
        Assert.All(delMartillo, m =>
        {
            Assert.Equal("Martillo demo", m.ProductoNombre);
            Assert.Equal("DEMO-HER-001", m.ProductoSku);
        });
        Assert.Empty((await service.ListarGeneralAsync(3, default)).Items);
        await Assert.ThrowsAsync<ValidationException>(() => service.ListarGeneralAsync(0, default));
        await Assert.ThrowsAsync<ValidationException>(() => service.ListarGeneralAsync(int.MaxValue, default));
    }

    [PostgreSqlFact]
    public async Task Semilla_Repetida_ConservaDatosSaldosEHistorial()
    {
        Guid martilloId;
        Guid stockId;
        await using (var context = CrearContexto())
        {
            await InventoryDbSeeder.SeedAsync(context);
            Assert.Equal(2, await context.Categorias.CountAsync());
            Assert.Equal(4, await context.Productos.CountAsync()); // Incluye el producto de la fixture.
            Assert.Equal(4, await context.Stocks.CountAsync());
            Assert.Equal(5, await context.MovimientosStock.CountAsync());
            var martillo = await context.Productos.SingleAsync(p => p.Sku == "DEMO-HER-001");
            martilloId = martillo.Id;
            var stock = await context.Stocks.SingleAsync(s => s.ProductoId == martilloId);
            stockId = stock.Id;
            Assert.Equal(7, stock.Cantidad);
            var taladroId = (await context.Productos.SingleAsync(p => p.Sku == "DEMO-HER-002")).Id;
            Assert.Equal(0, (await context.Stocks.SingleAsync(s => s.ProductoId == taladroId)).Cantidad);
            context.MovimientosStock.Add(stock.RegistrarMovimiento(TipoMovimientoStock.Salida, 2));
            martillo.Desactivar();
            await context.SaveChangesAsync();
        }

        await using var repeticion = CrearContexto();
        await InventoryDbSeeder.SeedAsync(repeticion);
        Assert.Equal(2, await repeticion.Categorias.CountAsync());
        Assert.Equal(4, await repeticion.Productos.CountAsync());
        Assert.Equal(4, await repeticion.Stocks.CountAsync());
        Assert.Equal(6, await repeticion.MovimientosStock.CountAsync());
        Assert.Equal(5, (await repeticion.Stocks.SingleAsync(s => s.Id == stockId)).Cantidad);
        Assert.False((await repeticion.Productos.SingleAsync(p => p.Id == martilloId)).Activo);
    }

    [PostgreSqlFact]
    public async Task DosVentasConElMismoSaldo_SoloUnaSeConfirmaYSuHistorialEsAtomico()
    {
        await using var primero = CrearContexto();
        await using var segundo = CrearContexto();
        var repo1 = new StockRepository(primero);
        var repo2 = new StockRepository(segundo);
        // Fuerza que ambos clientes lean 10 antes de que cualquiera confirme su venta de 7.
        var stock1 = (await repo1.ObtenerParaActualizarAsync(_stockId, default))!;
        var stock2 = (await repo2.ObtenerParaActualizarAsync(_stockId, default))!;
        await new MovimientoStockRepository(primero).AgregarAsync(stock1.RegistrarMovimiento(TipoMovimientoStock.Salida, 7), default);
        await new MovimientoStockRepository(segundo).AgregarAsync(stock2.RegistrarMovimiento(TipoMovimientoStock.Salida, 7), default);

        var resultados = await Task.WhenAll(IntentarGuardar(repo1), IntentarGuardar(repo2));

        Assert.Single(resultados.Where(ok => ok));
        await using var verificacion = CrearContexto();
        Assert.Equal(3, (await verificacion.Stocks.SingleAsync()).Cantidad);
        var movimiento = await verificacion.MovimientosStock.SingleAsync();
        Assert.Equal(10, movimiento.CantidadAnterior);
        Assert.Equal(3, movimiento.CantidadPosterior);
    }

    [PostgreSqlFact]
    public async Task AjusteManualConSaldoDesactualizado_NoSobrescribeUnaVenta()
    {
        await using var ajuste = CrearContexto();
        await using var venta = CrearContexto();
        var repoAjuste = new StockRepository(ajuste);
        var stockAjuste = (await repoAjuste.ObtenerParaActualizarAsync(_stockId, default))!;
        var repoVenta = new StockRepository(venta);
        var stockVenta = (await repoVenta.ObtenerParaActualizarAsync(_stockId, default))!;
        venta.MovimientosStock.Add(stockVenta.RegistrarMovimiento(TipoMovimientoStock.Salida, 4));
        await repoVenta.GuardarCambiosAsync(default);

        stockAjuste.ActualizarCantidad(20);
        await Assert.ThrowsAsync<ConflictException>(() => repoAjuste.GuardarCambiosAsync(default));
        await using var verificacion = CrearContexto();
        Assert.Equal(6, (await verificacion.Stocks.SingleAsync()).Cantidad);
        Assert.Equal(1, await verificacion.MovimientosStock.CountAsync());
    }

    [PostgreSqlFact]
    public async Task EliminarStockConMovimientos_RechazaYConservaHistorial()
    {
        await using (var entrada = CrearContexto())
        {
            var stock = await entrada.Stocks.SingleAsync();
            entrada.MovimientosStock.Add(stock.RegistrarMovimiento(TipoMovimientoStock.Entrada, 2));
            await entrada.SaveChangesAsync();
        }

        await using (var borrado = CrearContexto())
        {
            var repo = new StockRepository(borrado);
            repo.Eliminar((await repo.ObtenerParaActualizarAsync(_stockId, default))!);
            await Assert.ThrowsAsync<ConflictException>(() => repo.GuardarCambiosAsync(default));
        }

        await using var verificacion = CrearContexto();
        Assert.Equal(12, (await verificacion.Stocks.SingleAsync()).Cantidad);
        Assert.Equal(1, await verificacion.MovimientosStock.CountAsync());
    }

    [PostgreSqlFact]
    public async Task RestriccionBaseDeDatos_ImpideStockNegativo()
    {
        await using var context = CrearContexto();
        var exception = await Assert.ThrowsAsync<PostgresException>(() =>
            context.Database.ExecuteSqlRawAsync("UPDATE \"Stocks\" SET \"Cantidad\" = -1"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        Assert.Equal(10, (await context.Stocks.SingleAsync()).Cantidad);
    }

    private static async Task<bool> IntentarGuardar(StockRepository repo)
    {
        try
        {
            await repo.GuardarCambiosAsync(default);
            return true;
        }
        catch (ConflictException)
        {
            return false;
        }
    }
}
