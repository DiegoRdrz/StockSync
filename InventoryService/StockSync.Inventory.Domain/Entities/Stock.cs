using StockSync.Inventory.Domain.Exceptions;

namespace StockSync.Inventory.Domain.Entities;

// Product quantity at a branch. BranchId identifies a branch owned by another service;
// Inventory neither models the branch nor validates it against another context.
public class Stock
{
    public Guid Id { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid BranchId { get; private set; }
    public int Quantity { get; private set; }

    private Stock()
    {
    }

    public static Stock Create(Guid productId, Guid branchId, int quantity)
    {
        if (productId == Guid.Empty)
            throw new DomainException("The specified product is invalid.");
        if (branchId == Guid.Empty)
            throw new DomainException("The specified branch is invalid.");
        EnsureValidQuantity(quantity);

        return new Stock
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            BranchId = branchId,
            Quantity = quantity
        };
    }

    // Every balance change records a movement to explain the current balance.
    // Return null when the quantity is unchanged; movements always have positive quantities.
    public StockMovement? UpdateQuantity(int newQuantity)
    {
        EnsureValidQuantity(newQuantity);
        if (newQuantity == Quantity)
            return null;

        var previousQuantity = Quantity;
        Quantity = newQuantity;
        return new StockMovement(this, StockMovementType.Adjustment, Math.Abs(newQuantity - previousQuantity), previousQuantity);
    }

    public StockMovement RecordMovement(StockMovementType type, int quantity)
    {
        if (quantity <= 0)
            throw new DomainException("Movement quantity must be greater than zero.");

        if (type is not (StockMovementType.Receipt or StockMovementType.Issue))
            throw new DomainException("The movement type is invalid.");

        if (type == StockMovementType.Issue && quantity > Quantity)
            throw new InsufficientStockException(Quantity, quantity);

        if (type == StockMovementType.Receipt && quantity > int.MaxValue - Quantity)
            throw new DomainException("The receipt exceeds the maximum allowed quantity.");

        var previousQuantity = Quantity;
        Quantity = type == StockMovementType.Receipt ? Quantity + quantity : Quantity - quantity;

        return new StockMovement(this, type, quantity, previousQuantity);
    }

    private static void EnsureValidQuantity(int quantity)
    {
        if (quantity < 0)
            throw new DomainException("Quantity cannot be negative.");
    }
}
