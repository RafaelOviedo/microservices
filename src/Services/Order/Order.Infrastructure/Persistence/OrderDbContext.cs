using Microsoft.EntityFrameworkCore;
using Order.Domain.Orders;
using OrderEntity = Order.Domain.Orders.Order;

namespace Order.Infrastructure.Persistence;

public sealed class OrderDbContext(DbContextOptions<OrderDbContext> options, TimeProvider clock) : DbContext(options)
{
    public DbSet<OrderEntity> Orders => Set<OrderEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrderDbContext).Assembly);
        // Also hide order lines when they are queried directly through EF.
        modelBuilder.Entity<OrderItem>().HasQueryFilter(item => Orders.Any(order => order.Id == item.OrderId && !order.IsDeleted));
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ProtectHistory();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ProtectHistory();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ProtectHistory()
    {
        foreach (var entry in ChangeTracker.Entries<OrderItem>())
        {
            if (entry.State == EntityState.Deleted)
                throw new InvalidOperationException("Historical items cannot be deleted.");
            if (entry.State != EntityState.Modified) continue;
            var parent = ChangeTracker.Entries<OrderEntity>().SingleOrDefault(x => x.Entity.Id == entry.Entity.OrderId);
            if (entry.Properties.Any(property => property.IsModified && property.Metadata.Name != nameof(OrderItem.ConfirmedQuantity))
                || parent is null || parent.Property(x => x.Status).OriginalValue != OrderStatus.Confirming
                || parent.Entity.Status != OrderStatus.Confirmed)
                throw new InvalidOperationException("The confirmed quantity can only be recorded while confirming the aggregate.");
        }
        foreach (var entry in ChangeTracker.Entries<OrderEntity>().Where(entry => entry.State == EntityState.Deleted).ToList())
        {
            entry.CurrentValues.SetValues(entry.OriginalValues);
            entry.State = EntityState.Unchanged;
            entry.Entity.Delete(clock.GetUtcNow());
            entry.Property(order => order.IsDeleted).IsModified = true;
            entry.Property(order => order.DeletedAtUtc).IsModified = true;
            entry.Property(order => order.Version).IsModified = true;
        }
    }
}
