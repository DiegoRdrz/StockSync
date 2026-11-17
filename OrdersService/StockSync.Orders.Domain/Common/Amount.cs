namespace StockSync.Orders.Domain.Common;

// Amounts use numeric(18,2), the same precision Inventory uses for prices.
public static class Amount
{
    public const int DecimalPlaces = 2;
    public const decimal MaxValue = 9_999_999_999_999_999.99m;

    public static bool IsRepresentable(decimal value) =>
        value <= MaxValue && decimal.Round(value, DecimalPlaces) == value;
}
