using Microsoft.EntityFrameworkCore;
using Order.Application.Abstractions;
using Order.Application.Exceptions;
using Order.Domain.Orders;
using Order.Infrastructure.Persistence;
using OrderEntity = Order.Domain.Orders.Order;

namespace Order.Infrastructure.Repositories;

public sealed class OrderProcessingStore(OrderDbContext db) : IOrderProcessingStore
{
    public async Task<OrderEntity> ExecuteAsync(Guid id, Func<OrderEntity, CancellationToken, Task> action, CancellationToken cancellationToken)
    {
        db.ChangeTracker.Clear();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        // Serialize confirmation, cancellation, and recovery per order, including across multiple instances.
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT \"Id\" FROM \"Orders\" WHERE \"Id\" = {id} FOR UPDATE", cancellationToken);
        var order = await db.Orders.Include(x => x.Items).SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new OrderNotFoundException(id);
        await action(order, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return order;
    }

    public async Task<IReadOnlyList<Guid>> GetDueIdsAsync(DateTimeOffset now, CancellationToken cancellationToken)
        => await db.Orders.AsNoTracking()
            .Where(order => (order.Status == OrderStatus.Confirming || order.Status == OrderStatus.CompensationPending)
                && order.NextAttemptAtUtc <= now)
            .OrderBy(order => order.NextAttemptAtUtc).ThenBy(order => order.Id).Select(order => order.Id).Take(50).ToArrayAsync(cancellationToken);
}
