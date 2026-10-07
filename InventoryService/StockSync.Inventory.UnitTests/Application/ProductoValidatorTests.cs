using StockSync.Inventory.Application.Productos;
using StockSync.Inventory.Domain.Entities;

namespace StockSync.Inventory.UnitTests.Application;

public class ProductoValidatorTests
{
    private static readonly ProductoRequest RequestValido =
        new("Martillo", "FER-001", "Martillo de carpintero", 10m, 15m, 5, Guid.NewGuid());

    [Fact]
    public void Validar_RequestValido_NoDevuelveErrores()
    {
        Assert.Empty(ProductoValidator.Validar(RequestValido));
    }

    [Fact]
    public void Validar_SinCategoria_EsValido()
    {
        Assert.Empty(ProductoValidator.Validar(RequestValido with { CategoriaId = null }));
    }

    [Fact]
    public void Validar_RequestInvalido_DevuelveTodosLosErroresPorCampo()
    {
        var request = new ProductoRequest(
            "",
            " ",
            new string('a', Producto.DescripcionMaxLength + 1),
            -1m,
            -1m,
            -1,
            Guid.Empty);

        var errores = ProductoValidator.Validar(request);

        Assert.Equal(
            new[]
            {
                nameof(ProductoRequest.Nombre),
                nameof(ProductoRequest.Sku),
                nameof(ProductoRequest.Descripcion),
                nameof(ProductoRequest.PrecioCompra),
                nameof(ProductoRequest.PrecioVenta),
                nameof(ProductoRequest.StockMinimo),
                nameof(ProductoRequest.CategoriaId)
            }.Order(),
            errores.Keys.Order());
    }

    [Fact]
    public void Validar_TextosDemasiadoLargos_DevuelveErrores()
    {
        var request = RequestValido with
        {
            Nombre = new string('a', Producto.NombreMaxLength + 1),
            Sku = new string('a', Producto.SkuMaxLength + 1)
        };

        var errores = ProductoValidator.Validar(request);

        Assert.Contains(nameof(ProductoRequest.Nombre), errores.Keys);
        Assert.Contains(nameof(ProductoRequest.Sku), errores.Keys);
    }

    [Theory]
    [InlineData("10.999")]
    [InlineData("10000000000000000")]
    public void Validar_PrecioNoRepresentableEnBaseDeDatos_DevuelveError(string precio)
    {
        var valor = decimal.Parse(precio, System.Globalization.CultureInfo.InvariantCulture);

        var errores = ProductoValidator.Validar(RequestValido with { PrecioCompra = valor, PrecioVenta = valor });

        Assert.Contains(nameof(ProductoRequest.PrecioCompra), errores.Keys);
        Assert.Contains(nameof(ProductoRequest.PrecioVenta), errores.Keys);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(5, ProductoFiltro.TamanoPaginaMaximo)]
    public void Validar_FiltroValido_NoDevuelveErrores(int pagina, int tamanoPagina)
    {
        var filtro = new ProductoFiltro { Pagina = pagina, TamanoPagina = tamanoPagina };

        Assert.Empty(ProductoValidator.Validar(filtro));
    }

    [Theory]
    [InlineData(0, 20, nameof(ProductoFiltro.Pagina))]
    [InlineData(1, 0, nameof(ProductoFiltro.TamanoPagina))]
    [InlineData(1, ProductoFiltro.TamanoPaginaMaximo + 1, nameof(ProductoFiltro.TamanoPagina))]
    [InlineData(int.MaxValue, 20, nameof(ProductoFiltro.Pagina))]
    public void Validar_FiltroInvalido_DevuelveError(int pagina, int tamanoPagina, string campo)
    {
        var filtro = new ProductoFiltro { Pagina = pagina, TamanoPagina = tamanoPagina };

        Assert.Contains(campo, ProductoValidator.Validar(filtro).Keys);
    }
}
