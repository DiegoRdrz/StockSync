using StockSync.Inventory.Domain.Entities;
using StockSync.Inventory.Domain.Exceptions;

namespace StockSync.Inventory.UnitTests.Domain;

public class StockTests
{
    private static Stock CrearValido(Guid? productoId = null, Guid? sucursalId = null, int cantidad = 20) =>
        Stock.Crear(productoId ?? Guid.NewGuid(), sucursalId ?? Guid.NewGuid(), cantidad);

    [Fact]
    public void Crear_ConDatosValidos_InicializaStock()
    {
        var productoId = Guid.NewGuid();
        var sucursalId = Guid.NewGuid();

        var stock = CrearValido(productoId, sucursalId, 20);

        Assert.NotEqual(Guid.Empty, stock.Id);
        Assert.Equal(productoId, stock.ProductoId);
        Assert.Equal(sucursalId, stock.SucursalId);
        Assert.Equal(20, stock.Cantidad);
    }

    [Fact]
    public void Crear_ConCantidadEnCero_EsValido()
    {
        var stock = CrearValido(cantidad: 0);

        Assert.Equal(0, stock.Cantidad);
    }

    [Fact]
    public void Crear_ConCantidadNegativa_LanzaDomainException()
    {
        Assert.Throws<DomainException>(() => CrearValido(cantidad: -1));
    }

    [Fact]
    public void Crear_ConProductoVacio_LanzaDomainException()
    {
        Assert.Throws<DomainException>(() => CrearValido(productoId: Guid.Empty));
    }

    [Fact]
    public void Crear_ConSucursalVacia_LanzaDomainException()
    {
        Assert.Throws<DomainException>(() => CrearValido(sucursalId: Guid.Empty));
    }

    [Fact]
    public void ActualizarCantidad_ConCantidadValida_ModificaSoloLaCantidad()
    {
        var stock = CrearValido(cantidad: 20);
        var id = stock.Id;
        var productoId = stock.ProductoId;
        var sucursalId = stock.SucursalId;

        stock.ActualizarCantidad(5);

        Assert.Equal(5, stock.Cantidad);
        Assert.Equal(id, stock.Id);
        Assert.Equal(productoId, stock.ProductoId);
        Assert.Equal(sucursalId, stock.SucursalId);
    }

    [Fact]
    public void ActualizarCantidad_ConCantidadEnCero_EsValido()
    {
        var stock = CrearValido(cantidad: 20);

        stock.ActualizarCantidad(0);

        Assert.Equal(0, stock.Cantidad);
    }

    [Theory]
    [InlineData(20, 35, 15)]
    [InlineData(20, 5, 15)]
    [InlineData(0, 12, 12)]
    public void ActualizarCantidad_RegistraAjusteConSaldosAnteriorYPosterior(int inicial, int nueva, int diferencia)
    {
        var stock = CrearValido(cantidad: inicial);

        var ajuste = stock.ActualizarCantidad(nueva);

        Assert.NotNull(ajuste);
        Assert.Equal(TipoMovimientoStock.Ajuste, ajuste.Tipo);
        Assert.Equal(stock.Id, ajuste.StockId);
        Assert.Equal(diferencia, ajuste.Cantidad);
        Assert.Equal(inicial, ajuste.CantidadAnterior);
        Assert.Equal(nueva, ajuste.CantidadPosterior);
    }

    [Fact]
    public void ActualizarCantidad_SinCambio_NoRegistraAjuste()
    {
        var stock = CrearValido(cantidad: 20);

        Assert.Null(stock.ActualizarCantidad(20));
        Assert.Equal(20, stock.Cantidad);
    }

    [Fact]
    public void ActualizarCantidad_ConCantidadNegativa_LanzaDomainExceptionYNoModifica()
    {
        var stock = CrearValido(cantidad: 20);

        Assert.Throws<DomainException>(() => stock.ActualizarCantidad(-1));
        Assert.Equal(20, stock.Cantidad);
    }
}
