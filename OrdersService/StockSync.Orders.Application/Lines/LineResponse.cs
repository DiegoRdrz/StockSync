using StockSync.Orders.Domain.Entities;

namespace StockSync.Orders.Application.Lines;

public sealed record LineResponse(
    Guid Id,
    int LineNumber,
    Guid ProductId,
    Guid StockId,
    string ProductName,
    string ProductSku,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal,
    string StockStatus,
    Guid? StockMovementId,
    Guid? CompensationMovementId)
{
    public static LineResponse From(StockLine line) => new(
        line.Id,
        line.LineNumber,
        line.ProductId,
        line.StockId,
        line.ProductName,
        line.ProductSku,
        line.Quantity,
        line.UnitPrice,
        line.LineTotal,
        line.StockStatus.ToString(),
        line.StockMovementId,
        line.CompensationMovementId);

    public static IReadOnlyList<LineResponse> FromLines(IEnumerable<StockLine> lines) =>
        lines.OrderBy(l => l.LineNumber).Select(From).ToList();
}
