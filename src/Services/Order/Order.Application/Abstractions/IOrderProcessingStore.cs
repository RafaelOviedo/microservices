using OrderEntity = Order.Domain.Orders.Order;

namespace Order.Application.Abstractions;

public interface IOrderProcessingStore
{
    Task<OrderEntity> ExecuteAsync(Guid id, Func<OrderEntity, CancellationToken, Task> action, CancellationToken cancellationToken);
    Task<IReadOnlyList<Guid>> GetDueIdsAsync(DateTimeOffset now, CancellationToken cancellationToken);
}
