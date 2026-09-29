namespace Order.Domain.Orders;

public enum OrderStatus
{
    PendingStockConfirmation = 0,
    Confirming = 1,
    Confirmed = 2,
    CompensationPending = 3,
    Cancelled = 4,
    Rejected = 5
}
