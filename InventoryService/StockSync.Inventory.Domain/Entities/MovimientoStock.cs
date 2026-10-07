namespace StockSync.Inventory.Domain.Entities;

// Se crea únicamente al aplicar un movimiento válido sobre Stock.
public class MovimientoStock
{
    public Guid Id { get; private set; }
    public Guid StockId { get; private set; }
    public TipoMovimientoStock Tipo { get; private set; }
    public int Cantidad { get; private set; }
    public int CantidadAnterior { get; private set; }
    public int CantidadPosterior { get; private set; }
    public DateTime Fecha { get; private set; }

    private MovimientoStock()
    {
    }

    internal MovimientoStock(Stock stock, TipoMovimientoStock tipo, int cantidad, int cantidadAnterior)
    {
        Id = Guid.NewGuid();
        StockId = stock.Id;
        Tipo = tipo;
        Cantidad = cantidad;
        CantidadAnterior = cantidadAnterior;
        CantidadPosterior = stock.Cantidad;
        // PostgreSQL conserva microsegundos; evita devolver fracciones que se perderían al guardar.
        var ahora = DateTime.UtcNow;
        Fecha = new DateTime(ahora.Ticks - ahora.Ticks % TimeSpan.TicksPerMicrosecond, DateTimeKind.Utc);
    }
}
