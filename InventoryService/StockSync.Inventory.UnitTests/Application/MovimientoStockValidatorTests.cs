using StockSync.Inventory.Application.Movimientos;

namespace StockSync.Inventory.UnitTests.Application;

public class MovimientoStockValidatorTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validar_CantidadNoPositiva_DevuelveError(int cantidad) =>
        Assert.Contains("Cantidad", MovimientoStockValidator.Validar(new MovimientoStockRequest(cantidad)).Keys);

    [Fact]
    public void Validar_DatosValidos_NoDevuelveErrores()
    {
        Assert.Empty(MovimientoStockValidator.Validar(new MovimientoStockRequest(1)));
        Assert.Empty(MovimientoStockValidator.Validar(new MovimientoStockFiltro()));
    }

    [Theory]
    [InlineData(0, 20, "Pagina")]
    [InlineData(1, 0, "TamanoPagina")]
    [InlineData(1, 101, "TamanoPagina")]
    [InlineData(int.MaxValue, 100, "Pagina")]
    public void Validar_PaginacionInvalida_DevuelveError(int pagina, int tamano, string campo) =>
        Assert.Contains(campo, MovimientoStockValidator.Validar(new MovimientoStockFiltro(pagina, tamano)).Keys);

}
