using System.Text.Json.Serialization;

namespace StockSync.Reporting.Application;

public sealed record OrderLineEvent(int LineNumber, Guid ProductId, string ProductSku, string ProductName,
    int Quantity, decimal UnitPrice, decimal LineTotal, [property: JsonRequired] Guid? StockMovementId)
{
    public static void Validate(IEnumerable<OrderLineEvent> lines)
    {
        var numbers = new HashSet<int>();
        foreach (var line in lines)
        {
            if (line is null || line.LineNumber <= 0 || !numbers.Add(line.LineNumber) || line.ProductId == Guid.Empty ||
                string.IsNullOrWhiteSpace(line.ProductSku) || string.IsNullOrWhiteSpace(line.ProductName) ||
                line.Quantity <= 0 || line.UnitPrice <= 0 || line.LineTotal <= 0 || line.StockMovementId == Guid.Empty)
                throw new InvalidDataException("Invalid order line fields.");
        }
    }
}
