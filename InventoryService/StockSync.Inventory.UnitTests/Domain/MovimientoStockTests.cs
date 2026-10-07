using StockSync.Inventory.Domain.Entities;
using StockSync.Inventory.Domain.Exceptions;

namespace StockSync.Inventory.UnitTests.Domain;

public class MovimientoStockTests
{
    [Theory]
    [InlineData(TipoMovimientoStock.Entrada, 5, 15)]
    [InlineData(TipoMovimientoStock.Salida, 4, 6)]
    [InlineData(TipoMovimientoStock.Salida, 10, 0)]
    public void RegistrarMovimiento_Valido_ActualizaSaldoYGeneraHistorial(TipoMovimientoStock tipo, int cantidad, int esperado)
    {
        var stock = Stock.Crear(Guid.NewGuid(), Guid.NewGuid(), 10);
        var antes = DateTime.UtcNow;

        var movimiento = stock.RegistrarMovimiento(tipo, cantidad);

        Assert.Equal(esperado, stock.Cantidad);
        Assert.NotEqual(Guid.Empty, movimiento.Id);
        Assert.Equal(stock.Id, movimiento.StockId);
        Assert.Equal(tipo, movimiento.Tipo);
        Assert.Equal(cantidad, movimiento.Cantidad);
        Assert.Equal(10, movimiento.CantidadAnterior);
        Assert.Equal(esperado, movimiento.CantidadPosterior);
        Assert.InRange(movimiento.Fecha, antes.AddTicks(-TimeSpan.TicksPerMicrosecond), DateTime.UtcNow);
        Assert.Equal(DateTimeKind.Utc, movimiento.Fecha.Kind);
        Assert.Equal(0, movimiento.Fecha.Ticks % TimeSpan.TicksPerMicrosecond);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(10, 11)]
    public void Salida_SinExistenciasSuficientes_RechazaSinModificar(int disponible, int solicitado)
    {
        var stock = Stock.Crear(Guid.NewGuid(), Guid.NewGuid(), disponible);

        Assert.Throws<StockInsuficienteException>(() => stock.RegistrarMovimiento(TipoMovimientoStock.Salida, solicitado));
        Assert.Equal(disponible, stock.Cantidad);
    }

    [Theory]
    [InlineData(TipoMovimientoStock.Entrada, 0)]
    [InlineData(TipoMovimientoStock.Entrada, -1)]
    [InlineData(TipoMovimientoStock.Salida, 0)]
    [InlineData(TipoMovimientoStock.Salida, -1)]
    [InlineData((TipoMovimientoStock)99, 1)]
    [InlineData(TipoMovimientoStock.Ajuste, 1)]
    public void RegistrarMovimiento_Invalido_NoModifica(TipoMovimientoStock tipo, int cantidad)
    {
        var stock = Stock.Crear(Guid.NewGuid(), Guid.NewGuid(), 10);

        Assert.Throws<DomainException>(() => stock.RegistrarMovimiento(tipo, cantidad));
        Assert.Equal(10, stock.Cantidad);
    }

    [Fact]
    public void Entrada_Desbordamiento_NoModifica()
    {
        var stock = Stock.Crear(Guid.NewGuid(), Guid.NewGuid(), int.MaxValue);

        Assert.Throws<DomainException>(() => stock.RegistrarMovimiento(TipoMovimientoStock.Entrada, 1));
        Assert.Equal(int.MaxValue, stock.Cantidad);
    }

    [Fact]
    public void MovimientosSucesivos_ConservanLosSaldosHistoricos()
    {
        var stock = Stock.Crear(Guid.NewGuid(), Guid.NewGuid(), 0);
        var entrada = stock.RegistrarMovimiento(TipoMovimientoStock.Entrada, 10);
        var salida = stock.RegistrarMovimiento(TipoMovimientoStock.Salida, 10);

        Assert.Equal(10, entrada.CantidadPosterior);
        Assert.Equal(10, salida.CantidadAnterior);
        Assert.Equal(0, stock.Cantidad);
        Assert.NotEqual(entrada.Id, salida.Id);
    }
}
