using StockSync.Inventory.Application.Categorias;

namespace StockSync.Inventory.UnitTests.Application;

public class CategoriaValidatorTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(5, CategoriaFiltro.TamanoPaginaMaximo)]
    public void Validar_FiltroValido_NoDevuelveErrores(int pagina, int tamanoPagina)
    {
        var filtro = new CategoriaFiltro { Pagina = pagina, TamanoPagina = tamanoPagina };

        Assert.Empty(CategoriaValidator.Validar(filtro));
    }

    [Theory]
    [InlineData(0, 20, nameof(CategoriaFiltro.Pagina))]
    [InlineData(1, 0, nameof(CategoriaFiltro.TamanoPagina))]
    [InlineData(1, CategoriaFiltro.TamanoPaginaMaximo + 1, nameof(CategoriaFiltro.TamanoPagina))]
    [InlineData(int.MaxValue, 20, nameof(CategoriaFiltro.Pagina))]
    public void Validar_FiltroInvalido_DevuelveError(int pagina, int tamanoPagina, string campo)
    {
        var filtro = new CategoriaFiltro { Pagina = pagina, TamanoPagina = tamanoPagina };

        Assert.Contains(campo, CategoriaValidator.Validar(filtro).Keys);
    }
}
