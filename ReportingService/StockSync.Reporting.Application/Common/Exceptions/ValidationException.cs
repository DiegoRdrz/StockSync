namespace StockSync.Reporting.Application.Common.Exceptions;

public class ValidationException : Exception
{
    public ValidationException(IDictionary<string, string[]> errors)
        : base("One or more fields are invalid.")
    {
        Errors = errors;
    }

    public IDictionary<string, string[]> Errors { get; }
}
