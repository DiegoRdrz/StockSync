namespace StockSync.Orders.Application.Common;

public interface IUserContext
{
    string? AuthorizationHeader { get; }
}
