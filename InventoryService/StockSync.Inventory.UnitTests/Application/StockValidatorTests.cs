using StockSync.Inventory.Application.Stocks;

namespace StockSync.Inventory.UnitTests.Application;

public class StockValidatorTests
{
    private static readonly StockRequest RequestValido = new(Guid.NewGuid(), Guid.NewGuid(), 20);

    [Fact]
    public void Validar_RequestValido_NoDevuelveErrores()
    {
        Assert.Empty(StockValidator.Validar(RequestValido));
    }

    [Fact]
    public void Validar_CantidadEnCero_EsValido()
    {
        Assert.Empty(StockValidator.Validar(RequestValido with { Cantidad = 0 }));
    }

    [Fact]
    public void Validar_RequestInvalido_DevuelveTodosLosErroresPorCampo()
    {
        var request = new StockRequest(Guid.Empty, Guid.Empty, -1);

        var errores = StockValidator.Validar(request);

        Assert.Equal(
            new[]
            {
                nameof(StockRequest.ProductoId),
                nameof(StockRequest.SucursalId),
                nameof(StockRequest.Cantidad)
            }.Order(),
            errores.Keys.Order());
    }

    [Fact]
    public void Validar_CantidadRequestValido_NoDevuelveErrores()
    {
        Assert.Empty(StockValidator.Validar(new StockCantidadRequest(0)));
        Assert.Empty(StockValidator.Validar(new StockCantidadRequest(10)));
    }

    [Fact]
    public void Validar_CantidadRequestNegativo_DevuelveError()
    {
        var errores = StockValidator.Validar(new StockCantidadRequest(-1));

        Assert.Contains(nameof(StockCantidadRequest.NuevaCantidad), errores.Keys);
    }
}
