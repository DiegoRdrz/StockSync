namespace StockSync.Inventory.Domain.Common;

internal static class RelojUtc
{
    // PostgreSQL conserva microsegundos; truncar evita devolver fracciones que se perderían al guardar.
    public static DateTime Ahora()
    {
        var ahora = DateTime.UtcNow;
        return new DateTime(ahora.Ticks - ahora.Ticks % TimeSpan.TicksPerMicrosecond, DateTimeKind.Utc);
    }
}
