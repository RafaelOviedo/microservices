namespace Order.Application.Orders;

public interface IOrderService
{
    Task<CreateOrderResponse> CreateAsync(CreateOrderRequest request, CancellationToken cancellationToken);
    Task<OrderHistoryResponse> ListAsync(OrderHistoryQuery query, CancellationToken cancellationToken);
    Task<OrderResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}
