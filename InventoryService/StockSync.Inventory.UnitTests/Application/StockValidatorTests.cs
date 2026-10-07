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

    [Theory]
    [InlineData(0, 20, nameof(StockFiltro.Pagina))]
    [InlineData(1, 0, nameof(StockFiltro.TamanoPagina))]
    [InlineData(1, StockFiltro.TamanoPaginaMaximo + 1, nameof(StockFiltro.TamanoPagina))]
    [InlineData(int.MaxValue, 20, nameof(StockFiltro.Pagina))]
    public void Validar_FiltroInvalido_DevuelveError(int pagina, int tamanoPagina, string campo)
    {
        var filtro = new StockBajoMinimoFiltro { Pagina = pagina, TamanoPagina = tamanoPagina };

        Assert.Contains(campo, StockValidator.Validar(filtro).Keys);
    }

    [Fact]
    public void Validar_FiltroPorDefecto_NoDevuelveErrores()
    {
        Assert.Empty(StockValidator.Validar(new StockFiltro()));
    }

    [Fact]
    public void Validar_CantidadRequestNegativo_DevuelveError()
    {
        var errores = StockValidator.Validar(new StockCantidadRequest(-1));

        Assert.Contains(nameof(StockCantidadRequest.NuevaCantidad), errores.Keys);
    }
}
