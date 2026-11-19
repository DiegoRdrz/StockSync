namespace StockSync.Orders.Domain.Entities;

// Draft ──confirm──► Confirming ──all issues recorded──► Confirmed
//   │                    ├──rejected and compensated──► Draft
//   │                    └──unknown stock result──────► RequiresReview
//   └──cancel──► Cancelled
public enum OrderStatus
{
    Draft = 1,
    // Stock issues are being recorded in Inventory; the order cannot be edited.
    Confirming = 2,
    Confirmed = 3,
    // Inventory's result is unknown; a person must reconcile the stock movements.
    RequiresReview = 4,
    Cancelled = 5
}
