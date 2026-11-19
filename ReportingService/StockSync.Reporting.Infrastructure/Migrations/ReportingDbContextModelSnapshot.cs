using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace StockSync.Reporting.Infrastructure.Migrations;

[DbContext(typeof(ReportingDbContext))]
internal sealed class ReportingDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder model)
    {
        model.HasAnnotation("ProductVersion", "8.0.26").HasAnnotation("Relational:MaxIdentifierLength", 63);
        NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(model);
        model.Entity("StockSync.Reporting.Infrastructure.ProcessedEvent", entity =>
        {
            entity.Property<string>("ConsumerName").IsRequired().HasColumnType("text");
            entity.Property<Guid>("EventId").HasColumnType("uuid");
            entity.Property<DateTime>("ProcessedAt").HasColumnType("timestamp with time zone");
            entity.HasKey("ConsumerName", "EventId").HasName("pk_processed_events");
            entity.ToTable("processed_events");
        });

        model.Entity("StockSync.Reporting.Domain.Sale", entity =>
        {
            entity.Property<Guid>("TenantId").HasColumnType("uuid");
            entity.Property<Guid>("OrderId").HasColumnType("uuid");
            entity.Property<Guid>("BranchId").HasColumnType("uuid");
            entity.Property<DateTime>("ConfirmedAt").HasColumnType("timestamp with time zone");
            entity.Property<decimal>("Total").HasColumnType("numeric");
            entity.Property<long>("Units").HasColumnType("bigint");
            entity.HasKey("TenantId", "OrderId");
            entity.HasIndex("TenantId", "BranchId", "ConfirmedAt");
            entity.HasIndex("TenantId", "ConfirmedAt", "OrderId");
            entity.ToTable("sales");
        });

        model.Entity("StockSync.Reporting.Domain.SaleLine", entity =>
        {
            entity.Property<Guid>("TenantId").HasColumnType("uuid");
            entity.Property<Guid>("OrderId").HasColumnType("uuid");
            entity.Property<int>("LineNumber").HasColumnType("integer");
            entity.Property<Guid>("ProductId").HasColumnType("uuid");
            entity.Property<string>("ProductSku").IsRequired().HasColumnType("text");
            entity.Property<string>("ProductName").IsRequired().HasColumnType("text");
            entity.Property<int>("Quantity").HasColumnType("integer");
            entity.Property<decimal>("UnitPrice").HasColumnType("numeric");
            entity.Property<decimal>("LineTotal").HasColumnType("numeric");
            entity.Property<Guid?>("StockMovementId").HasColumnType("uuid");
            entity.HasKey("TenantId", "OrderId", "LineNumber");
            entity.HasIndex("TenantId", "ProductId");
            entity.ToTable("sale_lines");
        });
        model.Entity("StockSync.Reporting.Domain.SaleLine")
            .HasOne("StockSync.Reporting.Domain.Sale", null).WithMany("Lines")
            .HasForeignKey("TenantId", "OrderId").OnDelete(DeleteBehavior.Cascade).IsRequired();
    }
}
