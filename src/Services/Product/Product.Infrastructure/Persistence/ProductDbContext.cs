using Microsoft.EntityFrameworkCore;
using ProductEntity = Product.Domain.Products.Product;

namespace Product.Infrastructure.Persistence;

public sealed class ProductDbContext(DbContextOptions<ProductDbContext> options, TimeProvider clock)
    : DbContext(options)
{
    public DbSet<ProductEntity> Products => Set<ProductEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ProductDbContext).Assembly);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplySoftDelete();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ApplySoftDelete();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ApplySoftDelete()
    {
        foreach (var entry in ChangeTracker.Entries<ProductEntity>().Where(x => x.State == EntityState.Deleted).ToList())
        {
            // Incluso Remove/RemoveRange deben conservar el registro. Solo actualizar los campos del borrado.
            // Descartar cambios pendientes: una baja no debe guardar ediciones ajenas al borrado.
            entry.CurrentValues.SetValues(entry.OriginalValues);
            entry.State = EntityState.Unchanged;
            entry.Entity.Delete(clock.GetUtcNow());
            entry.Property(x => x.IsDeleted).IsModified = true;
            entry.Property(x => x.DeletedAtUtc).IsModified = true;
            entry.Property(x => x.UpdatedAtUtc).IsModified = true;
            entry.Property(x => x.Version).IsModified = true;
        }
    }
}
