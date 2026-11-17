using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockSync.Orders.Domain.Common;
using StockSync.Orders.Domain.Entities;

namespace StockSync.Orders.Infrastructure.Configurations;

// Columns shared by OrderLines and PurchaseOrderLines. StockLine is not mapped as a hierarchy;
// each line type has its own table.
internal static class StockLineConfiguration
{
    public static void Configure<TLine>(EntityTypeBuilder<TLine> builder, string table) where TLine : StockLine
    {
        builder.ToTable(table, t =>
        {
            t.HasCheckConstraint($"CK_{table}_LineNumber", "\"LineNumber\" > 0");
            t.HasCheckConstraint($"CK_{table}_Quantity", "\"Quantity\" > 0");
            t.HasCheckConstraint($"CK_{table}_UnitPrice", "\"UnitPrice\" > 0");
            t.HasCheckConstraint($"CK_{table}_StockStatus", EnumCheck.In<StockLineStatus>("StockStatus"));
        });

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();

        builder.Property(l => l.ProductName).IsRequired().HasMaxLength(StockLine.ProductNameMaxLength);
        builder.Property(l => l.ProductSku).IsRequired().HasMaxLength(StockLine.ProductSkuMaxLength);
        builder.Property(l => l.UnitPrice).HasPrecision(18, Amount.DecimalPlaces);
        builder.Property(l => l.LineTotal).HasPrecision(18, Amount.DecimalPlaces);
        builder.Property(l => l.StockStatus).HasConversion<string>().HasMaxLength(20);

        builder.Property<Guid>(OrdersDbContext.TenantIdProperty);
    }
}

internal static class EnumCheck
{
    // Statuses are stored as text; the check constraint keeps the column within the enum names.
    public static string In<TEnum>(string column) where TEnum : struct, Enum =>
        $"\"{column}\" IN ({string.Join(", ", Enum.GetNames<TEnum>().Select(n => $"'{n}'"))})";
}
