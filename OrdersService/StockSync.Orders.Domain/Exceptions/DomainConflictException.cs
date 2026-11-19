namespace StockSync.Orders.Domain.Exceptions;

// The operation is valid in general but not in the document's current state,
// for example confirming an order twice or adding a product that is already present.
public sealed class DomainConflictException : DomainException
{
    public DomainConflictException(string message) : base(message)
    {
    }
}
