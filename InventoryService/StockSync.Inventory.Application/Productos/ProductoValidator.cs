using StockSync.Inventory.Domain.Entities;

namespace StockSync.Inventory.Application.Productos;

public static class ProductoValidator
{
    public static Dictionary<string, string[]> Validar(ProductoRequest request)
    {
        var errores = new Dictionary<string, List<string>>();

        if (string.IsNullOrWhiteSpace(request.Nombre))
            Agregar(errores, nameof(request.Nombre), "El nombre es obligatorio.");
        else if (request.Nombre.Trim().Length > Producto.NombreMaxLength)
            Agregar(errores, nameof(request.Nombre), $"El nombre no puede superar {Producto.NombreMaxLength} caracteres.");

        if (string.IsNullOrWhiteSpace(request.Sku))
            Agregar(errores, nameof(request.Sku), "El SKU es obligatorio.");
        else if (request.Sku.Trim().Length > Producto.SkuMaxLength)
            Agregar(errores, nameof(request.Sku), $"El SKU no puede superar {Producto.SkuMaxLength} caracteres.");

        if (request.Descripcion?.Trim().Length > Producto.DescripcionMaxLength)
            Agregar(errores, nameof(request.Descripcion), $"La descripción no puede superar {Producto.DescripcionMaxLength} caracteres.");

        if (request.PrecioCompra < 0)
            Agregar(errores, nameof(request.PrecioCompra), "El precio de compra no puede ser negativo.");
        else if (!Producto.EsPrecioRepresentable(request.PrecioCompra))
            Agregar(errores, nameof(request.PrecioCompra), $"El precio de compra admite como máximo {Producto.PrecioDecimales} decimales y no puede superar {Producto.PrecioMaximo}.");

        if (request.PrecioVenta < 0)
            Agregar(errores, nameof(request.PrecioVenta), "El precio de venta no puede ser negativo.");
        else if (!Producto.EsPrecioRepresentable(request.PrecioVenta))
            Agregar(errores, nameof(request.PrecioVenta), $"El precio de venta admite como máximo {Producto.PrecioDecimales} decimales y no puede superar {Producto.PrecioMaximo}.");

        if (request.StockMinimo < 0)
            Agregar(errores, nameof(request.StockMinimo), "El stock mínimo no puede ser negativo.");

        if (request.CategoriaId == Guid.Empty)
            Agregar(errores, nameof(request.CategoriaId), "La categoría indicada no es válida.");

        return Convertir(errores);
    }

    public static Dictionary<string, string[]> Validar(ProductoFiltro filtro)
    {
        var errores = new Dictionary<string, List<string>>();

        if (filtro.Pagina < 1)
            Agregar(errores, nameof(filtro.Pagina), "La página debe ser mayor o igual a 1.");

        if (filtro.TamanoPagina < 1 || filtro.TamanoPagina > ProductoFiltro.TamanoPaginaMaximo)
            Agregar(errores, nameof(filtro.TamanoPagina), $"El tamaño de página debe estar entre 1 y {ProductoFiltro.TamanoPaginaMaximo}.");

        // El desplazamiento (Pagina - 1) * TamanoPagina se calcula en int y no debe desbordarse.
        if (filtro.Pagina > 0 && filtro.TamanoPagina > 0 &&
            (long)(filtro.Pagina - 1) * filtro.TamanoPagina > int.MaxValue)
            Agregar(errores, nameof(filtro.Pagina), "La página solicitada excede el límite permitido.");

        return Convertir(errores);
    }

    private static void Agregar(Dictionary<string, List<string>> errores, string campo, string mensaje)
    {
        if (!errores.TryGetValue(campo, out var mensajes))
            errores[campo] = mensajes = [];
        mensajes.Add(mensaje);
    }

    private static Dictionary<string, string[]> Convertir(Dictionary<string, List<string>> errores) =>
        errores.ToDictionary(e => e.Key, e => e.Value.ToArray());
}
