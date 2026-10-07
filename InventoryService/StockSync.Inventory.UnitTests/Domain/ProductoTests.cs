using StockSync.Inventory.Domain.Entities;
using StockSync.Inventory.Domain.Exceptions;

namespace StockSync.Inventory.UnitTests.Domain;

public class ProductoTests
{
    private static Producto CrearValido(
        string nombre = "Martillo",
        string sku = "FER-001",
        string? descripcion = "Martillo de carpintero",
        decimal precioCompra = 10m,
        decimal precioVenta = 15m,
        int stockMinimo = 5,
        Guid? categoriaId = null) =>
        Producto.Crear(nombre, sku, descripcion, precioCompra, precioVenta, stockMinimo, categoriaId);

    [Fact]
    public void Crear_ConDatosValidos_InicializaProductoActivo()
    {
        var categoriaId = Guid.NewGuid();

        var producto = CrearValido(categoriaId: categoriaId);

        Assert.NotEqual(Guid.Empty, producto.Id);
        Assert.True(producto.Activo);
        Assert.Equal("Martillo", producto.Nombre);
        Assert.Equal(categoriaId, producto.CategoriaId);
        Assert.Null(producto.FechaActualizacion);
        Assert.Equal(DateTimeKind.Utc, producto.FechaCreacion.Kind);
    }

    [Fact]
    public void Crear_NormalizaSkuYRecortaTextos()
    {
        var producto = CrearValido(nombre: "  Martillo  ", sku: " fer-001 ", descripcion: "   ");

        Assert.Equal("Martillo", producto.Nombre);
        Assert.Equal("FER-001", producto.Sku);
        Assert.Null(producto.Descripcion);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Crear_SinNombre_LanzaDomainException(string nombre)
    {
        Assert.Throws<DomainException>(() => CrearValido(nombre: nombre));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Crear_SinSku_LanzaDomainException(string sku)
    {
        Assert.Throws<DomainException>(() => CrearValido(sku: sku));
    }

    [Fact]
    public void Crear_ConTextosDemasiadoLargos_LanzaDomainException()
    {
        Assert.Throws<DomainException>(() => CrearValido(nombre: new string('a', Producto.NombreMaxLength + 1)));
        Assert.Throws<DomainException>(() => CrearValido(sku: new string('a', Producto.SkuMaxLength + 1)));
        Assert.Throws<DomainException>(() => CrearValido(descripcion: new string('a', Producto.DescripcionMaxLength + 1)));
    }

    [Fact]
    public void Crear_ConValoresNegativos_LanzaDomainException()
    {
        Assert.Throws<DomainException>(() => CrearValido(precioCompra: -0.01m));
        Assert.Throws<DomainException>(() => CrearValido(precioVenta: -1m));
        Assert.Throws<DomainException>(() => CrearValido(stockMinimo: -1));
    }

    [Theory]
    [InlineData("10.999")]
    [InlineData("10000000000000000")]
    public void Crear_ConPrecioNoRepresentableEnBaseDeDatos_LanzaDomainException(string precio)
    {
        var valor = decimal.Parse(precio, System.Globalization.CultureInfo.InvariantCulture);

        Assert.Throws<DomainException>(() => CrearValido(precioCompra: valor));
        Assert.Throws<DomainException>(() => CrearValido(precioVenta: valor));
    }

    [Fact]
    public void Crear_ConPrecioMaximoYDosDecimales_EsValido()
    {
        var producto = CrearValido(precioCompra: 10.50m, precioVenta: Producto.PrecioMaximo);

        Assert.Equal(Producto.PrecioMaximo, producto.PrecioVenta);
    }

    [Fact]
    public void Crear_ConPreciosYStockEnCero_EsValido()
    {
        var producto = CrearValido(precioCompra: 0m, precioVenta: 0m, stockMinimo: 0);

        Assert.Equal(0m, producto.PrecioVenta);
    }

    [Fact]
    public void Crear_ConCategoriaVacia_LanzaDomainException()
    {
        Assert.Throws<DomainException>(() => CrearValido(categoriaId: Guid.Empty));
    }

    [Fact]
    public void Actualizar_ModificaDatosYFechaActualizacion()
    {
        var producto = CrearValido();

        producto.Actualizar("Martillo grande", "fer-002", null, 20m, 30m, 2, null);

        Assert.Equal("Martillo grande", producto.Nombre);
        Assert.Equal("FER-002", producto.Sku);
        Assert.Equal(30m, producto.PrecioVenta);
        Assert.NotNull(producto.FechaActualizacion);
    }

    [Fact]
    public void Actualizar_ConDatosInvalidos_NoModificaElProducto()
    {
        var producto = CrearValido();

        Assert.Throws<DomainException>(() => producto.Actualizar("Otro", "FER-001", null, -5m, 15m, 5, null));
        Assert.Equal("Martillo", producto.Nombre);
        Assert.Equal(10m, producto.PrecioCompra);
    }

    // Con varias iteraciones, un UtcNow que caiga justo en un microsegundo exacto no oculta el fallo.
    [Fact]
    public void Fechas_SeTruncanAMicrosegundosComoEnPostgreSql()
    {
        for (var i = 0; i < 20; i++)
        {
            var producto = CrearValido();
            producto.Actualizar("Martillo grande", "FER-001", null, 1m, 2m, 0, null);

            Assert.Equal(0, producto.FechaCreacion.Ticks % TimeSpan.TicksPerMicrosecond);
            Assert.Equal(0, producto.FechaActualizacion!.Value.Ticks % TimeSpan.TicksPerMicrosecond);
            Assert.Equal(DateTimeKind.Utc, producto.FechaActualizacion.Value.Kind);
        }
    }

    [Fact]
    public void Desactivar_MarcaInactivo()
    {
        var producto = CrearValido();

        producto.Desactivar();

        Assert.False(producto.Activo);
        Assert.NotNull(producto.FechaActualizacion);
    }

    [Fact]
    public void Desactivar_ProductoYaInactivo_LanzaDomainException()
    {
        var producto = CrearValido();
        producto.Desactivar();

        Assert.Throws<DomainException>(producto.Desactivar);
    }

    [Fact]
    public void Actualizar_ProductoInactivo_LanzaDomainException()
    {
        var producto = CrearValido();
        producto.Desactivar();

        Assert.Throws<DomainException>(() => producto.Actualizar("Otro", "FER-001", null, 1m, 2m, 0, null));
    }
}
