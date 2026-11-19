using System.Text.Json;
using System.Text.Json.Serialization;
using StockSync.Reporting.Domain;

namespace StockSync.Reporting.Application;

public sealed record SaleConfirmed(Guid EventId, DateTime OccurredAt, Guid TenantId, Guid OrderId,
    Guid BranchId, DateTime ConfirmedAt, decimal Total, List<SaleLineEvent> Lines)
{
    public const string RoutingKey = "orders.sale-confirmed.v1";
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static SaleConfirmed Parse(ReadOnlyMemory<byte> body, string routingKey, string? type, string? messageId, string? tenant)
    {
        if (routingKey != RoutingKey || type != RoutingKey)
            throw new InvalidDataException("Unexpected sales routing key or message type.");
        var sale = JsonSerializer.Deserialize<SaleConfirmed>(body.Span, Options);
        if (sale is null || sale.EventId == Guid.Empty || !Guid.TryParse(messageId, out var id) || id != sale.EventId)
            throw new InvalidDataException("Missing or inconsistent event identity.");
        if (sale.TenantId == Guid.Empty || !Guid.TryParse(tenant, out var owner) || owner != sale.TenantId)
            throw new InvalidDataException("Missing or inconsistent tenant.");
        if (sale.OrderId == Guid.Empty || sale.BranchId == Guid.Empty || sale.OccurredAt.Kind != DateTimeKind.Utc ||
            sale.ConfirmedAt.Kind != DateTimeKind.Utc || sale.Total <= 0 || sale.Lines is not { Count: > 0 })
            throw new InvalidDataException("Invalid sale fields.");
        var numbers = new HashSet<int>();
        foreach (var line in sale.Lines)
        {
            if (line is null || line.LineNumber <= 0 || !numbers.Add(line.LineNumber) || line.ProductId == Guid.Empty ||
                string.IsNullOrWhiteSpace(line.ProductSku) || string.IsNullOrWhiteSpace(line.ProductName) ||
                line.Quantity <= 0 || line.UnitPrice <= 0 || line.LineTotal <= 0 || line.StockMovementId == Guid.Empty)
                throw new InvalidDataException("Invalid sale line fields.");
        }
        return sale;
    }

    public Sale Project() => new()
    {
        OrderId = OrderId, BranchId = BranchId, ConfirmedAt = ConfirmedAt, Total = Total,
        Units = Lines.Sum(l => (long)l.Quantity),
        Lines = Lines.Select(l => new SaleLine
        {
            OrderId = OrderId, LineNumber = l.LineNumber, ProductId = l.ProductId, ProductSku = l.ProductSku,
            ProductName = l.ProductName, Quantity = l.Quantity, UnitPrice = l.UnitPrice,
            LineTotal = l.LineTotal, StockMovementId = l.StockMovementId
        }).ToList()
    };
}

public sealed record SaleLineEvent(int LineNumber, Guid ProductId, string ProductSku, string ProductName,
    int Quantity, decimal UnitPrice, decimal LineTotal, [property: JsonRequired] Guid? StockMovementId);
