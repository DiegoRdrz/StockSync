namespace StockSync.Inventory.Application.Common.Exceptions;

public class ValidationException : Exception
{
    public ValidationException(IDictionary<string, string[]> errors)
        : base("Uno o más campos no son válidos.")
    {
        Errors = errors;
    }

    public IDictionary<string, string[]> Errors { get; }
}
