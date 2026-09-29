using Order.Application.Orders;
using OrderEntity = Order.Domain.Orders.Order;

namespace Order.Application.Abstractions;

public interface IOrderRepository
{
    Task<OrderEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<(IReadOnlyList<OrderEntity> Items, int TotalCount)> ListAsync(OrderHistoryQuery query, CancellationToken cancellationToken);
    void Add(OrderEntity order);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
