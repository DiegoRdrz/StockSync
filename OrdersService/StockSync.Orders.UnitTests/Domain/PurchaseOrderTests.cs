using StockSync.Orders.Domain.Entities;
using StockSync.Orders.Domain.Exceptions;

namespace StockSync.Orders.UnitTests.Domain;

public class PurchaseOrderTests
{
    private static PurchaseOrder CreateDraft() => PurchaseOrder.Create(Guid.NewGuid(), "Acme Tools", null);

    private static PurchaseOrderLine AddLine(PurchaseOrder purchaseOrder, int quantity = 5, decimal unitPrice = 4m) =>
        purchaseOrder.AddLine(Guid.NewGuid(), Guid.NewGuid(), "Hammer", "HAM-001", quantity, unitPrice);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithoutSupplier_Throws(string supplierName)
    {
        Assert.Throws<DomainException>(() => PurchaseOrder.Create(Guid.NewGuid(), supplierName, null));
    }

    [Fact]
    public void Create_WithTooLongSupplier_Throws()
    {
        Assert.Throws<DomainException>(() =>
            PurchaseOrder.Create(Guid.NewGuid(), new string('x', PurchaseOrder.SupplierNameMaxLength + 1), null));
    }

    [Fact]
    public void Place_WithoutLines_Throws()
    {
        var purchaseOrder = CreateDraft();

        Assert.Throws<DomainException>(purchaseOrder.Place);
    }

    [Fact]
    public void FullFlow_PlacedAndReceived()
    {
        var purchaseOrder = CreateDraft();
        var line = AddLine(purchaseOrder);

        purchaseOrder.Place();
        Assert.Throws<DomainConflictException>(() => AddLine(purchaseOrder));

        purchaseOrder.StartReceiving();
        purchaseOrder.MarkLineApplied(line.Id, Guid.NewGuid());
        purchaseOrder.CompleteReceiving();

        Assert.Equal(PurchaseOrderStatus.Received, purchaseOrder.Status);
        Assert.NotNull(purchaseOrder.OrderedAt);
        Assert.NotNull(purchaseOrder.ReceivedAt);
        Assert.Equal(20m, purchaseOrder.Total);
    }

    [Fact]
    public void StartReceiving_FromDraft_Throws()
    {
        var purchaseOrder = CreateDraft();
        AddLine(purchaseOrder);

        Assert.Throws<DomainConflictException>(purchaseOrder.StartReceiving);
    }

    [Fact]
    public void RevertToOrdered_AfterCompensation_AllowsAnotherReception()
    {
        var purchaseOrder = CreateDraft();
        var line = AddLine(purchaseOrder);
        purchaseOrder.Place();
        purchaseOrder.StartReceiving();
        purchaseOrder.MarkLineApplied(line.Id, Guid.NewGuid());
        purchaseOrder.MarkLineCompensated(line.Id, Guid.NewGuid());

        purchaseOrder.RevertToOrdered();

        Assert.Equal(PurchaseOrderStatus.Ordered, purchaseOrder.Status);
        Assert.Equal(StockLineStatus.Pending, line.StockStatus);
        purchaseOrder.StartReceiving();
    }

    [Fact]
    public void Cancel_DraftOrOrdered_IsAllowed()
    {
        var draft = CreateDraft();
        draft.Cancel();
        Assert.Equal(PurchaseOrderStatus.Cancelled, draft.Status);

        var ordered = CreateDraft();
        AddLine(ordered);
        ordered.Place();
        ordered.Cancel();
        Assert.Equal(PurchaseOrderStatus.Cancelled, ordered.Status);
    }

    [Fact]
    public void Cancel_ReceivingOrReceived_Throws()
    {
        var purchaseOrder = CreateDraft();
        var line = AddLine(purchaseOrder);
        purchaseOrder.Place();
        purchaseOrder.StartReceiving();
        Assert.Throws<DomainConflictException>(purchaseOrder.Cancel);

        purchaseOrder.MarkLineApplied(line.Id, Guid.NewGuid());
        purchaseOrder.CompleteReceiving();
        Assert.Throws<DomainConflictException>(purchaseOrder.Cancel);
    }
}
