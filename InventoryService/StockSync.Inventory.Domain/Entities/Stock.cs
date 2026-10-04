using StockSync.Inventory.Domain.Exceptions;

namespace StockSync.Inventory.Domain.Entities;

// Cantidad de un producto en una sucursal. SucursalId es solo el identificador de una sucursal
// que pertenece a otro servicio: Inventory no la modela ni la valida contra ningún otro contexto.
public class Stock
{
    public Guid Id { get; private set; }
    public Guid ProductoId { get; private set; }
    public Guid SucursalId { get; private set; }
    public int Cantidad { get; private set; }

    private Stock()
    {
    }

    public static Stock Crear(Guid productoId, Guid sucursalId, int cantidad)
    {
        if (productoId == Guid.Empty)
            throw new DomainException("El producto indicado no es válido.");
        if (sucursalId == Guid.Empty)
            throw new DomainException("La sucursal indicada no es válida.");
        AsegurarCantidadValida(cantidad);

        return new Stock
        {
            Id = Guid.NewGuid(),
            ProductoId = productoId,
            SucursalId = sucursalId,
            Cantidad = cantidad
        };
    }

    public void ActualizarCantidad(int nuevaCantidad)
    {
        AsegurarCantidadValida(nuevaCantidad);
        Cantidad = nuevaCantidad;
    }

    private static void AsegurarCantidadValida(int cantidad)
    {
        if (cantidad < 0)
            throw new DomainException("La cantidad no puede ser negativa.");
    }
}
