namespace StockSync.Inventory.Domain.Repositories;

// Los repositorios comparten el contexto de la solicitud; esta unidad delimita la transacción
// para que los bloqueos de fila tomados dentro de la operación duren hasta confirmarla.
public interface IUnidadDeTrabajo
{
    Task<T> EjecutarEnTransaccionAsync<T>(Func<Task<T>> operacion, CancellationToken cancellationToken);

    Task EjecutarEnTransaccionAsync(Func<Task> operacion, CancellationToken cancellationToken);
}
