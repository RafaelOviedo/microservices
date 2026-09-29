using Microsoft.EntityFrameworkCore;
using Order.Application.Abstractions;
using Order.Application.Exceptions;
using Order.Application.Orders;
using Order.Infrastructure.Persistence;
using OrderEntity = Order.Domain.Orders.Order;

namespace Order.Infrastructure.Repositories;

public sealed class OrderRepository(OrderDbContext dbContext) : IOrderRepository
{
    public Task<OrderEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => dbContext.Orders.Include(order => order.Items).FirstOrDefaultAsync(order => order.Id == id, cancellationToken);

    public async Task<(IReadOnlyList<OrderEntity> Items, int TotalCount)> ListAsync(OrderHistoryQuery query, CancellationToken cancellationToken)
    {
        var orders = dbContext.Orders.AsNoTracking();
        if (query.CustomerId is { } customerId) orders = orders.Where(order => order.Customer.Id == customerId);
        if (query.Status is { } status) orders = orders.Where(order => order.Status == status);
        if (query.From is { } from)
        {
            var utc = from.ToUniversalTime();
            orders = orders.Where(order => order.OrderedAtUtc >= utc);
        }
        if (query.To is { } to)
        {
            var utc = to.ToUniversalTime();
            orders = orders.Where(order => order.OrderedAtUtc <= utc);
        }
        // Use the same snapshot for the count and page, even when orders are created or confirmed concurrently.
        await using var transaction = await dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, cancellationToken);
        var totalCount = await orders.CountAsync(cancellationToken);
        var items = await orders.OrderByDescending(order => order.OrderedAtUtc).ThenByDescending(order => order.Id)
            .Skip(checked((query.Page - 1) * query.PageSize)).Take(query.PageSize)
            .Include(order => order.Items).ToListAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return (items, totalCount);
    }

    public void Add(OrderEntity order) => dbContext.Orders.Add(order);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException exception) { throw new ConcurrencyConflictException(exception); }
    }
}
