namespace StockSync.Identity.Application.Common.Exceptions;

public class UnauthorizedException : Exception
{
    public UnauthorizedException(string message = "You are not authorized to perform this operation.") : base(message)
    {
    }
}

