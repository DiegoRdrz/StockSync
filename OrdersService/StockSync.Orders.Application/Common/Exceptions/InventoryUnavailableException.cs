namespace StockSync.Orders.Application.Common.Exceptions;

// Inventory could not be reached or did not return a usable answer.
public class InventoryUnavailableException : Exception
{
    public InventoryUnavailableException(string message) : base(message)
    {
    }

    public InventoryUnavailableException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
