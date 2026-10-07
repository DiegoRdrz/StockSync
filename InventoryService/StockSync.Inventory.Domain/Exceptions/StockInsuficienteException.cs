namespace StockSync.Inventory.Domain.Exceptions;

public sealed class StockInsuficienteException : DomainException
{
    public StockInsuficienteException(int disponible, int solicitado)
        : base($"Stock insuficiente. Disponible: {disponible}; solicitado: {solicitado}. No se puede realizar la salida.")
    {
    }
}
