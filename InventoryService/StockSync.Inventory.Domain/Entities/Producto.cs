using StockSync.Inventory.Domain.Exceptions;

namespace StockSync.Inventory.Domain.Entities;

public class Producto
{
    public const int NombreMaxLength = 150;
    public const int SkuMaxLength = 50;
    public const int DescripcionMaxLength = 500;
    // Los precios se guardan como numeric(18,2): más decimales se redondearían en silencio y más dígitos desbordan.
    public const int PrecioDecimales = 2;
    public const decimal PrecioMaximo = 9_999_999_999_999_999.99m;

    public Guid Id { get; private set; }
    public string Nombre { get; private set; } = null!;
    public string Sku { get; private set; } = null!;
    public string? Descripcion { get; private set; }
    public decimal PrecioCompra { get; private set; }
    public decimal PrecioVenta { get; private set; }
    public int StockMinimo { get; private set; }
    public Guid? CategoriaId { get; private set; }
    public bool Activo { get; private set; }
    public DateTime FechaCreacion { get; private set; }
    public DateTime? FechaActualizacion { get; private set; }

    private Producto()
    {
    }

    public static Producto Crear(
        string nombre,
        string sku,
        string? descripcion,
        decimal precioCompra,
        decimal precioVenta,
        int stockMinimo,
        Guid? categoriaId)
    {
        var producto = new Producto
        {
            Id = Guid.NewGuid(),
            Activo = true,
            FechaCreacion = DateTime.UtcNow
        };
        producto.EstablecerDatos(nombre, sku, descripcion, precioCompra, precioVenta, stockMinimo, categoriaId);
        return producto;
    }

    public void Actualizar(
        string nombre,
        string sku,
        string? descripcion,
        decimal precioCompra,
        decimal precioVenta,
        int stockMinimo,
        Guid? categoriaId)
    {
        AsegurarActivo();
        EstablecerDatos(nombre, sku, descripcion, precioCompra, precioVenta, stockMinimo, categoriaId);
        FechaActualizacion = DateTime.UtcNow;
    }

    public void Desactivar()
    {
        AsegurarActivo();
        Activo = false;
        FechaActualizacion = DateTime.UtcNow;
    }

    // El SKU se compara siempre normalizado para que "abc-1" y " ABC-1 " no puedan coexistir.
    public static string NormalizarSku(string sku) => sku.Trim().ToUpperInvariant();

    public static bool EsPrecioRepresentable(decimal precio) =>
        precio <= PrecioMaximo && decimal.Round(precio, PrecioDecimales) == precio;

    private void EstablecerDatos(
        string nombre,
        string sku,
        string? descripcion,
        decimal precioCompra,
        decimal precioVenta,
        int stockMinimo,
        Guid? categoriaId)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw new DomainException("El nombre del producto es obligatorio.");
        if (nombre.Trim().Length > NombreMaxLength)
            throw new DomainException($"El nombre no puede superar {NombreMaxLength} caracteres.");
        if (string.IsNullOrWhiteSpace(sku))
            throw new DomainException("El SKU del producto es obligatorio.");
        if (sku.Trim().Length > SkuMaxLength)
            throw new DomainException($"El SKU no puede superar {SkuMaxLength} caracteres.");
        if (descripcion?.Trim().Length > DescripcionMaxLength)
            throw new DomainException($"La descripción no puede superar {DescripcionMaxLength} caracteres.");
        if (precioCompra < 0)
            throw new DomainException("El precio de compra no puede ser negativo.");
        if (!EsPrecioRepresentable(precioCompra))
            throw new DomainException($"El precio de compra admite como máximo {PrecioDecimales} decimales y no puede superar {PrecioMaximo}.");
        if (precioVenta < 0)
            throw new DomainException("El precio de venta no puede ser negativo.");
        if (!EsPrecioRepresentable(precioVenta))
            throw new DomainException($"El precio de venta admite como máximo {PrecioDecimales} decimales y no puede superar {PrecioMaximo}.");
        if (stockMinimo < 0)
            throw new DomainException("El stock mínimo no puede ser negativo.");
        if (categoriaId == Guid.Empty)
            throw new DomainException("La categoría indicada no es válida.");

        Nombre = nombre.Trim();
        Sku = NormalizarSku(sku);
        Descripcion = string.IsNullOrWhiteSpace(descripcion) ? null : descripcion.Trim();
        PrecioCompra = precioCompra;
        PrecioVenta = precioVenta;
        StockMinimo = stockMinimo;
        CategoriaId = categoriaId;
    }

    private void AsegurarActivo()
    {
        if (!Activo)
            throw new DomainException("El producto está dado de baja.");
    }
}
