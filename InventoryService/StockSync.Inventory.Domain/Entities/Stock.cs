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

    // Todo cambio de saldo deja un movimiento para que el historial explique siempre el saldo actual.
    // Devuelve null si la cantidad no cambia, porque un movimiento siempre tiene cantidad positiva.
    public MovimientoStock? ActualizarCantidad(int nuevaCantidad)
    {
        AsegurarCantidadValida(nuevaCantidad);
        if (nuevaCantidad == Cantidad)
            return null;

        var cantidadAnterior = Cantidad;
        Cantidad = nuevaCantidad;
        return new MovimientoStock(this, TipoMovimientoStock.Ajuste, Math.Abs(nuevaCantidad - cantidadAnterior), cantidadAnterior);
    }

    public MovimientoStock RegistrarMovimiento(TipoMovimientoStock tipo, int cantidad)
    {
        if (cantidad <= 0)
            throw new DomainException("La cantidad del movimiento debe ser mayor que cero.");

        if (tipo is not (TipoMovimientoStock.Entrada or TipoMovimientoStock.Salida))
            throw new DomainException("El tipo de movimiento no es válido.");

        if (tipo == TipoMovimientoStock.Salida && cantidad > Cantidad)
            throw new StockInsuficienteException(Cantidad, cantidad);

        if (tipo == TipoMovimientoStock.Entrada && cantidad > int.MaxValue - Cantidad)
            throw new DomainException("La entrada supera la cantidad máxima permitida.");

        var cantidadAnterior = Cantidad;
        Cantidad = tipo == TipoMovimientoStock.Entrada ? Cantidad + cantidad : Cantidad - cantidad;

        return new MovimientoStock(this, tipo, cantidad, cantidadAnterior);
    }

    private static void AsegurarCantidadValida(int cantidad)
    {
        if (cantidad < 0)
            throw new DomainException("La cantidad no puede ser negativa.");
    }
}
