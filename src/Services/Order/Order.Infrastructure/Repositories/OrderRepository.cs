using Microsoft.EntityFrameworkCore;
using Order.Application.Abstractions;
using Order.Application.Exceptions;
using Order.Infrastructure.Persistence;
using OrderEntity = Order.Domain.Orders.Order;

namespace Order.Infrastructure.Repositories;

public sealed class OrderRepository(OrderDbContext dbContext) : IOrderRepository
{
    public Task<OrderEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => dbContext.Orders.Include(order => order.Items).FirstOrDefaultAsync(order => order.Id == id, cancellationToken);

    public void Add(OrderEntity order) => dbContext.Orders.Add(order);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException exception) { throw new ConcurrencyConflictException(exception); }
    }
}
