namespace StockSync.Inventory.Domain.Common;

internal static class UtcClock
{
    // PostgreSQL stores microseconds; truncate fractions that would be lost on save.
    public static DateTime Now()
    {
        var now = DateTime.UtcNow;
        return new DateTime(now.Ticks - now.Ticks % TimeSpan.TicksPerMicrosecond, DateTimeKind.Utc);
    }
}
