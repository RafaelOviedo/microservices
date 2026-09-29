namespace Order.Application.Orders;

public sealed record OrderResponse(Guid Id, Guid CustomerId, string CustomerName, DateTimeOffset OrderedAtUtc,
    string Status, decimal Total, IReadOnlyList<OrderItemResponse> Items, string? FailureReason = null, DateTimeOffset? ConfirmedAtUtc = null);
public sealed record OrderItemResponse(Guid ProductId, string ProductName, decimal UnitPrice, int Quantity, decimal Subtotal);
public sealed record QuantityAdjustment(Guid ProductId, int RequestedQuantity, int AcceptedQuantity);
public sealed record CreateOrderResponse(OrderResponse Order, IReadOnlyList<QuantityAdjustment> QuantityAdjustments);
