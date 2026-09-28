namespace Order.Application.Orders;

public sealed record CreateOrderRequest(Guid CustomerId, IReadOnlyList<OrderItemRequest?>? Items);
public sealed record OrderItemRequest(Guid ProductId, int Quantity);
