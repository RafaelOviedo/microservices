using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Order.Application.Abstractions;
using Order.Application.Exceptions;
using Order.Domain.Orders;
using Order.Domain.ValueObjects;
using Order.Infrastructure.Persistence;
using OrderEntity = Order.Domain.Orders.Order;

namespace Order.Tests;

public sealed class PersistenceTests(OrderApiFactory factory) : IClassFixture<OrderApiFactory>
{
    private static OrderEntity Create() => OrderEntity.Create(CustomerSnapshot.From(Guid.NewGuid(), "Ana histórica"),
        [OrderItem.Create(Guid.NewGuid(), "Teclado histórico", Money.From(12.34m), 2),
         OrderItem.Create(Guid.NewGuid(), "Mouse histórico", Money.From(10), 3)], DateTimeOffset.UtcNow);

    [Fact]
    public async Task MigrationAndRepositoryRoundTripPreserveCompleteAggregate()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
        var original = Create();
        repository.Add(original);
        await repository.SaveChangesAsync(default);
        await using var readScope = factory.Services.CreateAsyncScope();
        var loaded = (await readScope.ServiceProvider.GetRequiredService<IOrderRepository>().GetByIdAsync(original.Id, default))!;
        Assert.Equal(original.Customer, loaded.Customer);
        Assert.Equal(original.OrderedAtUtc, loaded.OrderedAtUtc);
        Assert.Equal(54.68m, loaded.Total.Amount);
        Assert.Equal(original.Version, loaded.Version);
        Assert.Equal(OrderStatus.PendingStockConfirmation, loaded.Status);
        Assert.Equal(2, loaded.Items.Count);
        foreach (var item in original.Items)
        {
            var stored = loaded.Items.Single(x => x.Id == item.Id);
            Assert.Equal(item.OrderId, stored.OrderId);
            Assert.Equal(item.ProductId, stored.ProductId);
            Assert.Equal(item.ProductName, stored.ProductName);
            Assert.Equal(item.UnitPrice, stored.UnitPrice);
            Assert.Equal(item.Quantity, stored.Quantity);
            Assert.Equal(item.Subtotal, stored.Subtotal);
        }
        var db = readScope.ServiceProvider.GetRequiredService<OrderDbContext>();
        Assert.Equal(2, (await db.Database.GetAppliedMigrationsAsync()).Count());
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task RemoveSoftDeletesOrderAndRetainsAllItems(bool asyncSave, bool loadItems)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        var original = Create();
        db.Orders.Add(original);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var query = loadItems ? db.Orders.Include(order => order.Items) : db.Orders.AsQueryable();
        var tracked = await query.SingleAsync(order => order.Id == original.Id);
        db.Orders.Remove(tracked);
        if (asyncSave) await db.SaveChangesAsync(); else db.SaveChanges();
        db.ChangeTracker.Clear();
        Assert.False(await db.Orders.AnyAsync(order => order.Id == original.Id));
        Assert.False(await db.Set<OrderItem>().AnyAsync(item => item.OrderId == original.Id));
        var retained = await db.Orders.IgnoreQueryFilters().Include(order => order.Items).SingleAsync(order => order.Id == original.Id);
        Assert.True(retained.IsDeleted);
        Assert.NotNull(retained.DeletedAtUtc);
        Assert.Equal(original.Total, retained.Total);
        Assert.Equal(original.Customer, retained.Customer);
        Assert.Equal(2, retained.Items.Count);
        Assert.Equal(2, await db.Set<OrderItem>().IgnoreQueryFilters().CountAsync(item => item.OrderId == original.Id));
    }

    [Fact]
    public async Task DomainSoftDeletePersistsWithoutDeletingItems()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
        var order = Create();
        repository.Add(order);
        await repository.SaveChangesAsync(default);
        order.Delete(DateTimeOffset.UtcNow);
        await repository.SaveChangesAsync(default);
        Assert.Null(await repository.GetByIdAsync(order.Id, default));
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        Assert.Equal(2, await db.Set<OrderItem>().IgnoreQueryFilters().CountAsync(item => item.OrderId == order.Id));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HistoricalItemsCannotBeRemovedOrEdited(bool delete)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        var order = Create();
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        var item = order.Items.First();
        if (delete) db.Remove(item);
        else db.Entry(item).Property(x => x.ProductName).CurrentValue = "Cambio prohibido";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        var stored = await db.Set<OrderItem>().SingleAsync(x => x.Id == item.Id);
        Assert.Equal("Teclado histórico", stored.ProductName);
    }

    [Fact]
    public async Task ConcurrentDeletionIsReportedAsConflict()
    {
        await using var firstScope = factory.Services.CreateAsyncScope();
        var first = firstScope.ServiceProvider.GetRequiredService<IOrderRepository>();
        var order = Create();
        first.Add(order);
        await first.SaveChangesAsync(default);
        await using var secondScope = factory.Services.CreateAsyncScope();
        var second = secondScope.ServiceProvider.GetRequiredService<IOrderRepository>();
        var stale = (await second.GetByIdAsync(order.Id, default))!;
        order.Delete(DateTimeOffset.UtcNow);
        await first.SaveChangesAsync(default);
        stale.Delete(DateTimeOffset.UtcNow);
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => second.SaveChangesAsync(default));
    }
}
