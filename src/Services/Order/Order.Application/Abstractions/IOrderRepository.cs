using OrderEntity = Order.Domain.Orders.Order;

namespace Order.Application.Abstractions;

public interface IOrderRepository
{
    Task<OrderEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    void Add(OrderEntity order);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
