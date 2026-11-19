using StockSync.Orders.Domain.Common;
using StockSync.Orders.Domain.Exceptions;

namespace StockSync.Orders.Domain.Entities;

// Line rules shared by sales orders and purchase orders.
internal static class StockLines
{
    public const int MaxLines = 100;

    public static void EnsureCanAdd<TLine>(IReadOnlyCollection<TLine> lines, Guid productId) where TLine : StockLine
    {
        if (lines.Count >= MaxLines)
            throw new DomainException($"A document cannot have more than {MaxLines} lines.");
        if (lines.Any(l => l.ProductId == productId))
            throw new DomainConflictException("The product is already in the document; update the line quantity instead.");
    }

    // Next number after the highest current line: unique and ordered among the document's lines.
    public static int NextLineNumber(IEnumerable<StockLine> lines) =>
        lines.Select(l => l.LineNumber).DefaultIfEmpty(0).Max() + 1;

    public static TLine Find<TLine>(IEnumerable<TLine> lines, Guid lineId) where TLine : StockLine =>
        lines.FirstOrDefault(l => l.Id == lineId)
            ?? throw new DomainException($"Line '{lineId}' does not belong to the document.");

    public static decimal Total(IEnumerable<StockLine> lines)
    {
        var total = lines.Sum(l => l.LineTotal);
        if (total > Amount.MaxValue)
            throw new DomainException($"The document total cannot exceed {Amount.MaxValue}.");
        return total;
    }

    public static string? NormalizeText(string? value, int maxLength, string field)
    {
        if (value?.Trim().Length > maxLength)
            throw new DomainException($"{field} cannot exceed {maxLength} characters.");
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    // Review reasons come from Inventory responses; keep them within the column limit.
    public static string ReviewReason(string reason, int maxLength)
    {
        var text = string.IsNullOrWhiteSpace(reason) ? "The stock result is unknown." : reason.Trim();
        return text.Length <= maxLength ? text : text[..maxLength];
    }
}
