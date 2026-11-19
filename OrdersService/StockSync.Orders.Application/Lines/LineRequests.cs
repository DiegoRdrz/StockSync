namespace StockSync.Orders.Application.Lines;

// The price is not part of the request: Orders always takes it from Inventory.
public sealed record LineRequest(Guid ProductId, int Quantity);

public sealed record LineQuantityRequest(int Quantity);
