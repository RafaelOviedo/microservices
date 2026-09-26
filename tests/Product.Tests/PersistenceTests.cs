using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Product.Application.Abstractions;
using Product.Application.Exceptions;
using Product.Domain.ValueObjects;
using Product.Infrastructure.Persistence;
using ProductEntity = Product.Domain.Products.Product;

namespace Product.Tests;

public sealed class PersistenceTests(ProductApiFactory factory) : IClassFixture<ProductApiFactory>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RemoveIsConvertedToSoftDeleteForSyncAndAsyncSaves(bool asyncSave)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ProductDbContext>();
        var product = ProductEntity.Create("Original", "Descripción original", Money.From(25.99m), 8, DateTimeOffset.UtcNow);
        db.Products.Add(product);
        await db.SaveChangesAsync();
        product.Update("Cambio sin guardar", "Otra", Money.From(50), 2, DateTimeOffset.UtcNow);
        db.Products.Remove(product);
        if (asyncSave) await db.SaveChangesAsync();
        else db.SaveChanges();
        db.ChangeTracker.Clear();

        Assert.False(await db.Products.AnyAsync(item => item.Id == product.Id));
        var retained = await db.Products.IgnoreQueryFilters().SingleAsync(item => item.Id == product.Id);
        Assert.True(retained.IsDeleted);
        Assert.NotNull(retained.DeletedAtUtc);
        Assert.Equal("Original", retained.Name);
        Assert.Equal(25.99m, retained.Price.Amount);
        Assert.Equal(8, retained.Stock);
    }

    [Fact]
    public async Task StaleWriteCannotOverwriteAConcurrentSoftDelete()
    {
        await using var firstScope = factory.Services.CreateAsyncScope();
        var first = firstScope.ServiceProvider.GetRequiredService<IProductRepository>();
        var product = ProductEntity.Create("Concurrencia", "Descripción", Money.From(10), 5, DateTimeOffset.UtcNow);
        first.Add(product);
        await first.SaveChangesAsync(default);

        await using var secondScope = factory.Services.CreateAsyncScope();
        var second = secondScope.ServiceProvider.GetRequiredService<IProductRepository>();
        var stale = (await second.GetByIdAsync(product.Id, default))!;
        product.Delete(DateTimeOffset.UtcNow);
        await first.SaveChangesAsync(default);
        stale.Update("No debe guardarse", "Descripción", Money.From(20), 10, DateTimeOffset.UtcNow);
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => second.SaveChangesAsync(default));

        await using var verification = factory.Services.CreateAsyncScope();
        var db = verification.ServiceProvider.GetRequiredService<ProductDbContext>();
        var retained = await db.Products.IgnoreQueryFilters().SingleAsync(item => item.Id == product.Id);
        Assert.True(retained.IsDeleted);
        Assert.Equal("Concurrencia", retained.Name);
        Assert.Equal(5, retained.Stock);
    }
}
