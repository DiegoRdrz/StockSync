namespace StockSync.Inventory.Domain.Exceptions;

public sealed class InsufficientStockException : DomainException
{
    public InsufficientStockException(int available, int requested)
        : base($"Insufficient stock. Available: {available}; requested: {requested}. Cannot record the issue.")
    {
    }
}
