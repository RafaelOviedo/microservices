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
        // También ocultar las líneas si se consultan directamente mediante EF.
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
        if (ChangeTracker.Entries<OrderItem>().Any(entry => entry.State is EntityState.Deleted or EntityState.Modified))
            throw new InvalidOperationException("Los ítems históricos no se pueden modificar ni eliminar individualmente.");
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
