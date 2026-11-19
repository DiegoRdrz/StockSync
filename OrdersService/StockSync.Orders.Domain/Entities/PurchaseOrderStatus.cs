namespace StockSync.Orders.Domain.Entities;

// Draft ──place──► Ordered ──receive──► Receiving ──all receipts recorded──► Received
//                                           ├──rejected and compensated──► Ordered
//                                           └──unknown stock result──────► RequiresReview
// Draft and Ordered can also move to Cancelled.
public enum PurchaseOrderStatus
{
    Draft = 1,
    // Sent to the supplier; lines can no longer change.
    Ordered = 2,
    // Stock receipts are being recorded in Inventory.
    Receiving = 3,
    Received = 4,
    // Inventory's result is unknown; a person must reconcile the stock movements.
    RequiresReview = 5,
    Cancelled = 6
}
